from typing import Optional
from sqlmodel import SQLModel, Field, UniqueConstraint

class UserAssignment(SQLModel, table=True):
    __tablename__ = "user_assignments"

    id: Optional[int] = Field(default=None, primary_key=True)
    user_id: int = Field(foreign_key="users.id", index=True)
    resource_type: str = Field(index=True)
    # resource_type 허용값:
    #   'vendor' → resource_key = Vendor.code
    #   'line'   → resource_key = ProductionLine.code
    #   'model'  → resource_key = model_number (Model.Suffix 형식)
    resource_key: str

    __table_args__ = (
        UniqueConstraint("user_id", "resource_type", "resource_key"),
    )
