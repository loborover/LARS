from openai import AsyncOpenAI
from core.config import get_settings
from llm.base import LLMProvider

class CloudProvider(LLMProvider):
    def __init__(self):
        settings = get_settings()
        self._client = AsyncOpenAI(
            base_url=settings.CLOUD_LLM_BASE_URL,
            api_key=settings.CLOUD_LLM_API_KEY or "none",
        )
        self._model = settings.CLOUD_LLM_MODEL

    @property
    def tier(self) -> str:
        return "cloud"

    @property
    def model_name(self) -> str:
        return self._model

    async def chat(self, messages, system="", tools=None, max_tokens=2048) -> str:
        full_messages = []
        if system:
            full_messages.append({"role": "system", "content": system})
        full_messages.extend(messages)

        response = await self._client.chat.completions.create(
            model=self._model,
            messages=full_messages,
            max_tokens=max_tokens,
        )
        return response.choices[0].message.content

    async def embed(self, text: str) -> list[float]:
        resp = await self._client.embeddings.create(
            model="text-embedding-ada-002",
            input=text
        )
        return resp.data[0].embedding
