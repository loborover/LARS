from pydantic import BaseModel
from typing import Optional
from datetime import datetime

class UserProfileRead(BaseModel):
    id: int
    email: str
    display_name: str
    role: str
    is_active: bool
    phone: Optional[str] = None
    company: Optional[str] = None
    department: Optional[str] = None
    rank: Optional[str] = None
    position: Optional[str] = None
    created_at: Optional[datetime] = None

class UserProfileUpdate(BaseModel):
    display_name: Optional[str] = None
    phone: Optional[str] = None
    company: Optional[str] = None
    department: Optional[str] = None
    rank: Optional[str] = None
    position: Optional[str] = None

class UserAdminUpdate(BaseModel):
    role: Optional[str] = None
    is_active: Optional[bool] = None
    display_name: Optional[str] = None
    phone: Optional[str] = None
    company: Optional[str] = None
    department: Optional[str] = None
    rank: Optional[str] = None
    position: Optional[str] = None

class UserCreate(BaseModel):
    email: str
    display_name: str
    role: str = "viewer"
    password: str
    phone: Optional[str] = None
    company: Optional[str] = None
    department: Optional[str] = None
    rank: Optional[str] = None
    position: Optional[str] = None

class PasswordChange(BaseModel):
    current_password: str
    new_password: str
