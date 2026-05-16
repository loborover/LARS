from fastapi import APIRouter, Depends, HTTPException, UploadFile, File, Form, BackgroundTasks
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from typing import List, Dict, Any
import os
import shutil
from datetime import datetime
from core.database import get_session, engine
from core.deps import get_current_user, require_role
from core.config import get_settings
from models.user import User
from models.import_batch import ImportBatch
from schemas.import_batch import (
    BatchRead, PreviewResponse, PreviewRow,
    BatchUploadResult, MultiUploadResponse, MultiPreviewResponse, MultiProcessResponse
)
from parsers import bom_parser, daily_plan_parser, validator
from services import bom_service, item_master_service, folder_import_service, psi_service, daily_plan_service, part_list_service

router = APIRouter(dependencies=[Depends(require_role("manager", "admin"))])

UPLOAD_DIR = "data/raw"
settings = get_settings()

@router.post("/folder/bom")
async def import_folder_bom(
    background_tasks: BackgroundTasks,
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
) -> Dict[str, Any]:
    path = settings.BOMDB_PATH
    if not path:
        raise HTTPException(status_code=400, detail="BOMDB_PATH not configured")
    result = await folder_import_service.scan_and_import_folder(session, path, "bom", current_user.id)
    if result.get("success", 0) > 0:
        background_tasks.add_task(item_master_service.rebuild_from_bom_background, engine)
    return result

@router.post("/folder/dp")
async def import_folder_dp(
    background_tasks: BackgroundTasks,
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
) -> Dict[str, Any]:
    path = settings.DPDB_PATH
    if not path:
        raise HTTPException(status_code=400, detail="DPDB_PATH not configured")
    result = await folder_import_service.scan_and_import_folder(session, path, "dp", current_user.id)
    
    if result.get("success", 0) > 0:
        background_tasks.add_task(psi_service.recompute_all_background, engine)
        
    return result

@router.post("/upload")
async def upload_file(
    file: UploadFile = File(...),
    target_table: str = Form(...),
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    if target_table not in ["bom", "daily_plan"]:
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
        started_by=current_user.id,
        data_source="local"
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
    background_tasks: BackgroundTasks,
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
            
            # Background IT Rebuild
            background_tasks.add_task(item_master_service.rebuild_from_bom_background, engine)
            
        elif batch.target_table == "daily_plan":
            df = daily_plan_parser.parse(file_path)
            val_res = validator.validate_daily_plan(df)
            if not val_res.is_valid:
                raise Exception(f"Validation failed: {val_res.errors[0].message if val_res.errors else 'unknown'}")
            
            inserted = await daily_plan_service.import_from_df(session, df, batch.id)
            batch.records_inserted = inserted
            
            # Background PL & PSI Recompute
            dates = await daily_plan_service.get_dates_in_df(df)
            background_tasks.add_task(part_list_service.recompute_background, engine, dates, batch.id)
            background_tasks.add_task(psi_service.recompute_all_background, engine)
        
        batch.status = "success"
        batch.finished_at = datetime.utcnow()
    except Exception as e:
        batch.status = "failed"
        batch.error_log = {"error": str(e)}
        batch.records_failed = 0 

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

@router.post("/upload-multi", response_model=MultiUploadResponse)
async def upload_multi_files(
    files: list[UploadFile] = File(...),
    target_table: str = Form(...),
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    if target_table not in ["bom", "daily_plan"]:
        raise HTTPException(status_code=400, detail="Invalid target_table")

    os.makedirs(UPLOAD_DIR, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    
    results = []
    for file in files:
        filename = f"{timestamp}_{file.filename}"
        file_path = os.path.join(UPLOAD_DIR, filename)

        with open(file_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)

        batch = ImportBatch(
            source_type="excel_upload",
            source_name=filename,
            target_table=target_table,
            status="pending",
            started_by=current_user.id,
            data_source="local"
        )
        session.add(batch)
        await session.commit()
        await session.refresh(batch)
        
        results.append(BatchUploadResult(batch_id=batch.id, filename=file.filename, status="pending"))

    return MultiUploadResponse(batches=results)

@router.post("/preview-multi", response_model=MultiPreviewResponse)
async def preview_multi_batch(
    batch_ids: list[int],
    session: AsyncSession = Depends(get_session)
):
    previews = []
    for batch_id in batch_ids:
        try:
            stmt = select(ImportBatch).where(ImportBatch.id == batch_id)
            res = await session.execute(stmt)
            batch = res.scalar_one_or_none()
            
            if not batch:
                continue
                
            file_path = os.path.join(UPLOAD_DIR, batch.source_name)
            if not os.path.exists(file_path):
                continue

            if batch.target_table == "bom":
                df = bom_parser.parse(file_path)
                val_res = validator.validate_bom(df)
            elif batch.target_table == "daily_plan":
                df = daily_plan_parser.parse(file_path)
                val_res = validator.validate_daily_plan(df)
            else:
                continue

            preview_rows = []
            df_head = df.head(20)
            for i, row in enumerate(df_head.iter_rows(named=True)):
                row_errors = [e.message for e in val_res.errors if e.row_index == i]
                preview_rows.append(PreviewRow(
                    row_index=i,
                    data=row,
                    is_valid=len(row_errors) == 0,
                    errors=row_errors
                ))

            previews.append(PreviewResponse(
                batch_id=batch.id,
                total_rows=df.height,
                valid_rows=val_res.valid_row_count,
                invalid_rows=val_res.invalid_row_count,
                preview=preview_rows
            ))
        except Exception as e:
            continue
            
    return MultiPreviewResponse(previews=previews)

@router.post("/batches/process-multi", response_model=MultiProcessResponse)
async def process_multi_batch(
    batch_ids: list[int],
    background_tasks: BackgroundTasks,
    session: AsyncSession = Depends(get_session)
):
    results = []
    for batch_id in batch_ids:
        try:
            result = await process_batch(batch_id, background_tasks, session)
            results.append(result)
        except Exception as e:
            # Fetch the batch to return a failed state
            stmt = select(ImportBatch).where(ImportBatch.id == batch_id)
            res = await session.execute(stmt)
            batch = res.scalar_one_or_none()
            if batch:
                batch.status = "failed"
                batch.error_log = {"error": str(e)}
                session.add(batch)
                await session.commit()
                await session.refresh(batch)
                
                results.append(BatchRead(
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
                ))
                
    return MultiProcessResponse(results=results)
