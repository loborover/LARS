from datetime import datetime, date
from typing import Optional
from sqlmodel import SQLModel, Field

class Worker(SQLModel, table=True):
    __tablename__ = "workers"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    name: str
    employee_id: Optional[str] = Field(default=None, unique=True)
    line_id: Optional[int] = Field(default=None, foreign_key="production_lines.id")
    is_active: bool = Field(default=True)
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)

class LogisticsEfficiency(SQLModel, table=True):
    __tablename__ = "logistics_efficiency"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    worker_id: int = Field(foreign_key="workers.id", index=True)
    item_id: int = Field(foreign_key="item_master.id", index=True)
    model_id: Optional[int] = Field(default=None, foreign_key="bom_models.id")
    recorded_date: date = Field(index=True)
    target_qty: Optional[float] = None
    actual_qty: Optional[float] = None
    notes: Optional[str] = None
    is_realtime: bool = Field(default=False)
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
