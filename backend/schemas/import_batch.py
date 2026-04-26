from pydantic import BaseModel
from typing import List, Optional, Any
from datetime import datetime

class BatchCreate(BaseModel):
    source_type: str
    source_name: str
    target_table: str

class BatchRead(BaseModel):
    id: int
    source_name: str
    target_table: str
    status: str
    records_inserted: int
    records_updated: int
    records_failed: int
    error_log: Optional[Any] = None
    started_at: datetime
    finished_at: Optional[datetime] = None

class PreviewRow(BaseModel):
    row_index: int
    data: dict
    is_valid: bool
    errors: List[str]

class PreviewResponse(BaseModel):
    batch_id: int
    total_rows: int
    valid_rows: int
    invalid_rows: int
    preview: List[PreviewRow]

class BatchUploadResult(BaseModel):
    batch_id: int
    filename: str
    status: str

class MultiUploadResponse(BaseModel):
    batches: List[BatchUploadResult]

class MultiPreviewResponse(BaseModel):
    previews: List[PreviewResponse]

class MultiProcessResponse(BaseModel):
    results: List[BatchRead]
