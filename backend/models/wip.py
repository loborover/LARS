from datetime import datetime
from typing import Optional
from sqlmodel import SQLModel, Field, UniqueConstraint

class FactoryLocation(SQLModel, table=True):
    __tablename__ = "factory_locations"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    code: str = Field(unique=True, index=True)
    name: str
    zone: Optional[str] = None
    x_coord: Optional[float] = None
    y_coord: Optional[float] = None
    is_active: bool = Field(default=True)
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)

class StandardWip(SQLModel, table=True):
    __tablename__ = "standard_wip"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    item_id: int = Field(foreign_key="item_master.id")
    location_id: int = Field(foreign_key="factory_locations.id")
    target_qty: float = Field(default=0.0)
    safety_stock: Optional[float] = None
    notes: Optional[str] = None
    is_active: bool = Field(default=True)
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)

    __table_args__ = (
        UniqueConstraint("item_id", "location_id"),
    )
