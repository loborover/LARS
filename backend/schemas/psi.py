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

# --- Phase 21 New Schemas ---

class PsiDayCell(BaseModel):
    required: float = 0.0      # 소요량 (1행)
    incoming: float = 0.0      # 입고수량 (2행)
    defect: float = 0.0        # 불량수량 (3행)
    balance: float = 0.0       # 재고잔량 (4행, 누적 계산값)

class PsiMatrixRowV2(BaseModel):
    item_id: int
    part_number: str
    description: Optional[str]
    level: Optional[str]
    supply_type: Optional[str]
    uom: str = "EA"
    vendor_primary: Optional[str]     # 1차 협력사 (vendor_raw)
    vendor_secondary: Optional[str]   # 2차 협력사 (lower_vendor_raw)
    plan_qty: float = 0.0             # 기간 내 총 계획 수량
    inventory_qty: float = 0.0        # 현재 재고 (편집 가능)
    by_date: Dict[str, PsiDayCell]    # key = "YYYY-MM-DD"

class PsiMatrixV2Response(BaseModel):
    date_columns: List[str]            # 표시 날짜 목록 (오름차순)
    rows: List[PsiMatrixRowV2]

class PsiDailyRecordRead(BaseModel):
    id: int
    part_number: str
    record_date: date
    incoming_qty: float
    defect_qty: float
    note: Optional[str]
    recorded_by: Optional[int]
    recorded_by_name: Optional[str]

class PsiDailyRecordUpsert(BaseModel):
    part_number: str
    record_date: date
    incoming_qty: float = 0.0
    defect_qty: float = 0.0
    note: Optional[str] = None

class InventoryPatch(BaseModel):
    inventory_qty: float
    defect_qty: Optional[float] = None   # ItemMaster 불량수량 (초기값)
