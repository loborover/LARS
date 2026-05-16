import json
import re
from datetime import datetime, timezone
from typing import Optional, List, Dict, Any, Tuple
import polars as pl
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import sessionmaker
from sqlalchemy import func
from sqlmodel import select

from models.item_master import ItemMaster
from models.bom import BomItem, BomModel
from models.import_batch import ImportBatch
from schemas.item_master import ItemMasterCreate, ItemMasterUpdate, ItemMasterRead, ItemBomUsage
from core.redis_client import get_redis

_VENDOR_PATTERN = re.compile(r'^[A-Z]+_(.+)_KR\d+$')

def parse_vendor_name(vendor_raw: str | None) -> str | None:
    if not vendor_raw:
        return None
    m = _VENDOR_PATTERN.match(vendor_raw)
    return m.group(1) if m else vendor_raw

def _to_read(item: ItemMaster) -> ItemMasterRead:
    # SQLModel model_dump() or pydantic model_dump()
    data = item.model_dump()
    return ItemMasterRead(
        **data,
        vendor_name=parse_vendor_name(item.vendor_raw),
        lower_vendor_name=parse_vendor_name(item.lower_vendor_raw),
    )

async def _invalidate_item_cache():
    try:
        redis = await get_redis()
        await redis.delete("itemmaster:all")
    except Exception:
        pass

async def _db_list(session: AsyncSession, search: Optional[str] = None, is_active: bool = True) -> List[ItemMaster]:
    stmt = select(ItemMaster).where(ItemMaster.is_active == is_active)
    if search:
        from sqlalchemy import or_
        stmt = stmt.where(or_(
            ItemMaster.part_number.contains(search),
            ItemMaster.description.contains(search)
        ))
    res = await session.execute(stmt)
    return res.scalars().all()

async def list_items(session: AsyncSession, search: Optional[str] = None, is_active: bool = True) -> List[ItemMasterRead]:
    # inactive 요청은 캐시 bypass
    if not is_active:
        db_items = await _db_list(session, search=search, is_active=False)
        return [_to_read(i) for i in db_items]

    redis = await get_redis()
    cached = None
    try:
        cached = await redis.get("itemmaster:all")
    except Exception:
        pass

    all_items: List[ItemMasterRead] = []
    if cached:
        try:
            data = json.loads(cached)
            all_items = [ItemMasterRead(**d) for d in data]
        except Exception:
            cached = None

    if not cached:
        db_items = await _db_list(session, search=None, is_active=True)
        all_items = [_to_read(i) for i in db_items]
        try:
            await redis.setex("itemmaster:all", 300,
                              json.dumps([r.model_dump() for r in all_items]))
        except Exception:
            pass

    if search:
        q = search.lower()
        all_items = [r for r in all_items
                     if q in r.part_number.lower() or q in r.description.lower()]
    return all_items

async def get_item(session: AsyncSession, item_id: int) -> Optional[ItemMaster]:
    stmt = select(ItemMaster).where(ItemMaster.id == item_id)
    res = await session.execute(stmt)
    return res.scalar_one_or_none()

async def create_item(session: AsyncSession, data: ItemMasterCreate, user_id: int) -> ItemMasterRead:
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
    await _invalidate_item_cache()
    return _to_read(item)

async def update_item(session: AsyncSession, item_id: int, data: ItemMasterUpdate) -> Optional[ItemMasterRead]:
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
    await _invalidate_item_cache()
    return _to_read(item)

async def rebuild_from_bom(session: AsyncSession) -> int:
    """기존 동기 rebuild 로직 유지 (Import 파이프라인용)"""
    stmt = select(
        BomItem.part_number,
        BomItem.description,
        BomItem.vendor_raw,
        BomItem.level
    ).distinct(BomItem.part_number).where(
        BomItem.part_number != None,
        BomItem.part_number.notlike('%@CVZ.EKHQ%')
    )

    res = await session.execute(stmt)
    bom_items_data = res.all()

    im_stmt = select(ItemMaster)
    im_res = await session.execute(im_stmt)
    existing_items_dict = {item.part_number: item for item in im_res.scalars().all()}

    upserted_count = 0
    active_part_numbers = set()

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

    for pn, item in existing_items_dict.items():
        if pn not in active_part_numbers:
            item.is_active = False

    await session.commit()
    await _invalidate_item_cache()
    return upserted_count

async def should_rebuild(session: AsyncSession) -> Tuple[bool, str]:
    redis = await get_redis()
    status_raw = await redis.get("itemmaster:rebuild_status")
    if status_raw:
        status_data = json.loads(status_raw)
        if status_data.get("status") == "running":
            return False, "Rebuild already running"

    stmt = select(func.max(ImportBatch.finished_at)).where(
        ImportBatch.target_table == "bom",
        ImportBatch.status == "success"
    )
    res = await session.execute(stmt)
    latest_bom_import = res.scalar_one_or_none()

    if not latest_bom_import:
        return False, "No successful BOM import found"

    last_rebuild_str = await redis.get("itemmaster:last_rebuild_at")
    if not last_rebuild_str:
        return True, ""

    last_rebuild = datetime.fromisoformat(last_rebuild_str)
    if latest_bom_import.replace(tzinfo=timezone.utc) > last_rebuild.replace(tzinfo=timezone.utc):
        return True, ""
    return False, "ItemMaster already up-to-date"

async def rebuild_from_bom_background(engine):
    redis = await get_redis()
    AsyncSessionLocal = sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)

    await redis.set("itemmaster:rebuild_status", json.dumps({
        "status": "running", "progress": 0, "total": 0,
        "processed": 0, "started_at": datetime.utcnow().isoformat(), "finished_at": None, "error": None
    }))

    try:
        async with AsyncSessionLocal() as session:
            stmt = select(BomItem.part_number, BomItem.description, BomItem.vendor_raw, BomItem.level)\
                .distinct(BomItem.part_number).where(
                    BomItem.part_number != None,
                    BomItem.part_number.notlike('%@CVZ.EKHQ%')
                )
            res = await session.execute(stmt)
            bom_items_data = res.all()
            total = len(bom_items_data)

            im_res = await session.execute(select(ItemMaster))
            existing_dict = {i.part_number: i for i in im_res.scalars().all()}
            active_pns = set()
            processed = 0

            for row in bom_items_data:
                pn = row.part_number
                if not pn: continue
                active_pns.add(pn)

                item = existing_dict.get(pn)
                if item:
                    item.description = row.description or item.description
                    item.vendor_raw = row.vendor_raw or item.vendor_raw
                    item.level = row.level or item.level
                    item.is_active = True
                else:
                    session.add(ItemMaster(
                        part_number=pn,
                        description=row.description or "",
                        vendor_raw=row.vendor_raw,
                        level=row.level or 1,
                        is_active=True
                    ))

                processed += 1
                if processed % 50 == 0 or processed == total:
                    progress = int(processed / total * 100) if total > 0 else 100
                    await redis.set("itemmaster:rebuild_status", json.dumps({
                        "status": "running", "progress": progress, "total": total,
                        "processed": processed, "started_at": datetime.utcnow().isoformat(),
                        "finished_at": None, "error": None
                    }))

            for pn, item in existing_dict.items():
                if pn not in active_pns:
                    item.is_active = False

            await session.commit()

        now = datetime.utcnow().isoformat()
        await redis.set("itemmaster:rebuild_status", json.dumps({
            "status": "done", "progress": 100, "total": total,
            "processed": processed, "started_at": now, "finished_at": now, "error": None
        }))
        await redis.set("itemmaster:last_rebuild_at", now)
        await _invalidate_item_cache()

    except Exception as e:
        await redis.set("itemmaster:rebuild_status", json.dumps({
            "status": "failed", "progress": 0, "total": 0, "processed": 0,
            "started_at": datetime.utcnow().isoformat(), "finished_at": datetime.utcnow().isoformat(), "error": str(e)
        }))

async def get_bom_usage(session: AsyncSession, item_id: int) -> List[ItemBomUsage]:
    item = await get_item(session, item_id)
    if not item: return []

    stmt = (
        select(BomModel.model_code, BomModel.suffix, BomItem.description, BomItem.qty, BomItem.level, BomItem.path)
        .join(BomItem, BomModel.id == BomItem.model_id)
        .where(BomItem.part_number == item.part_number)
    )
    res = await session.execute(stmt)
    rows = res.all()
    if not rows: return []

    df = pl.DataFrame(
        [(f"{r.model_code}.{r.suffix}" if r.suffix else r.model_code,
          r.description, float(r.qty), r.level, r.path)
         for r in rows],
        schema=["model_number", "model_description", "qty", "level", "path"],
        orient="row"
    )
    grouped = (
        df.group_by("model_number")
        .agg([
            pl.col("qty").sum().alias("bom_qty"),
            pl.col("path").alias("paths"),
            pl.col("level").alias("levels"),
            pl.col("model_description").first().alias("model_description"),
        ])
        .sort("bom_qty", descending=True)
    )
    return [ItemBomUsage(**row) for row in grouped.to_dicts()]

async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """기존 _legacy_import_from_df를 public으로 전환 및 보강"""
    inserted = 0
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
    await _invalidate_item_cache()
    return inserted
