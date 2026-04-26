from typing import Optional, List
from datetime import date
import polars as pl
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.daily_plan import DailyPlan, DailyPlanLot, ProductionLine
from models.bom import BomModel

async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """
    DP DataFrame을 DB에 저장.
    Returns: 삽입된 DailyPlanLot 수
    """
    if df.height == 0:
        return 0

    total_inserted = 0

    # Process per (plan_date, line_code)
    # The parser gives us these columns. Excel: plan_date is uniform or per row? 
    # Our df should have plan_date per row from parser_csv/excel output.
    for (plan_date, line_code), group_df in df.group_by(["plan_date", "line_code"]):
        # 1. Get or create ProductionLine
        stmt = select(ProductionLine).where(ProductionLine.code == line_code)
        res = await session.execute(stmt)
        line = res.scalar_one_or_none()
        if not line:
            line = ProductionLine(code=line_code, name=line_code)
            session.add(line)
            await session.flush()
            await session.refresh(line)
            
        # 2. Get or create DailyPlan
        stmt = select(DailyPlan).where(DailyPlan.plan_date == plan_date, DailyPlan.line_id == line.id)
        res = await session.execute(stmt)
        plan = res.scalar_one_or_none()
        
        if not plan:
            plan = DailyPlan(plan_date=plan_date, line_id=line.id, import_batch_id=batch_id)
            session.add(plan)
            await session.flush()
            await session.refresh(plan)
        else:
            plan.import_batch_id = batch_id
            
        # 3. Delete existing DailyPlanLots
        from sqlalchemy import delete
        await session.execute(delete(DailyPlanLot).where(DailyPlanLot.plan_id == plan.id))
        
        # Insert lots
        lots_to_add = []
        for row in group_df.iter_rows(named=True):
            # Resolve model_id if BOM exists
            stmt = select(BomModel).where(BomModel.model_code == row["model_code"])
            res = await session.execute(stmt)
            bom_model = res.scalar_one_or_none()
            
            lot = DailyPlanLot(
                plan_id=plan.id,
                wo_number=row.get("wo_number"),
                model_id=bom_model.id if bom_model else None,
                model_code=row["model_code"],
                lot_number=row.get("lot_number", "N/A"),
                planned_qty=row["planned_qty"],
                input_qty=row.get("input_qty", 0),
                output_qty=row.get("output_qty", 0),
                planned_start=row.get("planned_start"),
                sort_order=row.get("sort_order", 0),
                daily_qty_json=row.get("daily_qty_json", "{}"),
                import_batch_id=batch_id
            )
            lots_to_add.append(lot)
            
        session.add_all(lots_to_add)
        total_inserted += len(lots_to_add)
        
    await session.commit()
    return total_inserted

async def list_plans(
    session: AsyncSession,
    date_from: Optional[date] = None,
    date_to: Optional[date] = None,
    line_code: Optional[str] = None
) -> List[dict]:
    """
    DailyPlan + ProductionLine 조인 조회.
    반환: [{plan_id, plan_date, line_code, line_name, lot_count, import_batch_id}]
    """
    stmt = select(DailyPlan, ProductionLine).join(ProductionLine)
    
    if date_from:
        stmt = stmt.where(DailyPlan.plan_date >= date_from)
    if date_to:
        stmt = stmt.where(DailyPlan.plan_date <= date_to)
    if line_code:
        stmt = stmt.where(ProductionLine.code == line_code)
        
    res = await session.execute(stmt)
    rows = res.all()
    
    result = []
    for plan, line in rows:
        # Count lots
        count_stmt = select(pl.col(DailyPlanLot.id).count()).where(DailyPlanLot.plan_id == plan.id)
        # Using sqlmodel func.count
        from sqlalchemy import func
        count_stmt = select(func.count(DailyPlanLot.id)).where(DailyPlanLot.plan_id == plan.id)
        c_res = await session.execute(count_stmt)
        lot_count = c_res.scalar_one()
        
        result.append({
            "plan_id": plan.id,
            "plan_date": plan.plan_date.date() if hasattr(plan.plan_date, 'date') else plan.plan_date,
            "line_code": line.code,
            "line_name": line.name,
            "lot_count": lot_count,
            "import_batch_id": plan.import_batch_id
        })
    return result

async def get_lots_by_plan(session: AsyncSession, plan_id: int) -> List[DailyPlanLot]:
    stmt = select(DailyPlanLot).where(DailyPlanLot.plan_id == plan_id).order_by(DailyPlanLot.sort_order)
    res = await session.execute(stmt)
    return res.scalars().all()

async def get_dates_in_df(df: pl.DataFrame) -> List[date]:
    """DataFrame에서 unique plan_date 목록 추출 (PSI 재계산 트리거용)"""
    if "plan_date" not in df.columns:
        return []
    # plan_date could be Date type in Polars
    unique_dates = df["plan_date"].unique().to_list()
    # convert to python datetime.date
    return [d if isinstance(d, date) else d.date() for d in unique_dates if d is not None]
