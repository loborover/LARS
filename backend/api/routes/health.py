from fastapi import APIRouter
from datetime import datetime

router = APIRouter()

@router.get("/status")
async def system_status() -> dict:
    """DB, AI API, 시스템 시간 상태 반환"""
    db_ok = False
    ai_ok = False

    # DB 체크
    try:
        from core.database import get_session_context
        async with get_session_context() as session:
            # Using text() from sqlalchemy
            from sqlalchemy import text
            await session.execute(text("SELECT 1"))
        db_ok = True
    except Exception:
        db_ok = False

    # AI API 키 유무 체크 (실제 API 호출 없이 키 존재 여부만)
    try:
        import os
        # Settings에서 가져오는 것이 더 정확할 수 있음
        from core.config import get_settings
        settings = get_settings()
        
        if settings.AI_MODE == "cloud":
            ai_ok = bool(settings.CLOUD_LLM_API_KEY and len(settings.CLOUD_LLM_API_KEY) > 10)
        elif settings.AI_MODE == "internal":
            ai_ok = bool(settings.AI_SERVICE_URL)
        elif settings.AI_MODE == "local":
            ai_ok = bool(settings.OLLAMA_URL)
        else:
            ai_ok = False
    except Exception:
        ai_ok = False

    return {
        "db": "ok" if db_ok else "error",
        "ai": "ok" if ai_ok else "error",
        "server_time": datetime.now().isoformat(),
    }
