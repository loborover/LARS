from pydantic import BaseModel
from typing import Optional, List
from datetime import date

class PartListItem(BaseModel):
    part_number: str
    description: Optional[str]
    total_required_qty: float
    uom: str
    vendor_raw: Optional[str]

class PartListResponse(BaseModel):
    plan_date: date
    line_code: Optional[str]
    items: List[PartListItem]
    total_items: int
