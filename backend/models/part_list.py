from datetime import datetime, date
from typing import Optional
from sqlmodel import SQLModel, Field

class PartListSnapshot(SQLModel, table=True):
    __tablename__ = "part_list_snapshots"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    lot_id: int = Field(foreign_key="daily_plan_lots.id", index=True)
    part_number: str = Field(index=True)
    description: Optional[str] = None
    uom: str = Field(default="EA")
    vendor_id: Optional[int] = Field(default=None, foreign_key="vendors.id")
    vendor_raw: Optional[str] = None
    required_qty: float
    snapshot_date: date = Field(index=True)
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
