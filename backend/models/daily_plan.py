from datetime import datetime
from typing import Optional
from sqlmodel import SQLModel, Field, UniqueConstraint

class ProductionLine(SQLModel, table=True):
    __tablename__ = "production_lines"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    code: str = Field(unique=True, index=True)
    name: str
    is_active: bool = Field(default=True)

class DailyPlan(SQLModel, table=True):
    __tablename__ = "daily_plans"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    plan_date: datetime
    line_id: int = Field(foreign_key="production_lines.id")
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)

    __table_args__ = (
        UniqueConstraint("plan_date", "line_id"),
    )

class DailyPlanLot(SQLModel, table=True):
    __tablename__ = "daily_plan_lots"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    plan_id: int = Field(foreign_key="daily_plans.id", index=True)
    wo_number: Optional[str] = None
    model_id: Optional[int] = Field(default=None, foreign_key="bom_models.id")
    model_code: str = Field(index=True)
    lot_number: str
    planned_qty: int = Field(default=0)
    input_qty: int = Field(default=0)
    output_qty: int = Field(default=0)
    planned_start: Optional[datetime] = None
    sort_order: int = Field(default=0)
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
