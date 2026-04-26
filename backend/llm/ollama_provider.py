import httpx
from core.config import get_settings
from llm.base import LLMProvider

class OllamaProvider(LLMProvider):
    def __init__(self):
        self._settings = get_settings()
        self._base_url = self._settings.OLLAMA_URL
        self._model = self._settings.LOCAL_LLM_MODEL

    @property
    def tier(self) -> str:
        return "local"

    @property
    def model_name(self) -> str:
        return self._model

    async def chat(self, messages, system="", tools=None, max_tokens=2048) -> str:
        payload = {
            "model": self._model,
            "messages": messages,
            "stream": False,
            "options": {"num_predict": max_tokens},
        }
        if system:
            payload["messages"] = [{"role": "system", "content": system}] + messages

        async with httpx.AsyncClient(timeout=120.0) as client:
            resp = await client.post(f"{self._base_url}/api/chat", json=payload)
            resp.raise_for_status()
            return resp.json()["message"]["content"]

    async def embed(self, text: str) -> list[float]:
        async with httpx.AsyncClient(timeout=30.0) as client:
            resp = await client.post(
                f"{self._base_url}/api/embeddings",
                json={"model": self._model, "prompt": text}
            )
            resp.raise_for_status()
            return resp.json()["embedding"]
