from typing import Optional, List, Dict, Any
from datetime import date, timedelta
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import func
from sqlmodel import select
import polars as pl

from models.psi import PsiRecord
from models.item_master import ItemMaster
from models.part_list import PartListSnapshot
from models.user import User
from models.bom import BomItem
from schemas.psi import PsiFilterParams, PsiRowFull, DateHeader

async def get_target_dp_batch_id() -> int | None:
    from core.redis_client import get_redis
    redis = await get_redis()
    raw = await redis.get("dp:target_batch_id")
    return int(raw) if raw else None

async def recompute_all_background(engine):
    """Background에서 PSI 전체 재계산, Redis에 진행 상태 기록"""
    from sqlalchemy.orm import sessionmaker
    from sqlalchemy.ext.asyncio import AsyncSession
    from core.redis_client import get_redis
    from datetime import datetime
    import json

    redis = await get_redis()
    STATUS_KEY = "psi:recompute_status"

    async def set_status(status, progress, processed=0, total=0, error=None):
        await redis.set(STATUS_KEY, json.dumps({
            "status": status,
            "progress": progress,
            "total": total,
            "processed": processed,
            "label": "PSI 재계산",
            "started_at": datetime.utcnow().isoformat(),
            "finished_at": datetime.utcnow().isoformat() if status in ("done", "failed") else None,
            "error": error,
        }))

    await set_status("running", 0)

    try:
        AsyncSessionLocal = sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)
        async with AsyncSessionLocal() as session:
            # 기존 recompute_all 로직 실행
            await set_status("running", 30)
            await recompute_all(session)  # 기존 동기 함수 재사용
            await set_status("done", 100, total=1, processed=1)
    except Exception as e:
        await set_status("failed", 0, error=str(e))

async def recompute_required_for_dates(session: AsyncSession, dates: List[date]) -> int:
    if not dates:
        return 0

    # 해당 날짜들의 required_qty 합계 계산 (part_list_snapshots)
    stmt = (
        select(
            PartListSnapshot.snapshot_date,
            PartListSnapshot.part_number,
            func.sum(PartListSnapshot.required_qty).label("req_qty")
        )
        .where(PartListSnapshot.snapshot_date.in_(dates))
        .group_by(PartListSnapshot.snapshot_date, PartListSnapshot.part_number)
    )
    res = await session.execute(stmt)
    req_data = res.all()
    
    # IT 품목에 있는 part_number 목록 가져오기
    stmt_items = select(ItemMaster.id, ItemMaster.part_number).where(ItemMaster.is_active == True)
    res_items = await session.execute(stmt_items)
    item_map = {part_no: i_id for i_id, part_no in res_items.all()}
    
    if not item_map:
        return 0
        
    upserted = 0
    # Group required qtys
    req_dict = {} # (item_id, date) -> qty
    for snapshot_date, part_number, req_qty in req_data:
        if part_number in item_map:
            item_id = item_map[part_number]
            s_date = snapshot_date.date() if hasattr(snapshot_date, 'date') else snapshot_date
            req_dict[(item_id, s_date)] = float(req_qty)
            
    # Process PSI records
    for (item_id, p_date), qty in req_dict.items():
        stmt = select(PsiRecord).where(PsiRecord.item_id == item_id, PsiRecord.psi_date == p_date)
        res = await session.execute(stmt)
        record = res.scalar_one_or_none()
        
        if record:
            record.required_qty = qty
        else:
            record = PsiRecord(
                item_id=item_id,
                psi_date=p_date,
                required_qty=qty,
                available_qty=None
            )
            session.add(record)
        upserted += 1
        
    await session.commit()
    return upserted

async def get_matrix(session: AsyncSession, date_from: date, date_to: date, item_ids: Optional[List[int]] = None) -> dict:
    # (Existing method kept for backward compatibility)
    dates = []
    curr = date_from
    while curr <= date_to:
        dates.append(curr.isoformat())
        curr += timedelta(days=1)
        
    stmt_items = select(ItemMaster).where(ItemMaster.is_active == True)
    if item_ids:
        stmt_items = stmt_items.where(ItemMaster.id.in_(item_ids))
    res_items = await session.execute(stmt_items)
    items = res_items.scalars().all()
    
    item_list = [{"id": i.id, "part_number": i.part_number, "description": i.description} for i in items]
    actual_item_ids = [i.id for i in items]
    
    if not actual_item_ids:
        return {"dates": dates, "items": [], "cells": {}}
        
    stmt_cells = select(PsiRecord).where(PsiRecord.psi_date >= date_from, PsiRecord.psi_date <= date_to, PsiRecord.item_id.in_(actual_item_ids))
    res_cells = await session.execute(stmt_cells)
    cells_db = res_cells.scalars().all()
    
    cells_res = {}
    for c in cells_db:
        key = f"{c.item_id}_{c.psi_date.isoformat()}"
        avail = c.available_qty if c.available_qty is not None else 0.0
        shortage = avail - c.required_qty
        cells_res[key] = {
            "required_qty": float(c.required_qty),
            "available_qty": float(c.available_qty) if c.available_qty is not None else None,
            "shortage_qty": float(shortage)
        }
        
    return {
        "dates": dates,
        "items": item_list,
        "cells": cells_res
    }

async def build_psi_full_matrix(session: AsyncSession, params: PsiFilterParams) -> List[PsiRowFull]:
    date_from = params.date_from
    date_to = date_from + timedelta(days=30)
    date_list = [date_from + timedelta(days=i) for i in range(31)]
    
    date_headers = [
        DateHeader(
            label=f"D+{(d - date_from).days}",
            date=d.isoformat(),
            week=d.isocalendar()[1]
        )
        for d in date_list
    ]
    
    # 1. Base Items Query
    stmt = select(ItemMaster, User.display_name).outerjoin(User, ItemMaster.tracking_user_id == User.id).where(ItemMaster.is_active == True)
    if params.expeditor_user_id:
        stmt = stmt.where(ItemMaster.tracking_user_id == params.expeditor_user_id)
    if params.level:
        stmt = stmt.where(ItemMaster.level == params.level)
        
    res = await session.execute(stmt)
    items_raw = res.all()
    if not items_raw:
        return []
        
    part_numbers = [it[0].part_number for it in items_raw]
    
    # 2. Fetch BOM Metadata (SupplyType, TechSpec)
    # Get latest metadata for each part number
    bom_stmt = select(BomItem).where(BomItem.part_number.in_(part_numbers)).order_by(BomItem.part_number, BomItem.created_at.desc())
    bom_res = await session.execute(bom_stmt)
    bom_items = bom_res.scalars().all()
    
    bom_info_map = {}
    for bi in bom_items:
        if bi.part_number not in bom_info_map:
            bom_info_map[bi.part_number] = {
                "supply_type": bi.supply_type,
                "tech_spec": bi.description,
                "uom": bi.uom
            }
            
    # Filter by supply_type if needed
    final_items = []
    for im, exp_name in items_raw:
        info = bom_info_map.get(im.part_number, {})
        st = info.get("supply_type")
        if params.supply_type and st != params.supply_type:
            continue
        final_items.append((im, exp_name, info))
        
    if not final_items:
        return []

    # 3. Fetch Daily Demand using Polars
    # Snapshot query
    snapshot_stmt = select(PartListSnapshot.part_number, PartListSnapshot.snapshot_date, PartListSnapshot.required_qty)
    if params.model_code:
        from models.daily_plan import DailyPlanLot
        # model_code 파라미터는 "Model.Suffix" 또는 "Model" 형식 모두 허용
        if "." in params.model_code:
            _mc, _sf = params.model_code.split(".", 1)
            snapshot_stmt = snapshot_stmt.join(DailyPlanLot, PartListSnapshot.lot_id == DailyPlanLot.id).where(
                DailyPlanLot.model_code == _mc,
                DailyPlanLot.suffix == _sf
            )
        else:
            snapshot_stmt = snapshot_stmt.join(DailyPlanLot, PartListSnapshot.lot_id == DailyPlanLot.id).where(DailyPlanLot.model_code == params.model_code)
        
    snapshot_stmt = snapshot_stmt.where(
        PartListSnapshot.snapshot_date >= date_from,
        PartListSnapshot.snapshot_date <= date_to,
        PartListSnapshot.part_number.in_([it[0].part_number for it in final_items])
    )
    
    snap_res = await session.execute(snapshot_stmt)
    snap_data = snap_res.all()
    
    demand_map = {} # part_number -> { "D+0": qty, ... }
    
    if snap_data:
        # snap_data is List[tuple] -> convert to Polars
        df = pl.DataFrame([
            {"pn": r[0], "dt": r[1], "qty": r[2]} for r in snap_data
        ])
        
        # Group by pn and dt
        df_agg = df.group_by(["pn", "dt"]).agg(pl.sum("qty").alias("total_qty"))
        
        # Build demand map
        for row in df_agg.to_dicts():
            pn = row["pn"]
            dt = row["dt"]
            qty = row["total_qty"]
            day_label = f"D+{(dt - date_from).days}"
            
            if pn not in demand_map:
                demand_map[pn] = {}
            demand_map[pn][day_label] = float(qty)

    # 4. Construct Response
    results = []
    for im, exp_name, info in final_items:
        pn = im.part_number
        results.append(PsiRowFull(
            item_id=im.id,
            part_number=pn,
            description=im.description,
            level=im.level,
            supply_type=info.get("supply_type"),
            uom=info.get("uom", "EA"),
            vendor_raw=im.vendor_raw,
            lower_vendor_raw=im.lower_vendor_raw,
            tech_spec=info.get("tech_spec"),
            inventory_qty=float(im.inventory_qty),
            defect_qty=float(im.defect_qty),
            is_picked=im.is_picked,
            daily_demand=demand_map.get(pn, {}),
            date_headers=date_headers,
            expeditor_name=exp_name
        ))
        
    return results

async def update_inventory(session: AsyncSession, item_id: int, inventory_qty: float, defect_qty: float) -> ItemMaster:
    stmt = select(ItemMaster).where(ItemMaster.id == item_id)
    res = await session.execute(stmt)
    item = res.scalar_one()
    item.inventory_qty = inventory_qty
    item.defect_qty = defect_qty
    await session.commit()
    await session.refresh(item)
    return item

async def toggle_pick(session: AsyncSession, item_id: int, is_picked: bool) -> ItemMaster:
    stmt = select(ItemMaster).where(ItemMaster.id == item_id)
    res = await session.execute(stmt)
    item = res.scalar_one()
    item.is_picked = is_picked
    await session.commit()
    await session.refresh(item)
    return item

async def get_active_models(session: AsyncSession) -> List[str]:
    from models.daily_plan import DailyPlanLot
    stmt = select(DailyPlanLot.model_code, DailyPlanLot.suffix).distinct()

    # [Phase 10] Use target DP batch if set
    batch_id = await get_target_dp_batch_id()
    if batch_id:
        stmt = stmt.where(DailyPlanLot.import_batch_id == batch_id)

    res = await session.execute(stmt)
    result = []
    for model_code, suffix in res.all():
        if suffix:
            result.append(f"{model_code}.{suffix}")
        else:
            result.append(model_code)
    return sorted(set(result))

async def update_cell(session: AsyncSession, item_id: int, psi_date: date, available_qty: float, notes: Optional[str], user_id: int) -> PsiRecord:
    stmt = select(PsiRecord).where(PsiRecord.item_id == item_id, PsiRecord.psi_date == psi_date)
    res = await session.execute(stmt)
    record = res.scalar_one_or_none()
    
    if not record:
        record = PsiRecord(
            item_id=item_id,
            psi_date=psi_date,
            required_qty=0.0,
            available_qty=available_qty,
            notes=notes,
            last_updated_by=user_id
        )
        session.add(record)
    else:
        record.available_qty = available_qty
        if notes is not None:
            record.notes = notes
        record.last_updated_by = user_id
        
    await session.commit()
    await session.refresh(record)
    return record

async def get_shortage_summary(session: AsyncSession, as_of_date: date) -> List[dict]:
    stmt = select(PsiRecord, ItemMaster).join(ItemMaster).where(PsiRecord.psi_date == as_of_date)
    res = await session.execute(stmt)
    rows = res.all()
    
    results = []
    for rec, item in rows:
        avail = rec.available_qty if rec.available_qty is not None else 0.0
        shortage = avail - rec.required_qty
        if shortage < 0:
            results.append({
                "item_id": item.id,
                "part_number": item.part_number,
                "description": item.description,
                "psi_date": rec.psi_date,
                "required_qty": float(rec.required_qty),
                "available_qty": float(rec.available_qty) if rec.available_qty is not None else None,
                "shortage_qty": float(shortage)
            })
            
    return results

async def advance_day(session: AsyncSession, today: date) -> Dict[str, Any]:
    # 1. 오늘 날짜 기준 D-Day 레코드 조회
    stmt = select(PsiRecord).where(PsiRecord.psi_date == today)
    res = await session.execute(stmt)
    today_records = res.scalars().all()
    
    items_processed = 0
    # 2. D-Day 소요수량을 재고에서 차감
    for rec in today_records:
        if rec.required_qty > 0:
            stmt_item = select(ItemMaster).where(ItemMaster.id == rec.item_id)
            res_item = await session.execute(stmt_item)
            item = res_item.scalar_one_or_none()
            if item:
                new_inventory = float(item.inventory_qty) - float(rec.required_qty)
                item.inventory_qty = max(0.0, new_inventory)
                session.add(item)
                items_processed += 1

    # 3. 만료된 D-Day 이전 레코드 삭제
    from sqlalchemy import delete
    stmt_del = delete(PsiRecord).where(PsiRecord.psi_date < today)
    await session.execute(stmt_del)
    
    await session.commit()
    
    tomorrow = today + timedelta(days=1)
    
    return {
        "advanced_date": today.isoformat(),
        "items_processed": items_processed,
        "next_d_day": tomorrow.isoformat()
    }

async def recompute_all(session: AsyncSession) -> int:
    # Get all distinct dates from PartListSnapshot
    stmt = select(PartListSnapshot.snapshot_date).distinct()
    res = await session.execute(stmt)
    dates = res.scalars().all()
    
    if not dates:
        return 0
        
    return await recompute_required_for_dates(session, list(dates))

async def one_click_solution(session: AsyncSession, user_id: int) -> Dict[str, Any]:
    import time
    from core.config import get_settings
    from services.folder_import_service import scan_and_import_folder
    from api.routes.ws import manager as ws_manager
    from models.ticket import Ticket
    
    settings = get_settings()
    today = date.today()
    start_time = time.time()
    steps = []
    
    # Step 1: Advance Day
    try:
        adv_res = await advance_day(session, today)
        steps.append({"step": 1, "name": "advance_day", "status": "ok", "detail": f"processed:{adv_res['items_processed']}"})
    except Exception as e:
        steps.append({"step": 1, "name": "advance_day", "status": "failed", "detail": str(e)})

    # Step 2: DP Import
    try:
        if settings.DPDB_PATH:
            dp_res = await scan_and_import_folder(session, settings.DPDB_PATH, "dp", user_id)
            steps.append({"step": 2, "name": "dp_import", "status": "ok", "detail": f"success:{dp_res.get('success')}, skipped:{dp_res.get('skipped')}"})
        else:
            steps.append({"step": 2, "name": "dp_import", "status": "failed", "detail": "DPDB_PATH not set"})
    except Exception as e:
        steps.append({"step": 2, "name": "dp_import", "status": "failed", "detail": str(e)})

    # Step 3: PSI Recompute
    try:
        recomputed = await recompute_all(session)
        steps.append({"step": 3, "name": "psi_recompute", "status": "ok", "detail": f"items/dates:{recomputed}"})
    except Exception as e:
        steps.append({"step": 3, "name": "psi_recompute", "status": "failed", "detail": str(e)})

    # Step 4: Ticket Creation
    try:
        # Check shortages for the next 30 days
        date_from = today
        date_to = today + timedelta(days=30)
        
        # Get all records where shortage might occur
        # available_qty - required_qty < 0. For missing available_qty, consider inventory_qty.
        # But our simple get_shortage_summary checks exactly one date. We can just use the DB's inventory_qty
        # Since this is a simplified ticket creator, we'll check today's shortage.
        # For actual PSI, shortages propagate. We'll use get_shortage_summary for the next few days.
        
        created_tickets = 0
        for i in range(7): # Check next 7 days for shortages to create tickets
            check_date = today + timedelta(days=i)
            shortages = await get_shortage_summary(session, check_date)
            
            for s in shortages:
                item_id = s["item_id"]
                shortage_val = abs(s["shortage_qty"])
                
                # Check if there's already an open ticket for this item
                stmt = select(Ticket).where(
                    Ticket.related_item_id == item_id,
                    Ticket.status.in_(["open", "in_progress"])
                )
                res = await session.execute(stmt)
                existing = res.first()
                
                if not existing:
                    priority = "normal"
                    if shortage_val > 100:
                        priority = "critical"
                    elif shortage_val > 50:
                        priority = "high"
                        
                    t = Ticket(
                        title=f"Shortage Alert: {s['part_number']}",
                        description=f"Shortage of {shortage_val} units detected on {s['psi_date'].isoformat()}.",
                        status="open",
                        priority=priority,
                        category="psi_alert",
                        creator_id=user_id,
                        related_item_id=item_id
                    )
                    session.add(t)
                    created_tickets += 1
                    
        await session.commit()
        steps.append({"step": 4, "name": "ticket_create", "status": "ok", "detail": f"created:{created_tickets}"})
    except Exception as e:
        steps.append({"step": 4, "name": "ticket_create", "status": "failed", "detail": str(e)})

    # Step 5: Broadcast
    try:
        await ws_manager.broadcast({"type": "refresh_dashboard"})
        steps.append({"step": 5, "name": "broadcast", "status": "ok"})
    except Exception as e:
        steps.append({"step": 5, "name": "broadcast", "status": "failed", "detail": str(e)})

    elapsed = time.time() - start_time
    
    return {
        "steps": steps,
        "elapsed_sec": round(elapsed, 2)
    }

async def build_psi_matrix_v2(
    session: AsyncSession,
    date_from: date,
    days: int = 7,
    expeditor_user_id: Optional[int] = None,
    supply_type: Optional[str] = None,
    vendor_code: Optional[str] = None,
) -> dict:
    """
    4행 블록 PSI 매트릭스 계산.
    날짜 범위: date_from ~ date_from + (days - 1)
    """
    import polars as pl
    from sqlalchemy import text

    date_to = date_from + timedelta(days=days - 1)
    date_list = [date_from + timedelta(days=i) for i in range(days)]
    date_strs = [d.isoformat() for d in date_list]

    # ─────────────────────────────────────────────────────────────────
    # 1. 아이템 목록 조회 (ItemMaster + Vendor + UserAssignment 조인)
    # ─────────────────────────────────────────────────────────────────
    from models.item_master import ItemMaster
    from models.vendor import Vendor
    from models.assignment import UserAssignment

    stmt = select(ItemMaster).where(ItemMaster.is_active == True)

    if expeditor_user_id:
        # user_assignments에서 해당 Expeditor의 vendor code 목록
        assign_stmt = select(UserAssignment.resource_key).where(
            UserAssignment.user_id == expeditor_user_id,
            UserAssignment.resource_type == "vendor",
        )
        assign_res = await session.execute(assign_stmt)
        vendor_codes = [r[0] for r in assign_res.all()]
        if not vendor_codes:
            return {"date_columns": date_strs, "rows": []}
        # vendor code → vendor id
        v_stmt = select(Vendor.id).where(Vendor.code.in_(vendor_codes))
        v_res = await session.execute(v_stmt)
        vendor_ids = [r[0] for r in v_res.all()]
        if not vendor_ids:
            return {"date_columns": date_strs, "rows": []}
        stmt = stmt.where(ItemMaster.vendor_id.in_(vendor_ids))

    if vendor_code:
        v_stmt = select(Vendor.id).where(Vendor.code == vendor_code)
        v_res = await session.execute(v_stmt)
        v_row = v_res.scalar_one_or_none()
        if not v_row:
            return {"date_columns": date_strs, "rows": []}
        stmt = stmt.where(ItemMaster.vendor_id == v_row)

    res = await session.execute(stmt)
    items = res.scalars().all()
    if not items:
        return {"date_columns": date_strs, "rows": []}

    part_numbers = [it.part_number for it in items]

    # ─────────────────────────────────────────────────────────────────
    # 2. BOM 메타데이터 (supply_type, level, vendor_raw)
    # ─────────────────────────────────────────────────────────────────
    from models.bom import BomItem

    bom_stmt = (
        select(BomItem.part_number, BomItem.supply_type, BomItem.level,
               BomItem.description, BomItem.uom)
        .where(BomItem.part_number.in_(part_numbers))
        .order_by(BomItem.part_number, BomItem.sort_order)
    )
    bom_res = await session.execute(bom_stmt)
    bom_map: dict[str, dict] = {}
    for pn, st, lv, desc, uom in bom_res.all():
        if pn not in bom_map:
            bom_map[pn] = {"supply_type": st, "level": str(lv) if lv is not None else None,
                           "bom_desc": desc, "uom": uom or "EA"}

    if supply_type:
        part_numbers = [pn for pn in part_numbers if bom_map.get(pn, {}).get("supply_type") == supply_type]
        items = [it for it in items if it.part_number in set(part_numbers)]
        if not items:
            return {"date_columns": date_strs, "rows": []}

    # ─────────────────────────────────────────────────────────────────
    # 3. 소요량 집계 (part_list_snapshots)
    # ─────────────────────────────────────────────────────────────────
    from models.part_list import PartListSnapshot

    snap_stmt = (
        select(
            PartListSnapshot.part_number,
            PartListSnapshot.snapshot_date,
            func.sum(PartListSnapshot.required_qty).label("req_qty"),
        )
        .where(
            PartListSnapshot.snapshot_date >= date_from,
            PartListSnapshot.snapshot_date <= date_to,
            PartListSnapshot.part_number.in_(part_numbers),
        )
        .group_by(PartListSnapshot.part_number, PartListSnapshot.snapshot_date)
    )
    snap_res = await session.execute(snap_stmt)
    # {part_number: {date_str: required_qty}}
    req_map: dict[str, dict[str, float]] = {}
    for pn, dt, qty in snap_res.all():
        dt_str = dt.isoformat() if hasattr(dt, "isoformat") else str(dt)
        req_map.setdefault(pn, {})[dt_str] = float(qty)

    # ─────────────────────────────────────────────────────────────────
    # 4. 입고/불량 데이터 (psi_daily_records)
    # ─────────────────────────────────────────────────────────────────
    from models.psi import PsiDailyRecord

    daily_stmt = select(PsiDailyRecord).where(
        PsiDailyRecord.part_number.in_(part_numbers),
        PsiDailyRecord.record_date >= date_from,
        PsiDailyRecord.record_date <= date_to,
    )
    daily_res = await session.execute(daily_stmt)
    # {part_number: {date_str: {incoming, defect}}}
    daily_map: dict[str, dict[str, dict]] = {}
    for rec in daily_res.scalars().all():
        dt_str = rec.record_date.isoformat()
        daily_map.setdefault(rec.part_number, {})[dt_str] = {
            "incoming": float(rec.incoming_qty),
            "defect": float(rec.defect_qty),
        }

    # ─────────────────────────────────────────────────────────────────
    # 5. 기간 내 계획 수량 합산 (plan_qty)
    # ─────────────────────────────────────────────────────────────────
    plan_qty_map: dict[str, float] = {
        pn: sum(req_map.get(pn, {}).values())
        for pn in part_numbers
    }

    # ─────────────────────────────────────────────────────────────────
    # 6. 4행 블록 데이터 계산
    # ─────────────────────────────────────────────────────────────────
    rows = []
    for item in items:
        pn = item.part_number
        inventory = float(item.inventory_qty or 0.0)
        binfo = bom_map.get(pn, {})

        by_date: dict[str, dict] = {}
        running_balance = inventory

        for d_str in date_strs:
            required = req_map.get(pn, {}).get(d_str, 0.0)
            day_info = daily_map.get(pn, {}).get(d_str, {})
            incoming = day_info.get("incoming", 0.0)
            defect = day_info.get("defect", 0.0)

            running_balance = running_balance - required + incoming - defect

            by_date[d_str] = {
                "required": required,
                "incoming": incoming,
                "defect": defect,
                "balance": round(running_balance, 2),
            }

        rows.append({
            "item_id": item.id,
            "part_number": pn,
            "description": item.description or binfo.get("bom_desc"),
            "level": binfo.get("level"),
            "supply_type": binfo.get("supply_type"),
            "uom": binfo.get("uom", "EA"),
            "vendor_primary": item.vendor_raw,
            "vendor_secondary": item.lower_vendor_raw,
            "plan_qty": plan_qty_map.get(pn, 0.0),
            "inventory_qty": inventory,
            "by_date": by_date,
        })

    # plan_qty 내림차순 정렬
    rows.sort(key=lambda r: r["plan_qty"], reverse=True)

    return {"date_columns": date_strs, "rows": rows}

async def upsert_daily_record(
    session: AsyncSession,
    part_number: str,
    record_date: date,
    incoming_qty: float,
    defect_qty: float,
    note: Optional[str],
    user_id: int,
) -> dict:
    """입고/불량 기록 upsert (part_number + record_date 기준)."""
    from models.psi import PsiDailyRecord
    from datetime import datetime

    stmt = select(PsiDailyRecord).where(
        PsiDailyRecord.part_number == part_number,
        PsiDailyRecord.record_date == record_date,
    )
    res = await session.execute(stmt)
    rec = res.scalar_one_or_none()

    if rec:
        rec.incoming_qty = incoming_qty
        rec.defect_qty = defect_qty
        if note is not None:
            rec.note = note
        rec.recorded_by = user_id
        rec.updated_at = datetime.utcnow()
    else:
        rec = PsiDailyRecord(
            part_number=part_number,
            record_date=record_date,
            incoming_qty=incoming_qty,
            defect_qty=defect_qty,
            note=note,
            recorded_by=user_id,
        )
        session.add(rec)

    await session.commit()
    await session.refresh(rec)
    return {"id": rec.id, "status": "ok"}


async def patch_item_inventory(
    session: AsyncSession,
    item_id: int,
    inventory_qty: float,
    defect_qty: Optional[float] = None,
) -> dict:
    """PSI 고정컬럼에서 재고수량 직접 편집."""
    from models.item_master import ItemMaster

    stmt = select(ItemMaster).where(ItemMaster.id == item_id)
    res = await session.execute(stmt)
    item = res.scalar_one_or_none()
    if not item:
        raise ValueError(f"ItemMaster id={item_id} not found")

    item.inventory_qty = inventory_qty
    if defect_qty is not None:
        item.defect_qty = defect_qty
    session.add(item)
    await session.commit()
    return {"item_id": item_id, "inventory_qty": inventory_qty}


async def get_daily_records(
    session: AsyncSession,
    part_number: Optional[str] = None,
    date_from: Optional[date] = None,
    date_to: Optional[date] = None,
) -> list:
    """입고/불량 기록 목록 조회 (탭 뷰용)."""
    from models.psi import PsiDailyRecord
    from models.user import User

    stmt = (
        select(PsiDailyRecord, User.display_name.label("recorded_by_name"))
        .outerjoin(User, User.id == PsiDailyRecord.recorded_by)
        .order_by(PsiDailyRecord.record_date.desc(), PsiDailyRecord.part_number)
    )
    if part_number:
        stmt = stmt.where(PsiDailyRecord.part_number == part_number)
    if date_from:
        stmt = stmt.where(PsiDailyRecord.record_date >= date_from)
    if date_to:
        stmt = stmt.where(PsiDailyRecord.record_date <= date_to)

    res = await session.execute(stmt)
    return [
        {
            "id": rec.id,
            "part_number": rec.part_number,
            "record_date": rec.record_date.isoformat(),
            "incoming_qty": rec.incoming_qty,
            "defect_qty": rec.defect_qty,
            "note": rec.note,
            "recorded_by": rec.recorded_by,
            "recorded_by_name": rname,
        }
        for rec, rname in res.all()
    ]
