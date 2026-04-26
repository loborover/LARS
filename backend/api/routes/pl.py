from fastapi import APIRouter, Depends, Query
from fastapi.responses import StreamingResponse
from sqlalchemy.ext.asyncio import AsyncSession
from typing import Optional, List
from datetime import date
from core.database import get_session
from core.deps import require_role
from services import part_list_service
from schemas.part_list import PartListResponse, PartListItem
import io

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

@router.get("", response_model=PartListResponse)
async def get_pl(
    plan_date: date = Query(...),
    line_code: Optional[str] = None,
    session: AsyncSession = Depends(get_session)
):
    items = await part_list_service.get_pl_summary(session, plan_date, line_code)
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
