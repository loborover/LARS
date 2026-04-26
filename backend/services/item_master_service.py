from typing import Optional, List
import polars as pl
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.item_master import ItemMaster
from models.bom import BomItem, BomModel
from schemas.item_master import ItemMasterCreate, ItemMasterUpdate

async def list_items(session: AsyncSession, search: Optional[str] = None, is_active: bool = True) -> List[ItemMaster]:
    stmt = select(ItemMaster).where(ItemMaster.is_active == is_active)
    if search:
        # Assuming search is a substring of part_number or description
        from sqlalchemy import or_
        stmt = stmt.where(or_(
            ItemMaster.part_number.contains(search),
            ItemMaster.description.contains(search)
        ))
    res = await session.execute(stmt)
    return res.scalars().all()

async def get_item(session: AsyncSession, item_id: int) -> Optional[ItemMaster]:
    stmt = select(ItemMaster).where(ItemMaster.id == item_id)
    res = await session.execute(stmt)
    return res.scalar_one_or_none()

async def create_item(session: AsyncSession, data: ItemMasterCreate, user_id: int) -> ItemMaster:
    item = ItemMaster(
        level=data.level,
        description=data.description,
        part_number=data.part_number,
        vendor_raw=data.vendor_raw,
        tracking_user_id=user_id
    )
    session.add(item)
    await session.commit()
    await session.refresh(item)
    return item

async def update_item(session: AsyncSession, item_id: int, data: ItemMasterUpdate) -> Optional[ItemMaster]:
    item = await get_item(session, item_id)
    if not item:
        return None
    if data.description is not None:
        item.description = data.description
    if data.vendor_raw is not None:
        item.vendor_raw = data.vendor_raw
    if data.is_active is not None:
        item.is_active = data.is_active
        
    await session.commit()
    await session.refresh(item)
    return item

async def rebuild_from_bom(session: AsyncSession) -> int:
    """
    bom_items 테이블에서 part_number를 key로 중복 제거하여
    item_master 테이블을 갱신(upsert)한다.

    merge 기준:
    - part_number (UNIQUE KEY)
    - description: bom_items에서 해당 part_number의 첫 번째 값
    - vendor_raw: bom_items에서 해당 part_number의 첫 번째 값
    - level: bom_items에서 해당 part_number의 첫 번째 값

    기존 item_master에 존재하는 part_number는 UPDATE,
    새로운 part_number는 INSERT한다.
    bom_items에 더 이상 존재하지 않는 part_number는 is_active = FALSE 처리한다.
    """
    # Get unique part_numbers from bom_items
    stmt = select(
        BomItem.part_number,
        BomItem.description,
        BomItem.vendor_raw,
        BomItem.level
    ).distinct(BomItem.part_number).where(BomItem.part_number != None)

    res = await session.execute(stmt)
    bom_items_data = res.all()

    # Get existing item_masters
    im_stmt = select(ItemMaster)
    im_res = await session.execute(im_stmt)
    existing_items = im_res.scalars().all()
    existing_items_dict = {item.part_number: item for item in existing_items}

    upserted_count = 0
    active_part_numbers = set()

    # Upsert from bom_items
    for row in bom_items_data:
        pn = row.part_number
        if not pn: continue

        active_part_numbers.add(pn)
        item = existing_items_dict.get(pn)

        if item:
            item.description = row.description or item.description
            item.vendor_raw = row.vendor_raw or item.vendor_raw
            item.level = row.level or item.level
            item.is_active = True
        else:
            item = ItemMaster(
                part_number=pn,
                description=row.description or "",
                vendor_raw=row.vendor_raw,
                level=row.level or 1,
                is_active=True
            )
            session.add(item)
        upserted_count += 1

    # Deactivate items that are not in bom_items anymore
    for pn, item in existing_items_dict.items():
        if pn not in active_part_numbers:
            item.is_active = False

    await session.commit()
    return upserted_count

async def _legacy_import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    inserted = 0
    # Expected columns: level, description, part_number, vendor_raw
    for row in df.iter_rows(named=True):
        pn = row.get("part_number")
        if not pn: continue
        
        stmt = select(ItemMaster).where(ItemMaster.part_number == pn)
        res = await session.execute(stmt)
        item = res.scalar_one_or_none()
        
        if item:
            item.description = row.get("description", item.description)
            item.level = row.get("level", item.level)
            item.vendor_raw = row.get("vendor_raw", item.vendor_raw)
            item.import_batch_id = batch_id
        else:
            item = ItemMaster(
                part_number=pn,
                description=row.get("description", ""),
                level=row.get("level", 1),
                vendor_raw=row.get("vendor_raw"),
                import_batch_id=batch_id
            )
            session.add(item)
        inserted += 1
        
    await session.commit()
    return inserted

async def get_bom_usage(session: AsyncSession, item_id: int) -> List[dict]:
    item = await get_item(session, item_id)
    if not item:
        return []
        
    stmt = (
        select(BomModel.model_code, BomItem.description, BomItem.qty, BomItem.level, BomItem.path)
        .join(BomItem, BomModel.id == BomItem.model_id)
        .where(BomItem.part_number == item.part_number)
    )
    res = await session.execute(stmt)
    rows = res.all()
    
    return [
        {
            "model_code": r.model_code,
            "description": r.description,
            "qty": float(r.qty),
            "level": r.level,
            "path": r.path
        } for r in rows
    ]
