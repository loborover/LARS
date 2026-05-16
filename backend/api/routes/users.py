from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from core.database import get_session
from core.deps import get_current_user
from core.security import hash_password, verify_password
from models.user import User
from schemas.user import UserProfileRead, UserProfileUpdate, PasswordChange
from datetime import datetime

router = APIRouter(prefix="/users", tags=["users"])

@router.get("/me", response_model=UserProfileRead)
async def get_my_profile(current_user: User = Depends(get_current_user)):
    return current_user

@router.put("/me", response_model=UserProfileRead)
async def update_my_profile(
    data: UserProfileUpdate,
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    for field, value in data.model_dump(exclude_none=True).items():
        setattr(current_user, field, value)
    
    current_user.updated_at = datetime.utcnow()
    session.add(current_user)
    await session.commit()
    await session.refresh(current_user)
    return current_user

@router.put("/me/password")
async def change_my_password(
    data: PasswordChange,
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    if not verify_password(data.current_password, current_user.hashed_pw):
        raise HTTPException(status_code=400, detail="현재 비밀번호가 올바르지 않습니다")
    
    current_user.hashed_pw = hash_password(data.new_password)
    current_user.updated_at = datetime.utcnow()
    session.add(current_user)
    await session.commit()
    return {"message": "비밀번호가 변경되었습니다"}
