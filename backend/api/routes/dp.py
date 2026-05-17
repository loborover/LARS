from fastapi import APIRouter, Depends, Query, HTTPException, BackgroundTasks
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Optional
from datetime import date
from sqlmodel import select
from core.database import get_session, engine
from core.deps import require_role
from services import daily_plan_service
from schemas.daily_plan import DailyPlanRead, DailyPlanLotRead, DailyPlanViewResponse
from models.daily_plan import DailyPlan, DailyPlanLot
from models.import_batch import ImportBatch

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

@router.get("/batches")
async def get_dp_batches(session: AsyncSession = Depends(get_session)) -> list[dict]:
    """DP import batch 목록과 날짜 범위, Target 여부 반환"""
    from sqlalchemy import func
    from core.redis_client import get_redis

    # import_batches 테이블에서 DP 배치 목록 조회
    stmt = select(ImportBatch).where(
        ImportBatch.target_table == "daily_plan",
        ImportBatch.status == "success"
    ).order_by(ImportBatch.finished_at.desc())
    res = await session.execute(stmt)
    batches = res.scalars().all()

    # Redis에서 현재 target batch_id 조회
    redis = await get_redis()
    target_raw = await redis.get("dp:target_batch_id")
    target_batch_id = int(target_raw) if target_raw else None

    result = []
    for b in batches:
        # 해당 batch에 속한 DailyPlan의 날짜 범위와 lot 수 조회
        date_stmt = select(
            func.min(DailyPlan.plan_date),
            func.max(DailyPlan.plan_date),
        ).where(DailyPlan.import_batch_id == b.id)
        date_res = await session.execute(date_stmt)
        date_row = date_res.one_or_none()

        lot_stmt = select(func.count(DailyPlanLot.id)).where(DailyPlanLot.import_batch_id == b.id)
        lot_res = await session.execute(lot_stmt)
        lot_count = lot_res.scalar_one()

        result.append({
            "batch_id": b.id,
            "date_min": date_row[0].date().isoformat() if date_row and date_row[0] else None,
            "date_max": date_row[1].date().isoformat() if date_row and date_row[1] else None,
            "lot_count": lot_count,
            "finished_at": b.finished_at.isoformat() if b.finished_at else None,
            "is_target": b.id == target_batch_id,
            "data_source": b.data_source,
        })
    return result

@router.delete("/batches/{batch_id}", status_code=200)
async def delete_dp_batch(
    batch_id: int,
    session: AsyncSession = Depends(get_session)
) -> dict:
    """
    DP import batch 삭제.
    삭제 cascade 순서:
      1. PartListSnapshot (lot_id FK)
      2. DailyPlanLot (import_batch_id)
      3. DailyPlan (import_batch_id 기준, lot 없는 것만)
      4. ImportBatch
      5. Redis target 초기화 (삭제된 batch가 target이었을 경우)
    """
    from sqlalchemy import delete as sa_delete
    from models.part_list import PartListSnapshot
    from core.redis_client import get_redis

    # 1. 배치 존재 확인
    batch = await session.get(ImportBatch, batch_id)
    if not batch or batch.target_table != "daily_plan":
        raise HTTPException(status_code=404, detail="DP batch not found")

    # 2. 해당 배치의 lot id 목록 수집
    lot_ids_stmt = select(DailyPlanLot.id).where(DailyPlanLot.import_batch_id == batch_id)
    lot_ids_res = await session.execute(lot_ids_stmt)
    lot_ids = [row[0] for row in lot_ids_res.all()]

    # 3. PartListSnapshot 삭제 (FK 우선)
    if lot_ids:
        await session.execute(
            sa_delete(PartListSnapshot).where(PartListSnapshot.lot_id.in_(lot_ids))
        )

    # 4. DailyPlanLot 삭제
    await session.execute(
        sa_delete(DailyPlanLot).where(DailyPlanLot.import_batch_id == batch_id)
    )

    # 5. lot이 없어진 DailyPlan 삭제 (import_batch_id가 이 배치인 것 중 lots가 없는 것)
    plan_ids_stmt = select(DailyPlan.id).where(DailyPlan.import_batch_id == batch_id)
    plan_ids_res = await session.execute(plan_ids_stmt)
    plan_ids = [row[0] for row in plan_ids_res.all()]

    if plan_ids:
        # lot이 남아 있는 plan_id는 제외 (다른 배치가 덮어쓴 경우)
        has_lots_stmt = select(DailyPlanLot.plan_id).where(
            DailyPlanLot.plan_id.in_(plan_ids)
        ).distinct()
        has_lots_res = await session.execute(has_lots_stmt)
        plans_with_lots = set(row[0] for row in has_lots_res.all())
        empty_plan_ids = [pid for pid in plan_ids if pid not in plans_with_lots]

        if empty_plan_ids:
            await session.execute(
                sa_delete(DailyPlan).where(DailyPlan.id.in_(empty_plan_ids))
            )

    # 6. ImportBatch 삭제
    await session.delete(batch)
    await session.commit()

    # 7. Redis target 초기화
    redis = await get_redis()
    target_raw = await redis.get("dp:target_batch_id")
    if target_raw and int(target_raw) == batch_id:
        await redis.delete("dp:target_batch_id")

    return {"status": "deleted", "batch_id": batch_id}

@router.get("/lots-raw")
async def get_lots_raw(
    batch_id: int = Query(...),
    session: AsyncSession = Depends(get_session)
) -> list[dict]:
    """
    특정 batch의 모든 lot을 flat하게 반환.
    - W/O 없는 행 제외
    - suffix는 DailyPlanLot에 직접 저장된 값 우선
    - daily_qty_json을 날짜별 dict로 파싱해서 포함
    - ProductionLine JOIN으로 line_code 포함
    """
    import json
    from models.daily_plan import DailyPlan, ProductionLine

    stmt = (
        select(DailyPlanLot, ProductionLine.code.label("line_code"))
        .join(DailyPlan, DailyPlanLot.plan_id == DailyPlan.id)
        .join(ProductionLine, DailyPlan.line_id == ProductionLine.id)
        .where(
            DailyPlanLot.import_batch_id == batch_id,
            DailyPlanLot.wo_number.is_not(None),
            DailyPlanLot.wo_number != ""
        )
        .order_by(ProductionLine.code, DailyPlanLot.sort_order)
    )
    res = await session.execute(stmt)
    rows = res.all()

    result = []
    for lot, line_code in rows:
        # suffix: DailyPlanLot에 직접 저장된 값 우선, 없으면 빈 문자열
        suffix = lot.suffix or ""
        model_number = f"{lot.model_code}.{suffix}" if suffix else lot.model_code
        remain_qty = (lot.planned_qty or 0) - (lot.output_qty or 0)

        daily_qty: dict = {}
        if lot.daily_qty_json:
            try:
                daily_qty = json.loads(lot.daily_qty_json)
            except Exception:
                pass

        result.append({
            "line_code": line_code or "",
            "planned_start": lot.planned_start.isoformat() if lot.planned_start else None,
            "wo_number": lot.wo_number,
            "model_number": model_number,
            "planned_qty": lot.planned_qty or 0,
            "input_qty": lot.input_qty or 0,
            "output_qty": lot.output_qty or 0,
            "remain_qty": remain_qty,
            "daily_qty": daily_qty,  # {"2026-05-14": 9.0, ...}
        })
    return result

@router.get("/daily", response_model=DailyPlanViewResponse)
async def get_daily_view(
    date: date = Query(..., description="조회 날짜 (YYYY-MM-DD)"),
    line_code: Optional[str] = Query(None),
    session: AsyncSession = Depends(get_session)
):
    return await daily_plan_service.get_daily_view(session, date, line_code)

@router.get("/dates", response_model=List[str])
async def get_available_dates(session: AsyncSession = Depends(get_session)):
    """DP 데이터가 존재하는 날짜 목록 반환 (ISO 8601 형식)"""
    from sqlalchemy import func
    stmt = select(func.distinct(DailyPlan.plan_date)).order_by(DailyPlan.plan_date)
    res = await session.execute(stmt)
    dates = res.scalars().all()
    return [d.date().isoformat() if hasattr(d, 'date') else str(d)[:10] for d in dates]

@router.post("/set-target")
async def set_target_batch(
    batch_id: int,
    background_tasks: BackgroundTasks,
    session: AsyncSession = Depends(get_session)
) -> dict:
    """Target DP batch 설정 → 즉시 PartList 백그라운드 재계산 트리거"""
    from core.redis_client import get_redis
    from services.part_list_service import recompute_background
    from models.daily_plan import DailyPlan, DailyPlanLot
    from sqlalchemy import func

    redis = await get_redis()
    await redis.set("dp:target_batch_id", str(batch_id))

    # 해당 배치의 고유 plan_date 목록 조회
    stmt = (
        select(func.distinct(DailyPlan.plan_date))
        .join(DailyPlanLot, DailyPlanLot.plan_id == DailyPlan.id)
        .where(DailyPlanLot.import_batch_id == batch_id)
    )
    res = await session.execute(stmt)
    dates_raw = res.scalars().all()
    dates = [d.date() if hasattr(d, "date") else d for d in dates_raw]

    if dates:
        background_tasks.add_task(recompute_background, engine, dates, batch_id)

    return {
        "status": "ok",
        "target_batch_id": batch_id,
        "recompute_triggered": len(dates) > 0,
        "dates_count": len(dates),
    }

@router.get("/target-batch")
async def get_target_batch(session: AsyncSession = Depends(get_session)) -> dict:
    """현재 Target DP batch 정보 반환"""
    from core.redis_client import get_redis
    redis = await get_redis()
    target_raw = await redis.get("dp:target_batch_id")
    if not target_raw:
        return {"target_batch_id": None}
    batch_id = int(target_raw)
    stmt = select(ImportBatch).where(ImportBatch.id == batch_id)
    res = await session.execute(stmt)
    b = res.scalar_one_or_none()
    return {
        "target_batch_id": batch_id,
        "finished_at": b.finished_at.isoformat() if b and b.finished_at else None,
    }

@router.get("", response_model=List[DailyPlanRead])
async def get_plans(
    date_from: Optional[date] = None,
    date_to: Optional[date] = None,
    line_code: Optional[str] = None,
    session: AsyncSession = Depends(get_session)
):
    return await daily_plan_service.list_plans(session, date_from, date_to, line_code)

@router.get("/{plan_id}/lots", response_model=List[DailyPlanLotRead])
async def get_plan_lots(
    plan_id: int,
    session: AsyncSession = Depends(get_session)
):
    lots = await daily_plan_service.get_lots_by_plan(session, plan_id)
    return [
        DailyPlanLotRead(
            id=l.id,
            wo_number=l.wo_number,
            model_code=l.model_code,
            lot_number=l.lot_number,
            planned_qty=l.planned_qty,
            input_qty=l.input_qty,
            output_qty=l.output_qty
        ) for l in lots
    ]
