from pydantic import BaseModel, computed_field
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
    suffix: str = ""
    description: Optional[str] = None
    version: str

    @computed_field
    @property
    def model_number(self) -> str:
        return f"{self.model_code}.{self.suffix}" if self.suffix else self.model_code

class BomTreeResponse(BaseModel):
    model: BomModelRead
    items: List[BomItemRead]

class ReverseResult(BaseModel):
    part_number: str
    models: List[BomModelRead]

class BomAmountItem(BaseModel):
    part_number: str
    description: Optional[str] = None
    uom: str
    total_qty: float
    vendor_raw: Optional[str] = None
    supply_type: Optional[str] = None
    occurrence_count: int  # BOM 내 중복 등장 횟수

class BomAmountResponse(BaseModel):
    model: BomModelRead
    items: List[BomAmountItem]
