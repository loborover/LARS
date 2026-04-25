from pydantic import BaseModel
from typing import List, Optional

class BomItemRead(BaseModel):
    id: int
    level: int
    part_number: str
    description: Optional[str] = None
    qty: float
    uom: str
    vendor_raw: Optional[str] = None
    supply_type: Optional[str] = None
    path: str
    children: List["BomItemRead"] = []

class BomModelRead(BaseModel):
    id: int
    model_code: str
    description: Optional[str] = None
    version: str

class BomTreeResponse(BaseModel):
    model: BomModelRead
    items: List[BomItemRead]

class ReverseResult(BaseModel):
    part_number: str
    models: List[BomModelRead]
