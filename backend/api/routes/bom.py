from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Optional
from core.database import get_session
from core.deps import require_role
from services import bom_service
from schemas.bom import BomModelRead, BomTreeResponse, ReverseResult

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

@router.get("/models", response_model=List[BomModelRead])
async def get_models(
    search: Optional[str] = None,
    is_active: bool = True,
    session: AsyncSession = Depends(get_session)
):
    return await bom_service.list_models(session, search, is_active)

@router.get("/models/{model_code}", response_model=BomTreeResponse)
async def get_model_tree(
    model_code: str,
    session: AsyncSession = Depends(get_session)
):
    result = await bom_service.get_bom_tree(session, model_code)
    if not result:
        raise HTTPException(status_code=404, detail="Model not found")
    return result

@router.get("/reverse", response_model=ReverseResult)
async def reverse_lookup(
    part_number: str = Query(...),
    session: AsyncSession = Depends(get_session)
):
    return await bom_service.bom_reverse_lookup(session, part_number)
