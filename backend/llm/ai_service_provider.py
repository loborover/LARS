from openai import AsyncOpenAI
from llm.base import LLMProvider

class AIServiceProvider(LLMProvider):
    """
    LARS AI Service(internal) 또는 OpenAI-compatible cloud API를 호출.
    base_url에 따라 자동으로 라우팅된다.
    """

    def __init__(self, base_url: str, model: str, api_key: str = "lars-internal"):
        # base_url에 /v1이 중복되지 않도록 처리
        clean_url = base_url.rstrip("/")
        if not clean_url.endswith("/v1"):
            clean_url = f"{clean_url}/v1"
        self._client = AsyncOpenAI(base_url=clean_url, api_key=api_key)
        self._model = model

    @property
    def tier(self) -> str:
        return "ai_service"

    @property
    def model_name(self) -> str:
        return self._model

    async def chat(self, messages: list[dict], system: str = "", tools: list[dict] | None = None, max_tokens: int = 2048) -> str:
        all_messages = []
        if system:
            all_messages.append({"role": "system", "content": system})
        all_messages.extend(messages)

        kwargs = {
            "model": self._model,
            "messages": all_messages,
            "max_tokens": max_tokens,
        }
        if tools:
            kwargs["tools"] = tools

        resp = await self._client.chat.completions.create(**kwargs)
        return resp.choices[0].message.content or ""

    async def embed(self, text: str) -> list[float]:
        resp = await self._client.embeddings.create(model=self._model, input=text)
        return resp.data[0].embedding

    async def transcribe(self, audio_bytes: bytes, filename: str = "audio.webm") -> str:
        """LARS AI Service의 /v1/audio/transcriptions 호출"""
        import httpx
        base = str(self._client.base_url).rstrip("/")
        async with httpx.AsyncClient(timeout=60.0) as client:
            resp = await client.post(
                f"{base}/audio/transcriptions",
                files={"file": (filename, audio_bytes, "audio/webm")},
                data={"model": "whisper-1", "language": "ko"},
            )
            resp.raise_for_status()
            return resp.json().get("text", "")

    async def synthesize(self, text: str, voice: str = "ko-KR-kss") -> bytes:
        """LARS AI Service의 /v1/audio/speech 호출"""
        resp = await self._client.audio.speech.create(
            model="tts-1",
            voice=voice,
            input=text,
        )
        return resp.content
