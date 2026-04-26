from typing import Optional, List
from datetime import date
import polars as pl
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.daily_plan import DailyPlan, DailyPlanLot, ProductionLine
from models.bom import BomModel, BomItem
from models.part_list import PartListSnapshot
from schemas.part_list import PartListItem, PartListResponse

async def recompute_for_dates(session: AsyncSession, dates: List[date], batch_id: int) -> int:
    """
    주어진 날짜들의 DailyPlanLot × BomItem을 계산해 part_list_snapshots에 저장.
    Returns: 총 삽입된 snapshot 레코드 수
    """
    if not dates:
        return 0

    from sqlalchemy import delete
    
    # 1. 해당 날짜의 plan_id 들을 찾는다
    stmt = select(DailyPlan).where(DailyPlan.plan_date.in_(dates))
    res = await session.execute(stmt)
    plans = res.scalars().all()
    plan_ids = [p.id for p in plans]
    
    if not plan_ids:
        return 0

    # 2. plan_id에 속하는 lot 조회
    stmt = select(DailyPlanLot).where(DailyPlanLot.plan_id.in_(plan_ids))
    res = await session.execute(stmt)
    lots = res.scalars().all()
    lot_ids = [l.id for l in lots]
    
    if not lot_ids:
        return 0

    # 3. 기존 part_list_snapshots 삭제
    await session.execute(delete(PartListSnapshot).where(PartListSnapshot.lot_id.in_(lot_ids)))

    # 4. 각 로트별로 BomItem을 가져와 required_qty 계산 후 스냅샷 생성
    snapshots = []
    
    # 캐싱용 (model_id -> List[BomItem])
    model_items_cache = {}
    
    for lot in lots:
        if not lot.model_id:
            print(f"Warning: Lot {lot.lot_number} (Model {lot.model_code}) has no matched BomModel. Skipping PL computation.")
            continue
            
        if lot.model_id not in model_items_cache:
            stmt = select(BomItem).where(BomItem.model_id == lot.model_id)
            res = await session.execute(stmt)
            model_items_cache[lot.model_id] = res.scalars().all()
            
        bom_items = model_items_cache[lot.model_id]
        
        # Determine lot date (from plan)
        plan_date = None
        for p in plans:
            if p.id == lot.plan_id:
                plan_date = p.plan_date
                break
        
        if not plan_date:
            continue
            
        if hasattr(plan_date, 'date'):
            plan_date = plan_date.date()
            
        for b_item in bom_items:
            req_qty = float(b_item.qty) * float(lot.planned_qty)
            snap = PartListSnapshot(
                lot_id=lot.id,
                part_number=b_item.part_number,
                description=b_item.description,
                required_qty=req_qty,
                snapshot_date=plan_date,
                uom=b_item.uom,
                vendor_raw=b_item.vendor_raw,
                import_batch_id=batch_id
            )
            snapshots.append(snap)
            
    session.add_all(snapshots)
    await session.flush()
    total_inserted = len(snapshots)
    
    # 5. PSI 업데이트 트리거
    from services.psi_service import recompute_required_for_dates
    await recompute_required_for_dates(session, dates)
    
    return total_inserted

async def get_pl_summary(session: AsyncSession, plan_date: date, line_code: Optional[str] = None) -> List[dict]:
    # 조인: PartListSnapshot -> DailyPlanLot -> DailyPlan -> ProductionLine
    from sqlalchemy import func
    stmt = (
        select(
            PartListSnapshot.part_number,
            func.max(PartListSnapshot.description).label("description"),
            func.sum(PartListSnapshot.required_qty).label("total_required_qty"),
            func.max(PartListSnapshot.uom).label("uom"),
            func.max(PartListSnapshot.vendor_raw).label("vendor_raw")
        )
        .join(DailyPlanLot, PartListSnapshot.lot_id == DailyPlanLot.id)
        .join(DailyPlan, DailyPlanLot.plan_id == DailyPlan.id)
    )
    
    stmt = stmt.where(PartListSnapshot.snapshot_date == plan_date)
    
    if line_code:
        stmt = stmt.join(ProductionLine, DailyPlan.line_id == ProductionLine.id)
        stmt = stmt.where(ProductionLine.code == line_code)
        
    stmt = stmt.group_by(PartListSnapshot.part_number).order_by(func.sum(PartListSnapshot.required_qty).desc())
    
    res = await session.execute(stmt)
    rows = res.all()
    
    return [
        {
            "part_number": r.part_number,
            "description": r.description,
            "total_required_qty": float(r.total_required_qty),
            "uom": r.uom,
            "vendor_raw": r.vendor_raw
        }
        for r in rows
    ]

async def export_pl_to_df(session: AsyncSession, plan_date: date) -> pl.DataFrame:
    summary = await get_pl_summary(session, plan_date)
    if not summary:
        return pl.DataFrame()
    return pl.DataFrame(summary)
