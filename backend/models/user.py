from datetime import datetime
from typing import Optional
from sqlmodel import SQLModel, Field

class User(SQLModel, table=True):
    __tablename__ = "users"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    email: str = Field(unique=True, index=True)
    display_name: str
    role: str = Field(default="viewer")
    is_active: bool = Field(default=True)
    hashed_pw: str

    # 신규 프로필 필드
    phone: Optional[str] = Field(default=None)          # 전화번호
    company: Optional[str] = Field(default=None)        # 소속사
    department: Optional[str] = Field(default=None)     # 부서
    rank: Optional[str] = Field(default=None)           # 직급
    position: Optional[str] = Field(default=None)       # 직책

    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
