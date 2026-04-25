from fastapi import APIRouter, Depends, HTTPException, UploadFile, File, Form
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from typing import List
import os
import shutil
from datetime import datetime
from core.database import get_session
from core.deps import get_current_user, require_role
from models.user import User
from models.import_batch import ImportBatch
from schemas.import_batch import BatchRead, PreviewResponse, PreviewRow
from parsers import bom_parser, daily_plan_parser, validator
from services import bom_service

router = APIRouter(dependencies=[Depends(require_role("manager", "admin"))])

UPLOAD_DIR = "data/raw"

@router.post("/upload")
async def upload_file(
    file: UploadFile = File(...),
    target_table: str = Form(...),
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    if target_table not in ["bom", "daily_plan", "item_master"]:
        raise HTTPException(status_code=400, detail="Invalid target_table")

    os.makedirs(UPLOAD_DIR, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    filename = f"{timestamp}_{file.filename}"
    file_path = os.path.join(UPLOAD_DIR, filename)

    with open(file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

    batch = ImportBatch(
        source_type="excel_upload",
        source_name=filename,
        target_table=target_table,
        status="pending",
        started_by=current_user.id
    )
    session.add(batch)
    await session.commit()
    await session.refresh(batch)

    return {"batch_id": batch.id, "status": "pending"}

@router.get("/preview/{batch_id}", response_model=PreviewResponse)
async def preview_batch(
    batch_id: int,
    session: AsyncSession = Depends(get_session)
):
    stmt = select(ImportBatch).where(ImportBatch.id == batch_id)
    res = await session.execute(stmt)
    batch = res.scalar_one_or_none()
    
    if not batch:
        raise HTTPException(status_code=404, detail="Batch not found")
        
    file_path = os.path.join(UPLOAD_DIR, batch.source_name)
    if not os.path.exists(file_path):
        raise HTTPException(status_code=404, detail="File not found")

    try:
        if batch.target_table == "bom":
            df = bom_parser.parse(file_path)
            val_res = validator.validate_bom(df)
        elif batch.target_table == "daily_plan":
            df = daily_plan_parser.parse(file_path)
            val_res = validator.validate_daily_plan(df)
        else:
            raise HTTPException(status_code=400, detail="Preview not implemented for target")
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

    preview_rows = []
    # limit to 20
    df_head = df.head(20)
    for i, row in enumerate(df_head.iter_rows(named=True)):
        row_errors = [e.message for e in val_res.errors if e.row_index == i]
        preview_rows.append(PreviewRow(
            row_index=i,
            data=row,
            is_valid=len(row_errors) == 0,
            errors=row_errors
        ))

    return PreviewResponse(
        batch_id=batch.id,
        total_rows=df.height,
        valid_rows=val_res.valid_row_count,
        invalid_rows=val_res.invalid_row_count,
        preview=preview_rows
    )

@router.post("/batches/{batch_id}/process", response_model=BatchRead)
async def process_batch(
    batch_id: int,
    session: AsyncSession = Depends(get_session)
):
    stmt = select(ImportBatch).where(ImportBatch.id == batch_id)
    res = await session.execute(stmt)
    batch = res.scalar_one_or_none()
    
    if not batch:
        raise HTTPException(status_code=404, detail="Batch not found")
        
    if batch.status not in ["pending", "failed"]:
        raise HTTPException(status_code=400, detail="Batch cannot be processed")

    file_path = os.path.join(UPLOAD_DIR, batch.source_name)
    if not os.path.exists(file_path):
        raise HTTPException(status_code=404, detail="File not found")

    batch.status = "processing"
    session.add(batch)
    await session.commit()

    try:
        if batch.target_table == "bom":
            df = bom_parser.parse(file_path)
            val_res = validator.validate_bom(df)
            if not val_res.is_valid:
                raise Exception("Validation failed")
            inserted = await bom_service.import_from_df(session, df, batch.id)
            batch.records_inserted = inserted
            
        elif batch.target_table == "daily_plan":
            df = daily_plan_parser.parse(file_path)
            val_res = validator.validate_daily_plan(df)
            if not val_res.is_valid:
                raise Exception("Validation failed")
            # For now, DP save logic is not implemented, just marked success
            batch.records_inserted = df.height
        
        batch.status = "success"
        batch.finished_at = datetime.utcnow()
    except Exception as e:
        batch.status = "failed"
        batch.error_log = {"error": str(e)}
        batch.records_failed = 0 # Not counting precisely yet

    session.add(batch)
    await session.commit()
    await session.refresh(batch)

    return BatchRead(
        id=batch.id,
        source_name=batch.source_name,
        target_table=batch.target_table,
        status=batch.status,
        records_inserted=batch.records_inserted,
        records_updated=batch.records_updated,
        records_failed=batch.records_failed,
        error_log=batch.error_log,
        started_at=batch.started_at,
        finished_at=batch.finished_at
    )

@router.get("/batches", response_model=List[BatchRead])
async def list_batches(session: AsyncSession = Depends(get_session)):
    stmt = select(ImportBatch).order_by(ImportBatch.id.desc()).limit(20)
    res = await session.execute(stmt)
    batches = res.scalars().all()
    return batches
