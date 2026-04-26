from abc import ABC, abstractmethod
from typing import AsyncIterator, Optional

class LLMProvider(ABC):
    """
    LLM 공급자 추상 클래스. 모든 LLM 호출은 이 인터페이스를 통한다.
    """

    @property
    @abstractmethod
    def tier(self) -> str:
        """'local' 또는 'cloud'"""
        pass

    @property
    @abstractmethod
    def model_name(self) -> str:
        pass

    @abstractmethod
    async def chat(
        self,
        messages: list[dict],
        system: str = "",
        tools: list[dict] | None = None,
        max_tokens: int = 2048,
    ) -> str:
        """동기식 응답 반환. messages 형식: [{"role": "user", "content": "..."}]"""
        pass

    @abstractmethod
    async def embed(self, text: str) -> list[float]:
        """텍스트를 벡터로 변환"""
        pass
