import io
import base64
from fastapi import APIRouter, Depends, HTTPException, UploadFile, File
from fastapi.responses import Response
from pydantic import BaseModel
from sqlalchemy.ext.asyncio import AsyncSession
from typing import Optional

from core.database import get_session
from core.deps import get_current_user
from models.user import User
from llm.factory import get_llm_provider
from agent import lars_agent
from services import voice_service

router = APIRouter(dependencies=[Depends(get_current_user)])

class ChatRequest(BaseModel):
    message: str
    history: Optional[list[dict]] = None  # [{"role": "user"|"assistant", "content": "..."}]
    voice_response: bool = False  # True이면 TTS 응답 포함

class ChatResponse(BaseModel):
    text: str
    audio_base64: Optional[str] = None  # voice_response=True인 경우

@router.post("/ai/chat", response_model=ChatResponse)
async def chat(
    req: ChatRequest,
    session: AsyncSession = Depends(get_session),
):
    """
    LARS AI 어시스턴트 채팅 엔드포인트.
    LLM + Tool 조합으로 한국어 응답 생성.
    """
    llm = get_llm_provider()
    if llm is None:
        raise HTTPException(status_code=503, detail="AI 서비스가 비활성화 상태입니다. 관리자에게 문의하세요.")

    try:
        response_text = await lars_agent.run(
            user_message=req.message,
            session=session,
            llm=llm,
            conversation_history=req.history or [],
        )
    except Exception as e:
        raise HTTPException(status_code=503, detail=f"AI 서비스 오류: {str(e)}")

    audio_b64 = None
    if req.voice_response and response_text:
        audio_bytes = await voice_service.synthesize(response_text)
        if audio_bytes:
            audio_b64 = base64.b64encode(audio_bytes).decode()

    return ChatResponse(text=response_text, audio_base64=audio_b64)


@router.post("/ai/voice/transcribe")
async def transcribe_voice(
    file: UploadFile = File(...),
):
    """
    업로드된 오디오 파일을 텍스트로 변환 (STT).
    지원 포맷: WAV, MP3, WebM (브라우저 MediaRecorder 기본 출력)
    """
    audio_bytes = await file.read()
    ext = file.filename.rsplit(".", 1)[-1] if "." in file.filename else "webm"

    try:
        text = await voice_service.transcribe(audio_bytes, ext=ext)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"STT 오류: {str(e)}")

    if not text.strip():
        raise HTTPException(status_code=422, detail="음성이 인식되지 않았습니다.")

    return {"text": text.strip()}


@router.post("/ai/voice/tts")
async def text_to_speech(body: dict):
    """
    텍스트를 한국어 음성(MP3)으로 변환.
    body: {"text": "...", "voice": "ko-KR-SunHiNeural"}
    """
    text = body.get("text", "")
    voice = body.get("voice", "ko-KR-SunHiNeural")

    if not text:
        raise HTTPException(status_code=400, detail="text is required")

    audio_bytes = await voice_service.synthesize(text, voice=voice)

    if not audio_bytes:
        raise HTTPException(status_code=503, detail="TTS 서비스를 사용할 수 없습니다.")

    return Response(content=audio_bytes, media_type="audio/mpeg")
