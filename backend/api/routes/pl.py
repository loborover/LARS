from fastapi import APIRouter, Depends, Query
from fastapi.responses import StreamingResponse
from sqlalchemy.ext.asyncio import AsyncSession
from typing import Optional, List
from datetime import date
from core.database import get_session
from core.deps import require_role
from services import part_list_service
from schemas.part_list import PartListResponse, PartListItem, LotViewResponse, PsiMatrixResponse
import io

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

@router.get("/filter-options")
async def get_filter_options(session: AsyncSession = Depends(get_session)):
    """Expeditor / SupplyType / Line 필터 선택지"""
    return await part_list_service.get_filter_options(session)


@router.get("", response_model=PartListResponse)
async def get_pl(
    plan_date: date = Query(...),
    line_code: Optional[str] = None,
    supply_type: Optional[str] = None,
    expeditor_user_id: Optional[int] = None,
    session: AsyncSession = Depends(get_session)
):
    items = await part_list_service.get_pl_summary(
        session, plan_date, line_code, supply_type, expeditor_user_id
    )
    return PartListResponse(
        plan_date=plan_date,
        line_code=line_code,
        items=[PartListItem(**i) for i in items],
        total_items=len(items)
    )

@router.post("/compute")
async def compute_pl(
    dates: List[date],
    batch_id: int = 0,
    session: AsyncSession = Depends(get_session)
):
    count = await part_list_service.recompute_for_dates(session, dates, batch_id)
    return {"computed": count}

@router.get("/export")
async def export_pl(
    plan_date: date = Query(...),
    session: AsyncSession = Depends(get_session)
):
    df = await part_list_service.export_pl_to_df(session, plan_date)
    buffer = io.BytesIO()
    df.write_excel(buffer)
    buffer.seek(0)
    
    headers = {
        "Content-Disposition": f"attachment; filename=PL_{plan_date}.xlsx"
    }
    return StreamingResponse(
        buffer, 
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers=headers
    )

@router.get("/lot-view", response_model=LotViewResponse)
async def get_lot_view(
    batch_id: Optional[int] = None,
    line_code: Optional[str] = None,
    supply_type: Optional[str] = None,
    expeditor_user_id: Optional[int] = None,
    session: AsyncSession = Depends(get_session)
):
    """Lot 행 × 품번 열 피벗 — batch_id 미지정 시 Redis Target 사용"""
    if batch_id is None:
        batch_id = await part_list_service.get_target_dp_batch_id()
    if not batch_id:
        return LotViewResponse(batch_id=0, part_columns=[], rows=[])
    data = await part_list_service.get_lot_view(session, batch_id, line_code, supply_type, expeditor_user_id)
    return LotViewResponse(**data)


@router.get("/psi-matrix", response_model=PsiMatrixResponse)
async def get_psi_matrix(
    batch_id: Optional[int] = None,
    line_code: Optional[str] = None,
    supply_type: Optional[str] = None,
    expeditor_user_id: Optional[int] = None,
    session: AsyncSession = Depends(get_session)
):
    """품번 행 × 날짜 열 피벗 — batch_id 미지정 시 Redis Target 사용"""
    if batch_id is None:
        batch_id = await part_list_service.get_target_dp_batch_id()
    if not batch_id:
        return PsiMatrixResponse(batch_id=0, date_columns=[], rows=[])
    data = await part_list_service.get_psi_matrix(session, batch_id, line_code, supply_type, expeditor_user_id)
    return PsiMatrixResponse(**data)
