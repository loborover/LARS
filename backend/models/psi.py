from datetime import datetime, date
from typing import Optional
from sqlmodel import SQLModel, Field, UniqueConstraint

class PsiRecord(SQLModel, table=True):
    __tablename__ = "psi_records"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    item_id: int = Field(foreign_key="item_master.id", index=True)
    psi_date: date = Field(index=True)
    required_qty: float = Field(default=0.0)
    available_qty: Optional[float] = None
    # shortage_qty is computed in DB or handled by service
    notes: Optional[str] = None
    last_updated_by: Optional[int] = Field(default=None, foreign_key="users.id")
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)

    __table_args__ = (
        UniqueConstraint("item_id", "psi_date"),
    )
