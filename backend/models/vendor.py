from datetime import datetime
from typing import Optional
from sqlmodel import SQLModel, Field

class Vendor(SQLModel, table=True):
    __tablename__ = "vendors"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    code: str = Field(unique=True, index=True)
    name: str
    is_active: bool = Field(default=True)
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
