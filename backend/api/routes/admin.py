from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Dict, Any
from pydantic import BaseModel
from sqlmodel import select
from core.database import get_session
from core.deps import require_role
from core.security import hash_password
from models.user import User
from models.vendor import Vendor

router = APIRouter(dependencies=[Depends(require_role("admin"))])

class UserCreate(BaseModel):
    email: str
    display_name: str
    role: str
    password: str

class UserUpdate(BaseModel):
    role: str
    is_active: bool

class VendorCreate(BaseModel):
    code: str
    name: str

@router.get("/users")
async def get_users(session: AsyncSession = Depends(get_session)) -> List[Dict[str, Any]]:
    stmt = select(User)
    res = await session.execute(stmt)
    users = res.scalars().all()
    return [
        {
            "id": u.id,
            "email": u.email,
            "display_name": u.display_name,
            "role": u.role,
            "is_active": u.is_active
        } for u in users
    ]

@router.post("/users")
async def create_user(data: UserCreate, session: AsyncSession = Depends(get_session)) -> Dict[str, Any]:
    stmt = select(User).where(User.email == data.email)
    res = await session.execute(stmt)
    if res.scalar_one_or_none():
        raise HTTPException(status_code=400, detail="Email already registered")
        
    user = User(
        email=data.email,
        display_name=data.display_name,
        role=data.role,
        hashed_pw=hash_password(data.password)
    )
    session.add(user)
    await session.commit()
    await session.refresh(user)
    
    return {
        "id": user.id,
        "email": user.email,
        "display_name": user.display_name,
        "role": user.role,
        "is_active": user.is_active
    }

@router.put("/users/{user_id}")
async def update_user(user_id: int, data: UserUpdate, session: AsyncSession = Depends(get_session)) -> Dict[str, Any]:
    stmt = select(User).where(User.id == user_id)
    res = await session.execute(stmt)
    user = res.scalar_one_or_none()
    
    if not user:
        raise HTTPException(status_code=404, detail="User not found")
        
    user.role = data.role
    user.is_active = data.is_active
    await session.commit()
    await session.refresh(user)
    
    return {
        "id": user.id,
        "email": user.email,
        "display_name": user.display_name,
        "role": user.role,
        "is_active": user.is_active
    }

@router.get("/vendors")
async def get_vendors(session: AsyncSession = Depends(get_session)) -> List[Dict[str, Any]]:
    stmt = select(Vendor)
    res = await session.execute(stmt)
    vendors = res.scalars().all()
    return [
        {
            "id": v.id,
            "code": v.code,
            "name": v.name,
            "is_active": v.is_active
        } for v in vendors
    ]

@router.post("/vendors")
async def create_vendor(data: VendorCreate, session: AsyncSession = Depends(get_session)) -> Dict[str, Any]:
    stmt = select(Vendor).where(Vendor.code == data.code)
    res = await session.execute(stmt)
    if res.scalar_one_or_none():
        raise HTTPException(status_code=400, detail="Vendor code already registered")
        
    vendor = Vendor(
        code=data.code,
        name=data.name
    )
    session.add(vendor)
    await session.commit()
    await session.refresh(vendor)
    
    return {
        "id": vendor.id,
        "code": vendor.code,
        "name": vendor.name,
        "is_active": vendor.is_active
    }

import httpx
from core.config import get_settings
from llm.factory import reset_provider

@router.get("/ai-config")
async def get_ai_config():
    s = get_settings()
    return {
        "ai_mode": s.AI_MODE,
        "ai_service_url": s.AI_SERVICE_URL,
        "cloud_llm_base_url": s.CLOUD_LLM_BASE_URL,
        "cloud_llm_model": s.CLOUD_LLM_MODEL,
        "cloud_api_key_set": bool(s.CLOUD_LLM_API_KEY),
        "local_llm_model": s.LOCAL_LLM_MODEL,
    }

@router.post("/ai-config/test")
async def test_ai_connection():
    """현재 AI_MODE 설정으로 연결 테스트"""
    s = get_settings()

    if s.AI_MODE == "disabled":
        return {"success": False, "message": "AI 모드가 비활성화 상태입니다."}

    if s.AI_MODE == "internal":
        try:
            async with httpx.AsyncClient(timeout=10.0) as client:
                resp = await client.get(f"{s.AI_SERVICE_URL}/health")
                if resp.status_code == 200:
                    return {"success": True, "message": f"LARS AI Service 연결 성공: {s.AI_SERVICE_URL}"}
                return {"success": False, "message": f"응답 코드: {resp.status_code}"}
        except Exception as e:
            return {"success": False, "message": f"연결 실패: {e}"}

    if s.AI_MODE == "local":
        try:
            async with httpx.AsyncClient(timeout=5.0) as client:
                resp = await client.get(f"{s.OLLAMA_URL}/api/tags")
                if resp.status_code == 200:
                    models = [m["name"] for m in resp.json().get("models", [])]
                    return {"success": True, "message": f"Ollama 연결 성공. 모델: {models}"}
        except Exception as e:
            return {"success": False, "message": f"Ollama 연결 실패: {e}"}

    if s.AI_MODE == "cloud":
        return {"success": bool(s.CLOUD_LLM_API_KEY), "message": "API Key " + ("설정됨" if s.CLOUD_LLM_API_KEY else "미설정")}

    return {"success": False, "message": "알 수 없는 AI 모드"}
