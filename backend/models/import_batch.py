from datetime import datetime
from typing import Optional, Dict, Any
from sqlmodel import SQLModel, Field
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy import Column

class ImportBatch(SQLModel, table=True):
    __tablename__ = "import_batches"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    source_type: str
    source_name: str
    target_table: str
    records_inserted: int = Field(default=0)
    records_updated: int = Field(default=0)
    records_failed: int = Field(default=0)
    status: str = Field(default="pending")
    error_log: Optional[Dict[str, Any]] = Field(default=None, sa_column=Column(JSONB))
    started_by: Optional[int] = Field(default=None, foreign_key="users.id")
    started_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    finished_at: Optional[datetime] = None
