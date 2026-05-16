from pydantic import BaseModel
from typing import Optional, List, Dict
from datetime import date

class PsiCellRead(BaseModel):
    required_qty: float
    available_qty: Optional[float]
    shortage_qty: float

class PsiMatrixResponse(BaseModel):
    dates: List[str]
    items: List[dict]
    cells: Dict[str, PsiCellRead]

class PsiCellUpdate(BaseModel):
    available_qty: float
    notes: Optional[str] = None

class PsiShortageItem(BaseModel):
    item_id: int
    part_number: str
    description: Optional[str]
    psi_date: date
    required_qty: float
    available_qty: Optional[float]
    shortage_qty: float

# --- Phase 5 New Schemas ---

class DateHeader(BaseModel):
    label: str = "" # "D-Day", "D+1", etc.
    date: str          # "2026-05-16"
    week: int          # ISO week number

class PsiRowFull(BaseModel):
    item_id: int
    part_number: str
    description: str
    level: int
    supply_type: Optional[str]
    uom: str
    vendor_raw: Optional[str]
    lower_vendor_raw: Optional[str]
    tech_spec: Optional[str]
    
    inventory_qty: float
    defect_qty: float
    is_picked: bool
    
    daily_demand: Dict[str, float]
    date_headers: List[DateHeader]
    expeditor_name: Optional[str]

class PsiFilterParams(BaseModel):
    expeditor_user_id: Optional[int] = None
    supply_type: Optional[str] = None
    level: Optional[int] = None
    model_code: Optional[str] = None
    date_from: date = date.today()

class PsiInventoryUpdate(BaseModel):
    inventory_qty: float
    defect_qty: float

class PsiPickUpdate(BaseModel):
    is_picked: bool
