from pydantic import BaseModel
from typing import Optional, List, Dict
from datetime import date

class PsiCellRead(BaseModel):
    required_qty: float
    available_qty: Optional[float]
    shortage_qty: float  # 서비스에서 계산: (available_qty or 0) - required_qty

class PsiMatrixResponse(BaseModel):
    dates: List[str]  # "YYYY-MM-DD" 형식
    items: List[dict]  # {"id": int, "part_number": str, "description": str}
    cells: Dict[str, PsiCellRead]  # key: "{item_id}_{YYYY-MM-DD}"

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
