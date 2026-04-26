import os
import tempfile
import asyncio
from core.config import get_settings

async def transcribe(audio_bytes: bytes, ext: str = "webm") -> str:
    """오디오 bytes → 한국어 텍스트. AI_MODE에 따라 처리 방식 결정."""
    settings = get_settings()

    if settings.AI_MODE == "disabled":
        return ""

    if settings.AI_MODE == "local":
        return await _transcribe_local(audio_bytes, ext, settings.WHISPER_MODEL_SIZE)

    # internal / cloud: AIServiceProvider 사용
    from llm.factory import get_llm_provider
    provider = get_llm_provider()
    if provider is None:
        return ""
    if not hasattr(provider, "transcribe"):
        return ""
    return await provider.transcribe(audio_bytes, filename=f"audio.{ext}")

async def synthesize(text: str) -> bytes:
    """텍스트 → 한국어 음성(MP3 bytes). AI_MODE에 따라 처리 방식 결정."""
    settings = get_settings()

    if settings.AI_MODE == "disabled":
        return b""

    if settings.AI_MODE == "local":
        return await _synthesize_edge_tts(text)

    # internal / cloud: AIServiceProvider 사용
    from llm.factory import get_llm_provider
    provider = get_llm_provider()
    if provider is None:
        return b""
    if not hasattr(provider, "synthesize"):
        return b""
    try:
        return await provider.synthesize(text)
    except Exception as e:
        print(f"[TTS] 실패: {e}")
        return b""

async def _transcribe_local(audio_bytes: bytes, ext: str, model_size: str) -> str:
    """로컬 faster-whisper 사용 (AI_MODE=local 전용)"""
    try:
        from faster_whisper import WhisperModel
    except ImportError:
        print("[STT] faster-whisper 미설치. pip install faster-whisper")
        return ""

    def _run():
        with tempfile.NamedTemporaryFile(suffix=f".{ext}", delete=False) as f:
            f.write(audio_bytes)
            tmp_path = f.name
        try:
            model = WhisperModel(model_size, device="cpu", compute_type="int8")
            segments, _ = model.transcribe(tmp_path, language="ko", beam_size=5)
            return " ".join(s.text.strip() for s in segments)
        finally:
            if os.path.exists(tmp_path):
                os.unlink(tmp_path)

    return await asyncio.to_thread(_run)

async def _synthesize_edge_tts(text: str) -> bytes:
    """edge-tts 사용 (AI_MODE=local 전용, 인터넷 필요)"""
    try:
        import edge_tts
        communicate = edge_tts.Communicate(text, "ko-KR-SunHiNeural")
        audio_data = b""
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                audio_data += chunk["data"]
        return audio_data
    except Exception as e:
        print(f"[TTS] edge-tts 실패: {e}")
        return b""
