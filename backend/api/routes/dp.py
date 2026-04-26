from fastapi import APIRouter, Depends, Query
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Optional
from datetime import date
from core.database import get_session
from core.deps import require_role
from services import daily_plan_service
from schemas.daily_plan import DailyPlanRead, DailyPlanLotRead

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

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
