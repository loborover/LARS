import os
from fastapi import APIRouter
from fastapi.responses import Response
from pydantic import BaseModel

router = APIRouter()
PIPER_VOICE = os.getenv("PIPER_VOICE", "ko_KR-kss-medium")

class SpeechRequest(BaseModel):
    model: str = "tts-1"
    input: str
    voice: str = "ko-KR-kss"

@router.post("/audio/speech")
async def synthesize(req: SpeechRequest):
    audio = await _synthesize(req.input)
    return Response(content=audio, media_type="audio/mpeg")

async def _synthesize(text: str) -> bytes:
    # 1. Piper TTS 우선 시도 (로컬 GPU/CPU 가속)
    try:
        import subprocess, tempfile, asyncio
        with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f:
            out_path = f.name
        
        # piper 바이너리가 설치되어 있어야 함
        proc = await asyncio.create_subprocess_exec(
            "piper",
            "--model", PIPER_VOICE,
            "--output_file", out_path,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        await proc.communicate(input=text.encode("utf-8"))
        
        if os.path.exists(out_path):
            with open(out_path, "rb") as f:
                wav_bytes = f.read()
            os.unlink(out_path)
            
            # WAV -> MP3 변환 (ffmpeg 필요)
            proc2 = await asyncio.create_subprocess_exec(
                "ffmpeg", "-i", "pipe:0", "-f", "mp3", "pipe:1",
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
            )
            mp3_bytes, _ = await proc2.communicate(input=wav_bytes)
            if mp3_bytes:
                return mp3_bytes
    except Exception as e:
        print(f"[TTS] Piper 실패, edge-tts로 fallback: {e}")

    # 2. Fallback: edge-tts (인터넷 필요)
    try:
        import edge_tts
        communicate = edge_tts.Communicate(text, "ko-KR-SunHiNeural")
        audio_data = b""
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                audio_data += chunk["data"]
        return audio_data
    except Exception as e:
        print(f"[TTS] edge-tts도 실패: {e}")
        return b""
