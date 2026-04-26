from fastapi import APIRouter, Depends, UploadFile, File
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Optional
import polars as pl
import io
from core.database import get_session
from core.deps import require_role
from services import wip_service

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

@router.get("")
async def get_wip(
    location_code: Optional[str] = None,
    session: AsyncSession = Depends(get_session)
):
    return await wip_service.list_wip(session, location_code)

@router.get("/locations")
async def get_locations(session: AsyncSession = Depends(get_session)):
    return await wip_service.list_locations(session)

@router.post("/import")
async def import_wip(file: UploadFile = File(...), session: AsyncSession = Depends(get_session)):
    import fastexcel
    content = await file.read()
    buffer = io.BytesIO(content)
    df = fastexcel.read_excel(buffer).load_sheet(0).to_polars()
    inserted = await wip_service.import_from_df(session, df, 0)
    return {"imported": inserted}
