from typing import Optional, List
from datetime import date
import polars as pl
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.daily_plan import DailyPlan, DailyPlanLot, ProductionLine
from models.bom import BomModel, BomItem
from models.part_list import PartListSnapshot
from schemas.part_list import PartListItem, PartListResponse
from core.utils import parse_vendor_name

async def get_target_dp_batch_id() -> int | None:
    from core.redis_client import get_redis
    redis = await get_redis()
    raw = await redis.get("dp:target_batch_id")
    return int(raw) if raw else None

async def recompute_background(engine, dates: list, batch_id: int):
    """Background에서 PartList 재계산, Redis에 진행 상태 기록"""
    from sqlalchemy.orm import sessionmaker
    from sqlalchemy.ext.asyncio import AsyncSession
    from core.redis_client import get_redis
    from datetime import datetime
    import json

    redis = await get_redis()
    STATUS_KEY = "partlist:recompute_status"

    async def set_status(status, progress, processed, total, error=None):
        await redis.set(STATUS_KEY, json.dumps({
            "status": status,
            "progress": progress,
            "total": total,
            "processed": processed,
            "label": "소요자재 재계산",
            "started_at": datetime.utcnow().isoformat(),
            "finished_at": datetime.utcnow().isoformat() if status in ("done", "failed") else None,
            "error": error,
        }))

    await set_status("running", 0, 0, len(dates))

    try:
        AsyncSessionLocal = sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)
        async with AsyncSessionLocal() as session:
            total = len(dates)
            for i, d in enumerate(dates):
                await recompute_for_dates(session, [d], batch_id)
                progress = int((i + 1) / total * 100) if total > 0 else 100
                await set_status("running", progress, i + 1, total)

        await set_status("done", 100, len(dates), len(dates))
    except Exception as e:
        await set_status("failed", 0, 0, 0, error=str(e))

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
            
        # ① 모델의 path → qty 매핑 (level >= 0 인 항목만)
        path_to_qty: dict[str, float] = {
            item.path: float(item.qty)
            for item in bom_items
            if item.level >= 0
        }

        for b_item in bom_items:
            # level=0 (루트) 과 level=-1 (대체품) 제외
            if b_item.level <= 0:
                continue

            # ② 계층 누적 소요량 계산
            path_parts = b_item.path.split(".")
            accumulated = float(b_item.qty)
            # range(2, len(path_parts)) → 자신의 path에서 루트("0") 제외, 중간 조상만 순회
            for i in range(2, len(path_parts)):
                ancestor_path = ".".join(path_parts[:i])
                accumulated *= path_to_qty.get(ancestor_path, 1.0)

            req_qty = accumulated * float(lot.planned_qty)

            snap = PartListSnapshot(
                lot_id=lot.id,
                part_number=b_item.part_number,
                description=b_item.description,
                required_qty=req_qty,
                snapshot_date=plan_date,
                uom=b_item.uom or "EA",
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

async def get_pl_summary(
    session: AsyncSession,
    plan_date: date,
    line_code: Optional[str] = None,
    supply_type: Optional[str] = None,
    expeditor_user_id: Optional[int] = None,
) -> List[dict]:
    from sqlalchemy import func, text as sa_text
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

    batch_id = await get_target_dp_batch_id()
    if batch_id:
        stmt = stmt.where(DailyPlanLot.import_batch_id == batch_id)

    if line_code:
        stmt = stmt.join(ProductionLine, DailyPlan.line_id == ProductionLine.id)
        stmt = stmt.where(ProductionLine.code == line_code)

    if supply_type:
        stmt = stmt.where(
            PartListSnapshot.part_number.in_(
                select(BomItem.part_number).where(BomItem.supply_type == supply_type).distinct()
            )
        )

    if expeditor_user_id:
        stmt = stmt.where(
            PartListSnapshot.part_number.in_(
                _expeditor_part_subquery(expeditor_user_id)
            )
        )

    stmt = stmt.group_by(PartListSnapshot.part_number).order_by(func.sum(PartListSnapshot.required_qty).desc())

    res = await session.execute(stmt)
    rows = res.all()

    return [
        {
            "part_number": r.part_number,
            "description": r.description,
            "total_required_qty": float(r.total_required_qty),
            "uom": r.uom,
            "vendor_raw": parse_vendor_name(r.vendor_raw),
        }
        for r in rows
    ]


def _expeditor_part_subquery(user_id: int):
    """user_assignments(vendor) → vendors → item_master 의 part_number 서브쿼리"""
    from models.item_master import ItemMaster
    from models.vendor import Vendor
    from models.assignment import UserAssignment
    return (
        select(ItemMaster.part_number)
        .join(Vendor, ItemMaster.vendor_id == Vendor.id)
        .join(UserAssignment, (UserAssignment.resource_key == Vendor.code) & (UserAssignment.resource_type == "vendor"))
        .where(UserAssignment.user_id == user_id)
        .distinct()
    )

async def export_pl_to_df(session: AsyncSession, plan_date: date) -> pl.DataFrame:
    summary = await get_pl_summary(session, plan_date)
    if not summary:
        return pl.DataFrame()
    return pl.DataFrame(summary)

async def get_lot_view(
    session: AsyncSession,
    batch_id: int,
    line_code: Optional[str] = None,
    supply_type: Optional[str] = None,
    expeditor_user_id: Optional[int] = None,
) -> dict:
    """
    Lot View 피벗: rows = Lots, cols = part_numbers
    batch_id 기준 필터, total_qty 상위 300 품번만 컬럼으로 반환
    """
    from sqlalchemy import text
    import polars as pl

    extra_join = "JOIN production_lines pl_line ON pl_line.id = dp.line_id" if line_code else ""
    line_filter = "AND pl_line.code = :line_code" if line_code else ""
    supply_filter = (
        "AND EXISTS (SELECT 1 FROM bom_items bi WHERE bi.part_number = pls.part_number AND bi.supply_type = :supply_type)"
        if supply_type else ""
    )
    expeditor_filter = (
        """AND pls.part_number IN (
            SELECT im.part_number FROM item_master im
            JOIN vendors v ON v.id = im.vendor_id
            JOIN user_assignments ua ON ua.resource_key = v.code AND ua.resource_type = 'vendor'
            WHERE ua.user_id = :expeditor_user_id
        )"""
        if expeditor_user_id else ""
    )

    sql = text(f"""
        SELECT
            pls.lot_id,
            dpl.wo_number,
            dpl.model_code,
            dpl.suffix,
            dpl.planned_qty,
            dp.plan_date::date AS plan_date,
            pls.part_number,
            pls.required_qty,
            pls.description,
            pls.uom
        FROM part_list_snapshots pls
        JOIN daily_plan_lots dpl ON dpl.id = pls.lot_id
        JOIN daily_plans dp ON dp.id = dpl.plan_id
        {extra_join}
        WHERE dpl.import_batch_id = :batch_id
        {line_filter}
        {supply_filter}
        {expeditor_filter}
        ORDER BY dp.plan_date, dpl.sort_order, pls.part_number
    """)

    params: dict = {"batch_id": batch_id}
    if line_code:
        params["line_code"] = line_code
    if supply_type:
        params["supply_type"] = supply_type
    if expeditor_user_id:
        params["expeditor_user_id"] = expeditor_user_id

    res = await session.execute(sql, params)
    raw = res.fetchall()

    if not raw:
        return {"batch_id": batch_id, "part_columns": [], "part_meta": {}, "rows": []}

    df = pl.DataFrame(
        [dict(r._mapping) for r in raw],
        schema_overrides={"required_qty": pl.Float64, "planned_qty": pl.Int64}
    )

    # 상위 300 품번 (lot 무관 total 기준)
    top_parts = (
        df.group_by("part_number")
          .agg(pl.sum("required_qty").alias("total"))
          .sort("total", descending=True)
          .head(300)
          .get_column("part_number")
          .to_list()
    )

    df_filtered = df.filter(pl.col("part_number").is_in(top_parts))

    # part_meta: {part_number: {description, uom}}
    meta_df = (
        df_filtered
        .group_by("part_number")
        .agg([
            pl.first("description").alias("description"),
            pl.first("uom").alias("uom"),
        ])
    )
    part_meta = {
        r["part_number"]: {"description": r["description"], "uom": r["uom"] or "EA"}
        for r in meta_df.to_dicts()
    }

    # 피벗
    pivot = df_filtered.pivot(
        index=["lot_id", "wo_number", "model_code", "suffix", "planned_qty", "plan_date"],
        on="part_number",
        values="required_qty",
        aggregate_function="sum",
    )

    # 컬럼 순서를 top_parts 기준으로 정렬
    meta_cols = ["lot_id", "wo_number", "model_code", "suffix", "planned_qty", "plan_date"]
    existing_part_cols = [c for c in top_parts if c in pivot.columns]
    pivot = pivot.select(meta_cols + existing_part_cols)

    rows = []
    for r in pivot.to_dicts():
        model_number = f"{r['model_code']}.{r['suffix']}" if r.get('suffix') else r['model_code']
        parts = {c: (r[c] or 0.0) for c in existing_part_cols}
        rows.append({
            "lot_id": r["lot_id"],
            "wo_number": r.get("wo_number"),
            "model_number": model_number,
            "plan_date": str(r["plan_date"]),
            "planned_qty": r["planned_qty"],
            "parts": parts,
        })

    return {
        "batch_id": batch_id,
        "part_columns": existing_part_cols,
        "part_meta": part_meta,
        "rows": rows,
    }


async def get_psi_matrix(
    session: AsyncSession,
    batch_id: int,
    line_code: Optional[str] = None,
    supply_type: Optional[str] = None,
    expeditor_user_id: Optional[int] = None,
) -> dict:
    """
    PSI Matrix 피벗: rows = part_numbers, cols = dates
    total_qty 상위 200 품번, 날짜 오름차순
    """
    from sqlalchemy import text
    import polars as pl

    extra_join = "JOIN production_lines pl_line ON pl_line.id = dp.line_id" if line_code else ""
    line_filter = "AND pl_line.code = :line_code" if line_code else ""
    supply_filter = (
        "AND EXISTS (SELECT 1 FROM bom_items bi WHERE bi.part_number = pls.part_number AND bi.supply_type = :supply_type)"
        if supply_type else ""
    )
    expeditor_filter = (
        """AND pls.part_number IN (
            SELECT im.part_number FROM item_master im
            JOIN vendors v ON v.id = im.vendor_id
            JOIN user_assignments ua ON ua.resource_key = v.code AND ua.resource_type = 'vendor'
            WHERE ua.user_id = :expeditor_user_id
        )"""
        if expeditor_user_id else ""
    )

    sql = text(f"""
        SELECT
            pls.part_number,
            MAX(pls.description)       AS description,
            MAX(pls.vendor_raw)        AS vendor_raw,
            MAX(im.lower_vendor_raw)   AS lower_vendor_raw,
            MAX(pls.uom)               AS uom,
            dp.plan_date::date         AS plan_date,
            SUM(pls.required_qty)      AS day_qty
        FROM part_list_snapshots pls
        JOIN daily_plan_lots dpl ON dpl.id = pls.lot_id
        JOIN daily_plans dp ON dp.id = dpl.plan_id
        LEFT JOIN item_master im ON im.part_number = pls.part_number
        {extra_join}
        WHERE dpl.import_batch_id = :batch_id
        {line_filter}
        {supply_filter}
        {expeditor_filter}
        GROUP BY pls.part_number, dp.plan_date
        ORDER BY pls.part_number, dp.plan_date
    """)

    params: dict = {"batch_id": batch_id}
    if line_code:
        params["line_code"] = line_code
    if supply_type:
        params["supply_type"] = supply_type
    if expeditor_user_id:
        params["expeditor_user_id"] = expeditor_user_id

    res = await session.execute(sql, params)
    raw = res.fetchall()

    if not raw:
        return {"batch_id": batch_id, "date_columns": [], "rows": []}

    df = pl.DataFrame(
        [dict(r._mapping) for r in raw],
        schema_overrides={"day_qty": pl.Float64}
    )

    # 상위 200 품번
    top_parts = (
        df.group_by("part_number")
          .agg(pl.sum("day_qty").alias("total"))
          .sort("total", descending=True)
          .head(200)
          .get_column("part_number")
          .to_list()
    )

    df_filtered = df.filter(pl.col("part_number").is_in(top_parts))

    date_columns = sorted(df_filtered["plan_date"].unique().cast(pl.Utf8).to_list())

    # 피벗
    pivot = df_filtered.pivot(
        index=["part_number", "description", "vendor_raw", "lower_vendor_raw", "uom"],
        on="plan_date",
        values="day_qty",
        aggregate_function="sum",
    )

    # total 컬럼 추가
    date_cols_in_pivot = [c for c in date_columns if c in pivot.columns]
    pivot = pivot.with_columns(
        pl.sum_horizontal([pl.col(c).fill_null(0.0) for c in date_cols_in_pivot]).alias("total_qty")
    ).sort("total_qty", descending=True)

    rows = []
    for r in pivot.to_dicts():
        by_date = {d: (r.get(d) or 0.0) for d in date_cols_in_pivot}
        rows.append({
            "part_number": r["part_number"],
            "description": r.get("description"),
            "vendor_raw": parse_vendor_name(r.get("vendor_raw")),
            "lower_vendor_raw": parse_vendor_name(r.get("lower_vendor_raw")),
            "uom": r.get("uom") or "EA",
            "total_qty": r["total_qty"],
            "by_date": by_date,
        })

    return {
        "batch_id": batch_id,
        "date_columns": date_cols_in_pivot,
        "rows": rows,
    }


async def get_filter_options(session: AsyncSession) -> dict:
    """Expeditor / SupplyType / Line 필터 선택지 반환"""
    from models.user import User
    from models.assignment import UserAssignment

    # 1. Lines
    lines_res = await session.execute(select(ProductionLine.code).order_by(ProductionLine.code))
    lines = [r[0] for r in lines_res.all() if r[0]]

    # 2. SupplyTypes (BomItem에서 distinct)
    st_res = await session.execute(
        select(BomItem.supply_type)
        .where(BomItem.supply_type.isnot(None))
        .distinct()
        .order_by(BomItem.supply_type)
    )
    supply_types = [r[0] for r in st_res.all() if r[0]]

    # 3. Expeditors = vendor 배정이 있는 사용자
    exp_res = await session.execute(
        select(User.id, User.display_name)
        .join(UserAssignment, UserAssignment.user_id == User.id)
        .where(UserAssignment.resource_type == "vendor")
        .distinct()
        .order_by(User.display_name)
    )
    expeditors = [{"id": r[0], "name": r[1]} for r in exp_res.all()]

    return {"lines": lines, "supply_types": supply_types, "expeditors": expeditors}
