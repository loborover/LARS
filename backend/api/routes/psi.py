from fastapi import APIRouter, Depends, Query
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List
from datetime import date
from core.database import get_session
from core.deps import get_current_user, require_role
from models.user import User
from services import psi_service
from schemas.psi import PsiMatrixResponse, PsiCellRead, PsiCellUpdate, PsiShortageItem

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

@router.get("", response_model=PsiMatrixResponse)
async def get_psi_matrix(
    date_from: date = Query(...),
    date_to: date = Query(...),
    session: AsyncSession = Depends(get_session)
):
    return await psi_service.get_matrix(session, date_from, date_to)

@router.put("/{item_id}/{psi_date}", response_model=PsiCellRead)
async def update_psi_cell(
    item_id: int,
    psi_date: date,
    data: PsiCellUpdate,
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    record = await psi_service.update_cell(
        session, item_id, psi_date, data.available_qty, data.notes, current_user.id
    )
    
    avail = record.available_qty if record.available_qty is not None else 0.0
    shortage = avail - record.required_qty
    
    return PsiCellRead(
        required_qty=float(record.required_qty),
        available_qty=float(record.available_qty) if record.available_qty is not None else None,
        shortage_qty=float(shortage)
    )

@router.post("/recompute")
async def recompute_psi(
    dates: List[date],
    session: AsyncSession = Depends(get_session)
):
    count = await psi_service.recompute_required_for_dates(session, dates)
    return {"recomputed": count}

@router.get("/shortage", response_model=List[PsiShortageItem])
async def get_shortage(
    as_of_date: date = Query(...),
    session: AsyncSession = Depends(get_session)
):
    return await psi_service.get_shortage_summary(session, as_of_date)
