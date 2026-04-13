import os
from typing import Generator
from sqlalchemy.ext.asyncio import create_async_engine, AsyncSession
from sqlalchemy.orm import sessionmaker
from sqlmodel import SQLModel, create_engine, Session
from dotenv import load_dotenv

load_dotenv()

# 환경 변수로부터 DB URL 로드 (기본값은 SQLite 파일)
DATABASE_URL = os.getenv("DATABASE_URL", "postgresql+asyncpg://user:pass@localhost/lars_db")
# 동기식 URL도 필요할 때가 있음 (예: Polars write_database)
SYNC_DATABASE_URL = DATABASE_URL.replace("+asyncpg", "") if "postgresql" in DATABASE_URL else DATABASE_URL

# 1. 비동기 엔진 설정 (성능 중심 API 통신용)
engine = create_async_engine(DATABASE_URL, echo=False, future=True)
async_session_maker = sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)

# 2. 동기식 엔진 설정 (Polars 벌크 작업 및 초기 스키마 생성용)
sync_engine = create_engine(SYNC_DATABASE_URL)

async def init_db():
    """데이터베이스 테이블 초기화"""
    async with engine.begin() as conn:
        # SQLModel에 정의된 모든 테이블 생성
        # await conn.run_sync(SQLModel.metadata.create_all)
        pass

async def get_session() -> Generator[AsyncSession, None, None]:
    """FastAPI Dependency Injection용 비동기 세션 제너레이터"""
    async with async_session_maker() as session:
        yield session

# 3. Query Utility: Polars 연동
import polars as pl

def fetch_to_polars(query: str) -> pl.DataFrame:
    """SQL 쿼리 결과를 Polars DataFrame으로 즉시 가져옵니다."""
    return pl.read_database(query, SYNC_DATABASE_URL)

def export_from_polars(df: pl.DataFrame, table_name: str, if_exists: str = "append"):
    """Polars DataFrame을 DB 테이블로 초고속 벌크 인서트합니다."""
    df.write_database(table_name, SYNC_DATABASE_URL, if_exists=if_exists)
