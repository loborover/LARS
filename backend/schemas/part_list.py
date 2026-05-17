from pydantic import BaseModel
from typing import Optional, List, Dict
from datetime import date

# --- 기존 유지 ---
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

# --- 신규 추가 ---

class LotViewRow(BaseModel):
    """Lot View: 1 Lot = 1 행. parts 딕셔너리 = {part_number: required_qty}"""
    lot_id: int
    wo_number: Optional[str]
    model_number: str          # model_code.suffix
    plan_date: str             # ISO date string
    planned_qty: int
    parts: Dict[str, float]    # key=part_number, value=required_qty

class LotViewResponse(BaseModel):
    batch_id: int
    part_columns: List[str]    # 전체 품번 목록 (total_qty 내림차순, 최대 300개)
    part_meta: Dict[str, 'PartMeta']   # key=part_number, value=PartMeta
    rows: List[LotViewRow]

class PartMeta(BaseModel):
    description: Optional[str] = None
    uom: str = "EA"

class PsiMatrixRow(BaseModel):
    """PSI Matrix: 1 품번 = 1 행. by_date = {ISO date: qty}"""
    part_number: str
    description: Optional[str]
    vendor_raw: Optional[str]
    uom: str
    total_qty: float
    by_date: Dict[str, float]  # key=ISO date string, value=total_required_qty

class PsiMatrixResponse(BaseModel):
    batch_id: int
    date_columns: List[str]    # 날짜 목록 (오름차순)
    rows: List[PsiMatrixRow]   # total_qty 내림차순, 최대 200개
