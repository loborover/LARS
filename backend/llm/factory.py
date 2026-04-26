from core.config import get_settings
from llm.base import LLMProvider

_provider: LLMProvider | None = None

def get_llm_provider() -> LLMProvider | None:
    """
    AI_MODE 설정에 따라 적절한 LLM Provider 반환.
    disabled 모드이면 None 반환 → 호출부에서 None 체크 필수.
    """
    global _provider
    if _provider is not None:
        return _provider

    settings = get_settings()
    mode = settings.AI_MODE

    if mode == "disabled":
        return None
    elif mode == "local":
        from llm.ollama_provider import OllamaProvider
        _provider = OllamaProvider()
    elif mode == "internal":
        from llm.ai_service_provider import AIServiceProvider
        _provider = AIServiceProvider(
            base_url=settings.AI_SERVICE_URL,
            model=settings.LOCAL_LLM_MODEL,
        )
    elif mode == "cloud":
        from llm.ai_service_provider import AIServiceProvider
        _provider = AIServiceProvider(
            base_url=settings.CLOUD_LLM_BASE_URL,
            model=settings.CLOUD_LLM_MODEL,
            api_key=settings.CLOUD_LLM_API_KEY,
        )
    else:
        return None

    return _provider

def reset_provider():
    """Admin이 AI_MODE를 변경할 때 호출하여 캐시 초기화"""
    global _provider
    _provider = None
