import os
import glob
from datetime import datetime
from typing import Dict, Any, List
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select

from models.import_batch import ImportBatch
from parsers import bom_parser, daily_plan_parser, validator
from services import bom_service, daily_plan_service, part_list_service, item_master_service
from core.config import get_settings

settings = get_settings()

async def scan_and_import_folder(
    session: AsyncSession,
    folder_path: str,
    file_type: str, # "bom" or "dp"
    user_id: int
) -> Dict[str, Any]:
    
    if not os.path.exists(folder_path):
        return {"total": 0, "success": 0, "failed": 0, "skipped": 0, "files": [], "error": f"Folder not found: {folder_path}"}
        
    # 1. 파일 목록 수집
    if file_type == "bom":
        patterns = ["*.xlsx"]
    elif file_type == "dp":
        patterns = ["Excel_Export_*.xlsx", "Production_Plan_*.csv"]
    else:
        return {"error": "Invalid file_type"}

    files_to_process = []
    for pattern in patterns:
        search_path = os.path.join(folder_path, pattern)
        files_to_process.extend(glob.glob(search_path))
        
    if not files_to_process:
        return {"total": 0, "success": 0, "failed": 0, "skipped": 0, "files": []}

    # 2. 이미 처리된 파일 필터링
    stmt = select(ImportBatch.source_name).where(
        ImportBatch.status == "success",
        ImportBatch.target_table == ("bom" if file_type == "bom" else "daily_plan")
    )
    res = await session.execute(stmt)
    processed_files = set(res.scalars().all())

    total = len(files_to_process)
    success = 0
    failed = 0
    skipped = 0
    file_results = []

    for file_path in files_to_process:
        filename = os.path.basename(file_path)
        
        # 파일명 기반 중복 스킵 (실제 운영 시에는 수정시간이나 파일 해시도 비교 가능)
        if filename in processed_files:
            skipped += 1
            file_results.append({"filename": filename, "status": "skipped", "message": "Already processed"})
            continue

        # 4. ImportBatch 생성
        batch = ImportBatch(
            source_type="folder_scan",
            source_name=filename,
            target_table="bom" if file_type == "bom" else "daily_plan",
            status="processing",
            started_by=user_id,
            data_source="local",
        )

        session.add(batch)
        await session.commit()
        await session.refresh(batch)

        try:
            if file_type == "bom":
                df = bom_parser.parse(file_path)
                val_res = validator.validate_bom(df)
                if not val_res.is_valid:
                    raise Exception(f"Validation failed: {val_res.errors[0].message if val_res.errors else 'unknown'}")
                inserted = await bom_service.import_from_df(session, df, batch.id)
                batch.records_inserted = inserted
            
            elif file_type == "dp":
                df = daily_plan_parser.parse(file_path)
                val_res = validator.validate_daily_plan(df)
                if not val_res.is_valid:
                    raise Exception(f"Validation failed: {val_res.errors[0].message if val_res.errors else 'unknown'}")
                inserted = await daily_plan_service.import_from_df(session, df, batch.id)
                batch.records_inserted = inserted

            batch.status = "success"
            batch.finished_at = datetime.utcnow()
            success += 1
            file_results.append({"filename": filename, "status": "success", "inserted": inserted})

        except Exception as e:
            await session.rollback()
            batch.status = "failed"
            batch.error_log = {"error": str(e)}
            batch.finished_at = datetime.utcnow()
            failed += 1
            file_results.append({"filename": filename, "status": "failed", "message": str(e)})
            session.add(batch)
            await session.commit()

    return {
        "total": total,
        "success": success,
        "failed": failed,
        "skipped": skipped,
        "files": file_results
    }
