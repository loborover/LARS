from datetime import datetime
from typing import Optional
from sqlmodel import SQLModel, Field, Index

class BomModel(SQLModel, table=True):
    __tablename__ = "bom_models"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    model_code: str = Field(unique=True, index=True)
    description: Optional[str] = None
    version: str = Field(default="1.0")
    is_active: bool = Field(default=True)
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)

class BomItem(SQLModel, table=True):
    __tablename__ = "bom_items"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    model_id: int = Field(foreign_key="bom_models.id", index=True)
    level: int
    part_number: str = Field(index=True)
    description: Optional[str] = None
    qty: float = Field(default=1.0)
    uom: str = Field(default="EA")
    vendor_id: Optional[int] = Field(default=None, foreign_key="vendors.id")
    vendor_raw: Optional[str] = None
    supply_type: Optional[str] = None
    path: str = Field(index=True)
    sort_order: int = Field(default=0)
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)

    __table_args__ = (
        Index("idx_bom_items_part_model", "part_number", "model_id"),
    )
