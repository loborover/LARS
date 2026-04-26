from fastapi import APIRouter, Depends, HTTPException, UploadFile, File, Query
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Optional
import polars as pl
import io
from core.database import get_session
from core.deps import get_current_user, require_role
from models.user import User
from services import item_master_service
from schemas.item_master import ItemMasterRead, ItemMasterCreate, ItemMasterUpdate, ItemBomUsage

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

@router.get("", response_model=List[ItemMasterRead])
async def list_items(
    search: Optional[str] = None,
    is_active: bool = True,
    session: AsyncSession = Depends(get_session)
):
    return await item_master_service.list_items(session, search, is_active)

@router.post("", response_model=ItemMasterRead)
async def create_item(
    data: ItemMasterCreate,
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    return await item_master_service.create_item(session, data, current_user.id)

@router.get("/{item_id}", response_model=ItemMasterRead)
async def get_item(item_id: int, session: AsyncSession = Depends(get_session)):
    item = await item_master_service.get_item(session, item_id)
    if not item:
        raise HTTPException(status_code=404, detail="Item not found")
    return item

@router.put("/{item_id}", response_model=ItemMasterRead)
async def update_item(item_id: int, data: ItemMasterUpdate, session: AsyncSession = Depends(get_session)):
    item = await item_master_service.update_item(session, item_id, data)
    if not item:
        raise HTTPException(status_code=404, detail="Item not found")
    return item

@router.get("/{item_id}/bom-usage", response_model=List[ItemBomUsage])
async def get_bom_usage(item_id: int, session: AsyncSession = Depends(get_session)):
    return await item_master_service.get_bom_usage(session, item_id)

@router.post("/import")
async def import_items(file: UploadFile = File(...), session: AsyncSession = Depends(get_session)):
    import fastexcel
    content = await file.read()
    try:
        buffer = io.BytesIO(content)
        doc = fastexcel.read_excel(buffer)
        df = doc.load_sheet(0).to_polars()
        
        # Mapping: Level→level, 품명→description, 품번→part_number, 업체→vendor_raw
        col_map = {}
        for c in df.columns:
            cl = c.strip().lower()
            if cl == "level": col_map[c] = "level"
            elif cl == "품명" or cl == "description": col_map[c] = "description"
            elif cl == "품번" or cl == "part_number" or cl == "part no": col_map[c] = "part_number"
            elif cl == "업체" or cl == "vendor": col_map[c] = "vendor_raw"
            
        df = df.rename(col_map)
        if "part_number" not in df.columns or "description" not in df.columns:
            raise HTTPException(status_code=400, detail="Missing required columns (part_number, description)")
            
        # Ensure correct types
        df = df.with_columns([
            pl.col("part_number").cast(pl.Utf8),
            pl.col("description").cast(pl.Utf8),
            pl.col("level").cast(pl.Int32, strict=False).fill_null(1) if "level" in df.columns else pl.lit(1).alias("level"),
            pl.col("vendor_raw").cast(pl.Utf8) if "vendor_raw" in df.columns else pl.lit(None).alias("vendor_raw")
        ])
        
        # Batch id would come from import pipeline, for standalone import we can use 0
        inserted = await item_master_service.import_from_df(session, df, 0)
        return {"imported": inserted}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))
