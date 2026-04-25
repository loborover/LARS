from datetime import datetime
from typing import Optional
from sqlmodel import SQLModel, Field

class ItemMaster(SQLModel, table=True):
    __tablename__ = "item_master"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    level: int = Field(default=1)
    description: str
    part_number: str = Field(unique=True, index=True)
    vendor_id: Optional[int] = Field(default=None, foreign_key="vendors.id")
    vendor_raw: Optional[str] = None
    tracking_user_id: Optional[int] = Field(default=None, foreign_key="users.id", index=True)
    is_active: bool = Field(default=True)
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
