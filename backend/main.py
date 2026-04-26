from contextlib import asynccontextmanager
from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from api.router import router as api_router
from api.routes.ws import router as ws_router, manager as ws_manager
from core.database import async_engine, get_session_context
from core.config import get_settings
from sqlmodel import text
import models
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from workers.psi_monitor import check_psi_shortages

# ParseError import (parsers에서 정의된 경우)
try:
    from parsers.validator import ParseError
except ImportError:
    ParseError = None

settings = get_settings()
scheduler = AsyncIOScheduler(timezone=settings.SCHEDULER_TIMEZONE)

@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        async with async_engine.connect() as conn:
            await conn.execute(text("SELECT 1"))
        print("[LARS] DB 연결 성공")
    except Exception as e:
        print(f"[LARS] DB 연결 실패: {e}")

    scheduler.add_job(
        check_psi_shortages,
        "interval",
        minutes=settings.PSI_MONITOR_INTERVAL_MINUTES,
        id="psi_monitor",
        kwargs={"ws_manager": ws_manager}
    )
    scheduler.start()
    print(f"[LARS] PSI 모니터 스케줄러 시작 (interval={settings.PSI_MONITOR_INTERVAL_MINUTES}분, tz={settings.SCHEDULER_TIMEZONE})")
    print(f"[LARS] AI 모드: {settings.AI_MODE}")

    yield

    scheduler.shutdown()
    print("[LARS] 스케줄러 종료")

app = FastAPI(title="LARS Platform API", lifespan=lifespan)

# 전역 예외 핸들러
if ParseError is not None:
    @app.exception_handler(ParseError)
    async def parse_error_handler(request: Request, exc: ParseError):
        return JSONResponse(status_code=400, content={"detail": str(exc), "type": "ParseError"})

@app.exception_handler(ValueError)
async def value_error_handler(request: Request, exc: ValueError):
    return JSONResponse(status_code=422, content={"detail": str(exc), "type": "ValueError"})

@app.exception_handler(Exception)
async def generic_error_handler(request: Request, exc: Exception):
    import traceback
    print(f"[LARS] 처리되지 않은 예외: {traceback.format_exc()}")
    return JSONResponse(status_code=500, content={"detail": "서버 내부 오류가 발생했습니다.", "type": type(exc).__name__})

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(api_router)
app.include_router(ws_router)

@app.get("/health")
async def health_check():
    return {"status": "ok", "ai_mode": settings.AI_MODE}
