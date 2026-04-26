from typing import Optional, List
from datetime import date
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.psi import PsiRecord
from models.item_master import ItemMaster
from models.part_list import PartListSnapshot

async def recompute_required_for_dates(session: AsyncSession, dates: List[date]) -> int:
    if not dates:
        return 0

    from sqlalchemy import func
    
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
            # snapshot_date might be datetime.date or datetime.datetime
            s_date = snapshot_date.date() if hasattr(snapshot_date, 'date') else snapshot_date
            req_dict[(item_id, s_date)] = float(req_qty)
            
    # Process PSI records for the affected item_ids and dates
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
    from datetime import timedelta
    
    # Generate date range
    dates = []
    curr = date_from
    while curr <= date_to:
        dates.append(curr.isoformat())
        curr += timedelta(days=1)
        
    # Query items
    stmt_items = select(ItemMaster).where(ItemMaster.is_active == True)
    if item_ids:
        stmt_items = stmt_items.where(ItemMaster.id.in_(item_ids))
    res_items = await session.execute(stmt_items)
    items = res_items.scalars().all()
    
    item_list = [{"id": i.id, "part_number": i.part_number, "description": i.description} for i in items]
    actual_item_ids = [i.id for i in items]
    
    if not actual_item_ids:
        return {"dates": dates, "items": [], "cells": {}}
        
    # Query cells
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
    # available_qty or 0 - required_qty < 0
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
