from fastapi import APIRouter, Depends, HTTPException, Query, BackgroundTasks
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Optional
import json
from core.database import get_session, engine
from core.deps import get_current_user, require_role
from core.redis_client import get_redis
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

@router.post("/rebuild")
async def trigger_rebuild(
    background_tasks: BackgroundTasks,
    current_user: User = Depends(require_role("manager", "admin")),
    session: AsyncSession = Depends(get_session)
) -> dict:
    ok, reason = await item_master_service.should_rebuild(session)
    if not ok:
        return {"status": "skipped", "reason": reason}

    background_tasks.add_task(item_master_service.rebuild_from_bom_background, engine)
    return {"status": "started"}

@router.get("/rebuild/status")
async def get_rebuild_status() -> dict:
    redis = await get_redis()
    raw = await redis.get("itemmaster:rebuild_status")
    if not raw:
        return {"status": "idle", "progress": 0, "total": 0, "processed": 0,
                "started_at": None, "finished_at": None, "error": None}
    return json.loads(raw)

@router.get("/{item_id}", response_model=ItemMasterRead)
async def get_item(item_id: int, session: AsyncSession = Depends(get_session)):
    item = await item_master_service.get_item(session, item_id)
    if not item:
        raise HTTPException(status_code=404, detail="Item not found")
    return item_master_service._to_read(item)

@router.put("/{item_id}", response_model=ItemMasterRead)
async def update_item(item_id: int, data: ItemMasterUpdate, session: AsyncSession = Depends(get_session)):
    item = await item_master_service.update_item(session, item_id, data)
    if not item:
        raise HTTPException(status_code=404, detail="Item not found")
    return item

@router.get("/{item_id}/bom-usage", response_model=List[ItemBomUsage])
async def get_bom_usage(item_id: int, session: AsyncSession = Depends(get_session)):
    return await item_master_service.get_bom_usage(session, item_id)
