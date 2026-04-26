from functools import lru_cache
from pydantic_settings import BaseSettings
from typing import Literal

class Settings(BaseSettings):
    # DB
    DATABASE_URL: str
    REDIS_URL: str

    # Auth
    JWT_SECRET_KEY: str
    JWT_ALGORITHM: str = "HS256"
    ACCESS_TOKEN_EXPIRE_MINUTES: int = 60
    REFRESH_TOKEN_EXPIRE_DAYS: int = 30

    # AI 모드 설정 (Admin이 선택)
    # disabled: AI 기능 전체 비활성화
    # local: 동일 머신의 Ollama 직접 호출 (개발/테스트용)
    # internal: 내부망 LARS AI Service 호출 (운영 권장)
    # cloud: 외부 클라우드 API 호출
    AI_MODE: Literal["disabled", "local", "internal", "cloud"] = "disabled"

    # local 모드용 (기존 OLLAMA_URL 유지)
    OLLAMA_URL: str = "http://localhost:11434"
    LOCAL_LLM_MODEL: str = "qwen2.5:32b"

    # internal 모드용 (LARS AI Service)
    AI_SERVICE_URL: str = "http://192.168.0.100:8088"

    # cloud 모드용
    CLOUD_LLM_BASE_URL: str = "https://api.openai.com/v1"
    CLOUD_LLM_MODEL: str = "gpt-4o"
    CLOUD_LLM_API_KEY: str = ""

    # Whisper (local 모드에서만 사용)
    WHISPER_MODEL_SIZE: str = "medium"

    # 스케줄러
    SCHEDULER_TIMEZONE: str = "Asia/Seoul"
    PSI_MONITOR_INTERVAL_MINUTES: int = 15

    class Config:
        env_file = ".env"

@lru_cache()
def get_settings() -> Settings:
    return Settings()
