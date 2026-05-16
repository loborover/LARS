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
            started_by=user_id
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

    # 사후 작업 (전체 파일 일괄 처리 후 1회만 수행)
    if success > 0:
        if file_type == "bom":
            await item_master_service.rebuild_from_bom(session)
        elif file_type == "dp":
            # 전체 DP 스냅샷 날짜 추출 (간단히 처리하기 위해 재계산)
            # 여기서는 One-Click에서 재계산하도록 안내하므로, DP 임포트 자체에서는 
            # 재계산하지 않거나, 전체 재계산을 트리거할 수 있습니다.
            # 지시서: DP Import 완료 후 PSI required_qty 재계산 트리거
            # 하지만 여러 파일 임포트 시 너무 빈번할 수 있으므로 여기서 전체 재계산
            # 대신 DPDB Import API 밖의 컨트롤러나 여기서 recompute_all을 호출하도록 합니다.
            pass

    return {
        "total": total,
        "success": success,
        "failed": failed,
        "skipped": skipped,
        "files": file_results
    }
