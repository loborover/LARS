from pydantic import BaseModel
from typing import Optional, List
from datetime import date

class DailyPlanRead(BaseModel):
    plan_id: int
    plan_date: date
    line_code: str
    line_name: str
    lot_count: int
    import_batch_id: Optional[int]

class DailyPlanLotRead(BaseModel):
    id: int
    wo_number: Optional[str]
    model_code: str
    lot_number: str
    planned_qty: int
    input_qty: int
    output_qty: int

class DailyLotView(BaseModel):
    wo_number: Optional[str]
    model_code: str
    lot_number: str
    daily_qty: float      # 해당 날짜의 수량
    planned_qty: int      # 전체 계획수량
    output_qty: int       # 실적
    sort_order: int

class DailyLineView(BaseModel):
    line_code: str
    line_name: str
    lots: List[DailyLotView]
    total_daily_qty: float    # 해당 라인 당일 수량 합계

class DailyPlanViewResponse(BaseModel):
    date: str
    lines: List[DailyLineView]
    total_qty: float          # 전체 라인 합계
