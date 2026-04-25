from sqlalchemy.ext.asyncio import create_async_engine, async_sessionmaker, AsyncSession
from sqlalchemy import create_engine
from core.config import get_settings

settings = get_settings()

# Async Engine (for FastAPI)
async_engine = create_async_engine(settings.DATABASE_URL, pool_pre_ping=True)
async_session = async_sessionmaker(async_engine, class_=AsyncSession, expire_on_commit=False)

# Sync Engine (for Alembic and synchronous scripts)
sync_db_url = settings.DATABASE_URL.replace("postgresql+asyncpg", "postgresql")
sync_engine = create_engine(sync_db_url, pool_pre_ping=True)

async def get_session() -> AsyncSession:
    """
    FastAPI dependency for database session.
    """
    async with async_session() as session:
        yield session

def get_sync_engine():
    """
    Returns synchronous engine for Alembic.
    """
    return sync_engine
