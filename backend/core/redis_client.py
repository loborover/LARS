import redis.asyncio as aioredis
from core.config import get_settings

_client: aioredis.Redis | None = None

async def get_redis() -> aioredis.Redis:
    global _client
    if _client is None:
        settings = get_settings()
        # aioredis 5.0+ supports from_url with asyncio support built-in
        _client = aioredis.from_url(settings.REDIS_URL, decode_responses=True)
    return _client

async def close_redis():
    global _client
    if _client:
        await _client.aclose()
        _client = None
