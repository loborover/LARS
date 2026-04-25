from functools import lru_cache
from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    # DB
    DATABASE_URL: str
    REDIS_URL: str

    # Auth
    JWT_SECRET_KEY: str
    JWT_ALGORITHM: str = "HS256"
    ACCESS_TOKEN_EXPIRE_MINUTES: int = 60
    REFRESH_TOKEN_EXPIRE_DAYS: int = 30

    # Local LLM
    OLLAMA_URL: str = "http://localhost:11434"
    LOCAL_LLM_MODEL: str = "qwen2.5:7b"

    # Cloud LLM
    CLOUD_LLM_BASE_URL: str = "https://api.openai.com/v1"
    CLOUD_LLM_MODEL: str = "gpt-4o"
    CLOUD_LLM_API_KEY: str = ""

    # Whisper
    WHISPER_MODEL_SIZE: str = "medium"

    class Config:
        env_file = ".env"

@lru_cache()
def get_settings() -> Settings:
    return Settings()
