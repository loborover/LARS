from datetime import datetime
from typing import Optional
from sqlmodel import SQLModel, Field

class Ticket(SQLModel, table=True):
    __tablename__ = "tickets"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    title: str
    description: Optional[str] = None
    priority: str = Field(default="normal")
    status: str = Field(default="open")
    category: Optional[str] = None
    related_item_id: Optional[int] = Field(default=None, foreign_key="item_master.id")
    related_model_id: Optional[int] = Field(default=None, foreign_key="bom_models.id")
    assigned_to: Optional[int] = Field(default=None, foreign_key="users.id")
    created_by_agent: Optional[str] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    resolved_at: Optional[datetime] = None
