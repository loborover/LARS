from pydantic import BaseModel
from typing import Optional, List

class ItemMasterRead(BaseModel):
    id: int
    level: int
    description: str
    part_number: str
    vendor_raw: Optional[str]
    vendor_name: Optional[str] = None
    lower_vendor_raw: Optional[str]
    lower_vendor_name: Optional[str] = None
    inventory_qty: float
    defect_qty: float
    is_picked: bool
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
    model_description: Optional[str]
    bom_qty: float
    paths: List[str]
    levels: List[int]
