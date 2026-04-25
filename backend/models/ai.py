from datetime import datetime
from typing import Optional, List, Dict, Any
from sqlmodel import SQLModel, Field
from sqlalchemy.dialects.postgresql import JSONB, ARRAY
from sqlalchemy import Column, String

class MeetingRecord(SQLModel, table=True):
    __tablename__ = "meeting_records"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    recorded_at: datetime
    duration_sec: Optional[int] = None
    audio_path: str
    transcript: Optional[str] = None
    summary: Optional[str] = None
    participants: Optional[List[str]] = Field(default=None, sa_column=Column(ARRAY(String)))
    action_items: Optional[Dict[str, Any]] = Field(default=None, sa_column=Column(JSONB))
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)

class AgentLog(SQLModel, table=True):
    __tablename__ = "agent_logs"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    role: str
    provider_tier: str
    provider_model: str
    input_tokens: Optional[int] = None
    output_tokens: Optional[int] = None
    duration_ms: Optional[int] = None
    success: bool = Field(default=True)
    error_msg: Optional[str] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
