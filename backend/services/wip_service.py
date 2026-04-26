from typing import Optional, List
import polars as pl
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.wip import StandardWip, FactoryLocation
from models.item_master import ItemMaster

async def list_wip(session: AsyncSession, location_code: Optional[str] = None) -> List[dict]:
    stmt = select(StandardWip, FactoryLocation, ItemMaster).join(FactoryLocation).join(ItemMaster)
    if location_code:
        stmt = stmt.where(FactoryLocation.code == location_code)
        
    res = await session.execute(stmt)
    rows = res.all()
    
    result = []
    for wip, loc, item in rows:
        result.append({
            "location_code": loc.code,
            "item_part_number": item.part_number,
            "item_description": item.description,
            "target_qty": float(wip.target_qty),
            "safety_stock": float(wip.safety_stock) if wip.safety_stock else None
        })
    return result

async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    return 0

async def list_locations(session: AsyncSession) -> List[dict]:
    stmt = select(FactoryLocation)
    res = await session.execute(stmt)
    locs = res.scalars().all()
    return [{"id": l.id, "code": l.code, "name": l.name, "zone": l.zone} for l in locs]
