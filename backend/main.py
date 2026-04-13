import os
from datetime import datetime
from typing import List, Dict, Any, Optional
import polars as pl
from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

app = FastAPI(title="LARS Platform Backend", version="1.0.0")

# CORS 설정 (WebUI 연동용)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 데이터 디렉토리 설정
RAW_DATA_PATH = "data/raw"
os.makedirs(RAW_DATA_PATH, exist_ok=True)

# 1. 파일 목록화 (Staging Registry) 모델
class FileInfo(BaseModel):
    name: str
    path: str
    size: int
    modified_at: str
    headers: List[str]
    preview_data: List[Dict[str, Any]]

@app.get("/")
def read_root():
    return {"status": "LARS Platform Online", "engine": "FastAPI + Polars"}

# 2. .xlsx 파일 등록 및 목록 조회 기능 (가공 전 목록 등재)
@app.get("/files", response_model=List[FileInfo])
def list_files():
    """
    data/raw 디렉토리의 모든 .xlsx 파일을 스캔하여 
    가공 전 목록(Staging Registry)으로 반환합니다.
    """
    files_info = []
    for filename in os.listdir(RAW_DATA_PATH):
        if filename.endswith(".xlsx"):
            full_path = os.path.join(RAW_DATA_PATH, filename)
            stat = os.stat(full_path)
            
            # Polars를 사용하여 헤더 및 프리뷰 데이터만 고속 로드
            try:
                # Polars의 read_excel은 매우 빠름 (fastexcel 엔진 활용 권장)
                df = pl.read_excel(full_path, read_options={"n_rows": 5})
                headers = df.columns
                preview = df.to_dicts()
            except Exception as e:
                headers = [f"Error reading headers: {str(e)}"]
                preview = []

            files_info.append(FileInfo(
                name=filename,
                path=full_path,
                size=stat.st_size,
                modified_at=datetime.fromtimestamp(stat.st_mtime).isoformat(),
                headers=headers,
                preview_data=preview
            ))
    return files_info

@app.post("/files/upload")
async def upload_file(file: UploadFile = File(...)):
    """새로운 엑셀 파일을 업로드하여 Registry에 등록합니다."""
    file_location = f"{RAW_DATA_PATH}/{file.filename}"
    with open(file_location, "wb+") as file_object:
        file_object.write(file.file.read())
    return {"info": f"file '{file.filename}' saved at '{file_location}'"}

from backend.core.database import get_session, fetch_to_polars, export_from_polars, QueryRequest
from sqlalchemy.ext.asyncio import AsyncSession
from fastapi import Depends

# 3. DB Infrastructure (Query Bridge) - 실제 구현
@app.post("/db/fetch")
async def db_fetch_query(request: QueryRequest):
    """
    DB로부터 쿼리 결과를 가져옵니다 (Polars DataFrame으로 반환).
    """
    try:
        # Polars의 고성능 DB 읽기 기능 활용
        df = fetch_to_polars(request.query)
        return {
            "status": "Success", 
            "records_count": len(df),
            "data": df.to_dicts()
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Database Fetch Error: {str(e)}")

@app.post("/db/export")
async def db_export_data(table_name: str, data: List[Dict[str, Any]]):
    """
    가공된 데이터를 DB로 내보냅니다 (Polars 벌크 인서트 활용).
    """
    try:
        if not data:
            return {"status": "No data to export"}

        df = pl.from_dicts(data)
        export_from_polars(df, table_name)
        return {"status": "Export Success", "table": table_name, "records_count": len(df)}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Database Export Error: {str(e)}")


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
