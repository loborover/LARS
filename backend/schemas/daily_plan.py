from pydantic import BaseModel
from typing import Optional
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
