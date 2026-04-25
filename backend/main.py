from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from api.router import router as api_router
from core.database import async_engine
from sqlmodel import text
import models # Ensure all models are loaded

@asynccontextmanager
async def lifespan(app: FastAPI):
    # DB 연결 확인
    try:
        async with async_engine.connect() as conn:
            await conn.execute(text("SELECT 1"))
    except Exception as e:
        print(f"DB 연결 실패: {e}")
    
    # Redis 연결 확인 (향후 구현 시 추가)
    yield
    # Cleanup

app = FastAPI(title="LARS Platform API", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], # .env 설정 반영 가능
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(api_router)

@app.get("/health")
async def health_check():
    return {"status": "ok", "db": "connected", "redis": "pending"}
