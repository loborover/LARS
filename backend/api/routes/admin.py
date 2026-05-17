from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Dict, Any, Optional
from pydantic import BaseModel
from sqlmodel import select
from core.database import get_session
from core.deps import require_role
from core.security import hash_password
from models.user import User
from models.vendor import Vendor
from models.assignment import UserAssignment
from schemas.user import UserCreate, UserAdminUpdate
from datetime import datetime
from sqlalchemy import delete as sa_delete

router = APIRouter(dependencies=[Depends(require_role("admin"))])

class VendorCreate(BaseModel):
    code: str
    name: str

@router.get("/users")
async def get_users(session: AsyncSession = Depends(get_session)) -> List[Dict[str, Any]]:
    stmt = select(User).order_by(User.id)
    res = await session.execute(stmt)
    users = res.scalars().all()
    return [
        {
            "id": u.id,
            "email": u.email,
            "display_name": u.display_name,
            "role": u.role,
            "is_active": u.is_active,
            "phone": u.phone,
            "company": u.company,
            "department": u.department,
            "rank": u.rank,
            "position": u.position,
            "created_at": u.created_at.isoformat() if u.created_at else None
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
        hashed_pw=hash_password(data.password),
        phone=data.phone,
        company=data.company,
        department=data.department,
        rank=data.rank,
        position=data.position
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
async def update_user(user_id: int, data: UserAdminUpdate, session: AsyncSession = Depends(get_session)) -> Dict[str, Any]:
    stmt = select(User).where(User.id == user_id)
    res = await session.execute(stmt)
    user = res.scalar_one_or_none()
    
    if not user:
        raise HTTPException(status_code=404, detail="User not found")
        
    for field, value in data.model_dump(exclude_none=True).items():
        setattr(user, field, value)
    
    user.updated_at = datetime.utcnow()
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

@router.post("/users/{user_id}/reset-password")
async def admin_reset_password(
    user_id: int,
    new_password: str,
    session: AsyncSession = Depends(get_session)
):
    stmt = select(User).where(User.id == user_id)
    res = await session.execute(stmt)
    user = res.scalar_one_or_none()
    
    if not user:
        raise HTTPException(status_code=404, detail="User not found")
        
    user.hashed_pw = hash_password(new_password)
    user.updated_at = datetime.utcnow()
    session.add(user)
    await session.commit()
    return {"message": "비밀번호가 초기화되었습니다"}

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


# ─────────────────────────────────────────────────────
# 담당자 배정 (User Assignments)
# ─────────────────────────────────────────────────────

class AssignmentCreate(BaseModel):
    user_id: int
    resource_type: str   # 'vendor' | 'line' | 'model'
    resource_key: str    # Vendor.code | line.code | model_number


@router.get("/assignments")
async def get_assignments(
    resource_type: str,
    session: AsyncSession = Depends(get_session)
) -> List[Dict[str, Any]]:
    """
    resource_type별 전체 배정 목록.
    vendor 타입이면 Vendor.name을 JOIN해서 함께 반환.
    """
    stmt = (
        select(UserAssignment, User.display_name.label("user_name"))
        .join(User, User.id == UserAssignment.user_id)
        .where(UserAssignment.resource_type == resource_type)
        .order_by(User.display_name, UserAssignment.resource_key)
    )
    res = await session.execute(stmt)
    rows = res.all()

    result = []
    # vendor 타입이면 code→name 조회용 맵 생성
    vendor_map: dict[str, str] = {}
    if resource_type == "vendor":
        v_res = await session.execute(select(Vendor))
        for v in v_res.scalars().all():
            vendor_map[v.code] = v.name

    for assignment, user_name in rows:
        item: Dict[str, Any] = {
            "id": assignment.id,
            "user_id": assignment.user_id,
            "user_name": user_name,
            "resource_type": assignment.resource_type,
            "resource_key": assignment.resource_key,
        }
        if resource_type == "vendor":
            item["resource_name"] = vendor_map.get(assignment.resource_key, "")
        result.append(item)

    return result


@router.get("/assignments/user/{user_id}")
async def get_user_assignments(
    user_id: int,
    resource_type: Optional[str] = None,
    session: AsyncSession = Depends(get_session)
) -> List[Dict[str, Any]]:
    """특정 유저의 배정 목록. resource_type 필터 선택적."""
    stmt = select(UserAssignment).where(UserAssignment.user_id == user_id)
    if resource_type:
        stmt = stmt.where(UserAssignment.resource_type == resource_type)
    stmt = stmt.order_by(UserAssignment.resource_type, UserAssignment.resource_key)
    res = await session.execute(stmt)
    assignments = res.scalars().all()

    # vendor name 조회
    vendor_map: dict[str, str] = {}
    if not resource_type or resource_type == "vendor":
        v_res = await session.execute(select(Vendor))
        for v in v_res.scalars().all():
            vendor_map[v.code] = v.name

    return [
        {
            "id": a.id,
            "user_id": a.user_id,
            "resource_type": a.resource_type,
            "resource_key": a.resource_key,
            "resource_name": vendor_map.get(a.resource_key, "") if a.resource_type == "vendor" else "",
        }
        for a in assignments
    ]


@router.post("/assignments")
async def create_assignment(
    data: AssignmentCreate,
    session: AsyncSession = Depends(get_session)
) -> Dict[str, Any]:
    """배정 추가. 중복이면 200으로 기존 항목 반환(멱등)."""
    stmt = select(UserAssignment).where(
        UserAssignment.user_id == data.user_id,
        UserAssignment.resource_type == data.resource_type,
        UserAssignment.resource_key == data.resource_key,
    )
    res = await session.execute(stmt)
    existing = res.scalar_one_or_none()
    if existing:
        return {"id": existing.id, "status": "already_exists"}

    assignment = UserAssignment(
        user_id=data.user_id,
        resource_type=data.resource_type,
        resource_key=data.resource_key,
    )
    session.add(assignment)
    await session.commit()
    await session.refresh(assignment)
    return {"id": assignment.id, "status": "created"}


@router.delete("/assignments/{assignment_id}")
async def delete_assignment(
    assignment_id: int,
    session: AsyncSession = Depends(get_session)
) -> Dict[str, Any]:
    """배정 삭제."""
    stmt = select(UserAssignment).where(UserAssignment.id == assignment_id)
    res = await session.execute(stmt)
    assignment = res.scalar_one_or_none()
    if not assignment:
        raise HTTPException(status_code=404, detail="Assignment not found")
    await session.delete(assignment)
    await session.commit()
    return {"status": "deleted", "id": assignment_id}


@router.get("/lines")
async def get_lines(session: AsyncSession = Depends(get_session)) -> List[Dict[str, Any]]:
    """생산라인 목록 (배정 패널용)."""
    from models.daily_plan import ProductionLine
    stmt = select(ProductionLine).where(ProductionLine.is_active == True).order_by(ProductionLine.code)
    res = await session.execute(stmt)
    lines = res.scalars().all()
    return [{"code": l.code, "name": l.name} for l in lines]
