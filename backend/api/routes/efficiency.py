from fastapi import APIRouter, Depends, UploadFile, File
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Optional
from datetime import date
import polars as pl
import io
from core.database import get_session
from core.deps import require_role
from services import efficiency_service

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

@router.get("")
async def get_efficiency(
    date_from: Optional[date] = None,
    date_to: Optional[date] = None,
    session: AsyncSession = Depends(get_session)
):
    return await efficiency_service.list_efficiency(session, date_from, date_to)

@router.post("/import")
async def import_efficiency(file: UploadFile = File(...), session: AsyncSession = Depends(get_session)):
    import fastexcel
    content = await file.read()
    buffer = io.BytesIO(content)
    df = fastexcel.read_excel(buffer).load_sheet(0).to_polars()
    inserted = await efficiency_service.import_from_df(session, df, 0)
    return {"imported": inserted}
