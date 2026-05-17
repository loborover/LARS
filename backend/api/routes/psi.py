from fastapi import APIRouter, Depends, Query, Body
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Optional
from datetime import date
from core.database import get_session
from core.deps import get_current_user, require_role
from models.user import User
from services import psi_service
from schemas.psi import (
    PsiMatrixResponse, PsiCellRead, PsiCellUpdate, PsiShortageItem,
    PsiRowFull, PsiFilterParams, PsiInventoryUpdate, PsiPickUpdate,
    PsiMatrixV2Response, PsiDailyRecordUpsert, InventoryPatch, PsiDailyRecordRead
)
from schemas.item_master import ItemMasterRead

router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

@router.get("", response_model=PsiMatrixResponse)
async def get_psi_matrix(
    date_from: date = Query(...),
    date_to: date = Query(...),
    session: AsyncSession = Depends(get_session)
):
    return await psi_service.get_matrix(session, date_from, date_to)

# --- Phase 5 New Endpoints ---

@router.get("/matrix", response_model=List[PsiRowFull])
async def get_psi_matrix_full(
    expeditor_user_id: Optional[int] = Query(None),
    supply_type: Optional[str] = Query(None),
    level: Optional[int] = Query(None),
    model_code: Optional[str] = Query(None),
    date_from: date = Query(default_factory=date.today),
    session: AsyncSession = Depends(get_session)
):
    params = PsiFilterParams(
        expeditor_user_id=expeditor_user_id,
        supply_type=supply_type,
        level=level,
        model_code=model_code,
        date_from=date_from
    )
    return await psi_service.build_psi_full_matrix(session, params)

@router.put("/item/{item_id}/inventory", response_model=ItemMasterRead)
async def update_item_inventory(
    item_id: int,
    data: PsiInventoryUpdate,
    session: AsyncSession = Depends(get_session)
):
    item = await psi_service.update_inventory(session, item_id, data.inventory_qty, data.defect_qty)
    return item

@router.patch("/item/{item_id}/pick", response_model=ItemMasterRead)
async def toggle_item_pick(
    item_id: int,
    data: PsiPickUpdate,
    session: AsyncSession = Depends(get_session)
):
    item = await psi_service.toggle_pick(session, item_id, data.is_picked)
    return item

@router.get("/models", response_model=List[str])
async def get_active_models(
    session: AsyncSession = Depends(get_session)
):
    return await psi_service.get_active_models(session)

@router.post("/advance-day")
async def advance_day(
    session: AsyncSession = Depends(get_session)
):
    return await psi_service.advance_day(session, date.today())

@router.post("/one-click")
async def one_click_solution(
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    return await psi_service.one_click_solution(session, current_user.id)

# --- Legacy / Others ---

@router.put("/{item_id}/{psi_date}", response_model=PsiCellRead)
async def update_psi_cell(
    item_id: int,
    psi_date: date,
    data: PsiCellUpdate,
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    record = await psi_service.update_cell(
        session, item_id, psi_date, data.available_qty, data.notes, current_user.id
    )
    
    avail = record.available_qty if record.available_qty is not None else 0.0
    shortage = avail - record.required_qty
    
    return PsiCellRead(
        required_qty=float(record.required_qty),
        available_qty=float(record.available_qty) if record.available_qty is not None else None,
        shortage_qty=float(shortage)
    )

@router.post("/recompute")
async def recompute_psi(
    dates: List[date],
    session: AsyncSession = Depends(get_session)
):
    count = await psi_service.recompute_required_for_dates(session, dates)
    return {"recomputed": count}

@router.get("/shortage", response_model=List[PsiShortageItem])
async def get_shortage(
    as_of_date: date = Query(...),
    session: AsyncSession = Depends(get_session)
):
    return await psi_service.get_shortage_summary(session, as_of_date)


# ─────────────────────────────────────────────────────────────────────
# PSI Matrix V2 (4행 블록)
# ─────────────────────────────────────────────────────────────────────

@router.get("/matrix-v2", response_model=PsiMatrixV2Response)
async def get_psi_matrix_v2(
    date_from: Optional[date] = None,
    days: int = Query(7, ge=1, le=60),
    expeditor_user_id: Optional[int] = None,
    supply_type: Optional[str] = None,
    vendor_code: Optional[str] = None,
    session: AsyncSession = Depends(get_session),
):
    """4행 블록 PSI 매트릭스. date_from 미입력 시 오늘 기준."""
    from datetime import date as date_type
    if date_from is None:
        date_from = date_type.today()

    data = await psi_service.build_psi_matrix_v2(
        session,
        date_from=date_from,
        days=days,
        expeditor_user_id=expeditor_user_id,
        supply_type=supply_type,
        vendor_code=vendor_code,
    )
    return data


# ─────────────────────────────────────────────────────────────────────
# 재고수량 직접 편집 (PSI 고정 컬럼)
# ─────────────────────────────────────────────────────────────────────

@router.patch("/items/{item_id}/inventory")
async def patch_inventory(
    item_id: int,
    body: InventoryPatch,
    session: AsyncSession = Depends(get_session),
):
    return await psi_service.patch_item_inventory(
        session, item_id, body.inventory_qty, body.defect_qty
    )


# ─────────────────────────────────────────────────────────────────────
# 입고/불량 기록 CRUD
# ─────────────────────────────────────────────────────────────────────

@router.get("/daily-records", response_model=List[PsiDailyRecordRead])
async def list_daily_records(
    part_number: Optional[str] = None,
    date_from: Optional[date] = None,
    date_to: Optional[date] = None,
    session: AsyncSession = Depends(get_session),
):
    return await psi_service.get_daily_records(session, part_number, date_from, date_to)


@router.put("/daily-records/upsert")
async def upsert_daily_record(
    body: PsiDailyRecordUpsert,
    current_user=Depends(require_role("internal", "manager", "admin")),
    session: AsyncSession = Depends(get_session),
):
    return await psi_service.upsert_daily_record(
        session,
        body.part_number,
        body.record_date,
        body.incoming_qty,
        body.defect_qty,
        body.note,
        current_user.id,
    )


@router.delete("/daily-records/{record_id}")
async def delete_daily_record(
    record_id: int,
    current_user=Depends(require_role("internal", "manager", "admin")),
    session: AsyncSession = Depends(get_session),
):
    from models.psi import PsiDailyRecord
    from fastapi import HTTPException
    stmt = select(PsiDailyRecord).where(PsiDailyRecord.id == record_id)
    res = await session.execute(stmt)
    rec = res.scalar_one_or_none()
    if not rec:
        raise HTTPException(status_code=404, detail="Record not found")
    await session.delete(rec)
    await session.commit()
    return {"status": "deleted"}
