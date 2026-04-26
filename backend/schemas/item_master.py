from pydantic import BaseModel
from typing import Optional

class ItemMasterRead(BaseModel):
    id: int
    level: int
    description: str
    part_number: str
    vendor_raw: Optional[str]
    tracking_user_id: Optional[int]
    is_active: bool

class ItemMasterCreate(BaseModel):
    level: int = 1
    description: str
    part_number: str
    vendor_raw: Optional[str] = None

class ItemMasterUpdate(BaseModel):
    description: Optional[str] = None
    vendor_raw: Optional[str] = None
    is_active: Optional[bool] = None

class ItemBomUsage(BaseModel):
    model_code: str
    description: Optional[str]
    qty: float
    level: int
    path: str
