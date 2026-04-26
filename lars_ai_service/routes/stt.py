import os
import tempfile
from fastapi import APIRouter, UploadFile, File, Form
from faster_whisper import WhisperModel

router = APIRouter()
WHISPER_MODEL_SIZE = os.getenv("WHISPER_MODEL", "large-v3")

_model: WhisperModel | None = None

def get_model() -> WhisperModel:
    global _model
    if _model is None:
        # GPU(cuda) 사용 설정
        _model = WhisperModel(WHISPER_MODEL_SIZE, device="cuda", compute_type="float16")
        print(f"[STT] Faster-Whisper 로드 완료: {WHISPER_MODEL_SIZE} (GPU)")
    return _model

@router.post("/audio/transcriptions")
async def transcribe(
    file: UploadFile = File(...),
    model: str = Form(default="whisper-1"),
    language: str = Form(default="ko"),
):
    audio_bytes = await file.read()
    ext = file.filename.rsplit(".", 1)[-1] if "." in file.filename else "webm"

    with tempfile.NamedTemporaryFile(suffix=f".{ext}", delete=False) as f:
        f.write(audio_bytes)
        tmp_path = f.name

    try:
        whisper = get_model()
        segments, _ = whisper.transcribe(tmp_path, language=language, beam_size=5)
        text = " ".join(s.text.strip() for s in segments)
        return {"text": text}
    finally:
        if os.path.exists(tmp_path):
            os.unlink(tmp_path)
