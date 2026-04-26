# Coder Task: Phase 3.5 — AI 아키텍처 리팩토링 + 버그 수정

## Context
- Project: LARS (Logistics Agent & Reporting System)
- Working directory: /test/LARS/
- Phase: 3.5 (Phase 3 완료 후 아키텍처 변경 + 긴급 버그 수정)
- Task type: modify_existing + new_file

## 배경 및 변경 이유

Phase 3까지 완료되었으나, 아래 두 가지 이유로 Phase 3.5가 필요하다.

**1. 아키텍처 변경 (AI 서버 분리 결정)**
- 배포 환경: LARS Core는 Synology NAS(저사양)에서 24/7 운영
- AI 추론(LLM/STT/TTS)은 별도 AI PC(RTX 4090)에서 전담
- 기존 OllamaProvider(로컬 직접 호출) → AIServiceProvider(원격 URL 기반)로 교체
- Admin이 AI 모드를 환경변수 하나로 전환 가능하도록 설계

**2. Technical Review 지적 긴급 사항**
- BOM import 시 delete+reinsert 방식 → 데이터 무결성 위험 (PK 재발급)
- 전역 에러 핸들러 누락 → 파싱 실패 시 HTTP 500 반환
- 스케줄러 타임존 하드코딩 → 설정으로 이관 필요

## Prerequisites (코딩 전 반드시 읽을 파일)
- [ ] /test/LARS/backend/core/config.py (현재 Settings 구조 확인)
- [ ] /test/LARS/backend/llm/base.py (LLMProvider ABC)
- [ ] /test/LARS/backend/llm/ollama_provider.py (기존 구현)
- [ ] /test/LARS/backend/llm/factory.py (현재 factory 로직)
- [ ] /test/LARS/backend/services/voice_service.py (현재 구현)
- [ ] /test/LARS/backend/services/bom_service.py (버그 대상)
- [ ] /test/LARS/backend/main.py (스케줄러 설정 확인)
- [ ] /test/LARS/backend/api/routes/admin.py (현재 admin 라우트)
- [ ] /test/LARS/backend/models/bom.py (BomItem 모델 구조 확인)

---

## Task 3.5-A: BOM Upsert 버그 수정 (긴급)

### 문제
`bom_service.py`의 `import_from_df()` 함수에서 기존 BomItem을
`DELETE` 후 `INSERT`하여 매 import마다 PK가 재발급된다.
향후 BomItem.id를 참조하는 테이블이 생기면 데이터 무결성이 파괴된다.

### 수정 대상
**MODIFY: backend/services/bom_service.py**

`import_from_df()` 함수의 BomItem 처리 부분을 교체한다.
BomItem에 UNIQUE 제약이 없으므로 **PostgreSQL ON CONFLICT** 방식 대신
**part_number + model_id + sort_order 조합으로 기존 레코드를 UPDATE하고,
없으면 INSERT하는 수동 upsert**를 구현한다.

```python
async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """
    BOM DataFrame을 DB에 upsert.
    PK를 보존하기 위해 delete+insert 대신 개별 upsert 수행.
    """
    from sqlalchemy import delete
    model_codes = df["model_code"].unique().to_list()
    total_upserted = 0

    for mc in model_codes:
        # BomModel upsert
        stmt = select(BomModel).where(BomModel.model_code == mc)
        res = await session.execute(stmt)
        bom_model = res.scalar_one_or_none()

        if not bom_model:
            bom_model = BomModel(model_code=mc, import_batch_id=batch_id)
            session.add(bom_model)
            await session.flush()
            await session.refresh(bom_model)
        else:
            bom_model.import_batch_id = batch_id
            await session.flush()

        model_df = df.filter(pl.col("model_code") == mc)

        # 기존 items를 {sort_order: BomItem} 딕셔너리로 인덱싱
        existing_stmt = select(BomItem).where(BomItem.model_id == bom_model.id)
        existing_res = await session.execute(existing_stmt)
        existing_items: dict[int, BomItem] = {
            item.sort_order: item for item in existing_res.scalars().all()
        }

        incoming_sort_orders = set()
        for row in model_df.iter_rows(named=True):
            so = row["sort_order"]
            incoming_sort_orders.add(so)
            if so in existing_items:
                # UPDATE: PK 유지
                item = existing_items[so]
                item.level = row["level"]
                item.part_number = row["part_number"]
                item.description = row["description"]
                item.qty = row["qty"]
                item.uom = row["uom"]
                item.vendor_raw = row["vendor_raw"]
                item.supply_type = row["supply_type"]
                item.path = row["path"]
                item.import_batch_id = batch_id
            else:
                # INSERT
                session.add(BomItem(
                    model_id=bom_model.id,
                    level=row["level"],
                    part_number=row["part_number"],
                    description=row["description"],
                    qty=row["qty"],
                    uom=row["uom"],
                    vendor_raw=row["vendor_raw"],
                    supply_type=row["supply_type"],
                    path=row["path"],
                    sort_order=so,
                    import_batch_id=batch_id,
                ))
            total_upserted += 1

        # 삭제된 rows 정리 (import에 없는 sort_order 제거)
        obsolete = set(existing_items.keys()) - incoming_sort_orders
        if obsolete:
            await session.execute(
                delete(BomItem).where(
                    BomItem.model_id == bom_model.id,
                    BomItem.sort_order.in_(list(obsolete))
                )
            )

    await session.commit()
    return total_upserted
```

### 검증
```bash
# 동일 BOM 파일을 2회 import 후 ID 유지 여부 확인
# 1회 import 후 ID 목록 기록, 2회 import 후 동일 ID인지 확인
python -c "
import asyncio
from core.database import get_session_context
from sqlmodel import select
from models.bom import BomItem

async def check():
    async with get_session_context() as s:
        r = await s.execute(select(BomItem).limit(5))
        for i in r.scalars():
            print(i.id, i.sort_order, i.part_number)

asyncio.run(check())
"
```

---

## Task 3.5-B: 전역 에러 핸들러 + config 개선

### 수정 대상 1: backend/core/config.py

기존 Settings에 다음 필드를 추가한다.
기존 필드는 모두 유지하되 AI 관련 설정을 재구성한다.

```python
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
```

### 수정 대상 2: backend/main.py

1. 스케줄러 타임존을 config에서 읽도록 변경
2. 전역 예외 핸들러 추가

```python
from contextlib import asynccontextmanager
from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from api.router import router as api_router
from api.routes.ws import router as ws_router, manager as ws_manager
from core.database import async_engine, get_session_context
from core.config import get_settings
from sqlmodel import text
import models
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from workers.psi_monitor import check_psi_shortages

# ParseError import (parsers에서 정의된 경우)
try:
    from parsers.validator import ParseError
except ImportError:
    ParseError = None

settings = get_settings()
scheduler = AsyncIOScheduler(timezone=settings.SCHEDULER_TIMEZONE)

@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        async with async_engine.connect() as conn:
            await conn.execute(text("SELECT 1"))
        print("[LARS] DB 연결 성공")
    except Exception as e:
        print(f"[LARS] DB 연결 실패: {e}")

    scheduler.add_job(
        check_psi_shortages,
        "interval",
        minutes=settings.PSI_MONITOR_INTERVAL_MINUTES,
        id="psi_monitor",
        kwargs={"ws_manager": ws_manager}
    )
    scheduler.start()
    print(f"[LARS] PSI 모니터 스케줄러 시작 (interval={settings.PSI_MONITOR_INTERVAL_MINUTES}분, tz={settings.SCHEDULER_TIMEZONE})")
    print(f"[LARS] AI 모드: {settings.AI_MODE}")

    yield

    scheduler.shutdown()
    print("[LARS] 스케줄러 종료")

app = FastAPI(title="LARS Platform API", lifespan=lifespan)

# 전역 예외 핸들러
if ParseError is not None:
    @app.exception_handler(ParseError)
    async def parse_error_handler(request: Request, exc: ParseError):
        return JSONResponse(status_code=400, content={"detail": str(exc), "type": "ParseError"})

@app.exception_handler(ValueError)
async def value_error_handler(request: Request, exc: ValueError):
    return JSONResponse(status_code=422, content={"detail": str(exc), "type": "ValueError"})

@app.exception_handler(Exception)
async def generic_error_handler(request: Request, exc: Exception):
    import traceback
    print(f"[LARS] 처리되지 않은 예외: {traceback.format_exc()}")
    return JSONResponse(status_code=500, content={"detail": "서버 내부 오류가 발생했습니다.", "type": type(exc).__name__})

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(api_router)
app.include_router(ws_router)

@app.get("/health")
async def health_check():
    return {"status": "ok", "ai_mode": settings.AI_MODE}
```

---

## Task 3.5-C: LLM Provider 리팩토링 (AI_MODE 기반)

### 수정 대상 1: backend/llm/ai_service_provider.py (신규 생성)

LARS AI Service 또는 Cloud API를 호출하는 단일 프로바이더.
OpenAI SDK의 `base_url` 파라미터를 활용하여 두 모드 모두 처리.

```python
from openai import AsyncOpenAI
from llm.base import LLMProvider

class AIServiceProvider(LLMProvider):
    """
    LARS AI Service(internal) 또는 OpenAI-compatible cloud API를 호출.
    base_url에 따라 자동으로 라우팅된다.
    """

    def __init__(self, base_url: str, model: str, api_key: str = "lars-internal"):
        self._client = AsyncOpenAI(base_url=f"{base_url}/v1", api_key=api_key)
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
```

### 수정 대상 2: backend/llm/factory.py (전체 교체)

```python
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
```

### 수정 대상 3: backend/services/voice_service.py (전체 교체)

```python
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
```

### 수정 대상 4: backend/api/routes/ai.py

기존 코드에서 LLM provider가 None일 경우 처리 추가.
`get_llm_provider()`가 None을 반환할 수 있으므로 분기 처리 필수.

```python
from llm.factory import get_llm_provider

# chat 엔드포인트 내부에:
provider = get_llm_provider()
if provider is None:
    raise HTTPException(status_code=503, detail="AI 서비스가 비활성화 상태입니다. 관리자에게 문의하세요.")
```

---

## Task 3.5-D: LARS AI Service (AI PC용 독립 FastAPI 앱)

### 생성 위치: /test/LARS/lars_ai_service/

이 디렉토리는 AI PC(192.168.0.100)에서 별도로 실행된다.
NAS의 LARS Core와 HTTP로 통신하며, GPU 추론을 전담한다.

### 파일 목록

**CREATE: lars_ai_service/main.py**
```python
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from routes import llm, stt, tts

app = FastAPI(title="LARS AI Service")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(llm.router, prefix="/v1")
app.include_router(stt.router, prefix="/v1")
app.include_router(tts.router, prefix="/v1")

@app.get("/health")
async def health():
    return {"status": "ok", "service": "lars-ai"}
```

**CREATE: lars_ai_service/routes/llm.py**

Ollama `/v1/chat/completions` OpenAI-compatible 엔드포인트를 프록시한다.
Ollama는 포트 11434에서 실행 중이고, OpenAI-compatible API를 지원한다.

```python
import httpx
import os
from fastapi import APIRouter, Request
from fastapi.responses import JSONResponse

router = APIRouter()
OLLAMA_URL = os.getenv("OLLAMA_URL", "http://localhost:11434")

@router.post("/chat/completions")
async def chat_completions(request: Request):
    """Ollama /v1/chat/completions 투명 프록시"""
    body = await request.json()
    async with httpx.AsyncClient(timeout=300.0) as client:
        resp = await client.post(
            f"{OLLAMA_URL}/v1/chat/completions",
            json=body,
        )
        return JSONResponse(content=resp.json(), status_code=resp.status_code)
```

**CREATE: lars_ai_service/routes/stt.py**

```python
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
        os.unlink(tmp_path)
```

**CREATE: lars_ai_service/routes/tts.py**

Piper TTS를 우선 사용하고, 미설치 시 edge-tts로 fallback한다.

```python
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
    # Piper TTS 우선 시도
    try:
        import subprocess, tempfile, asyncio
        with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f:
            out_path = f.name
        proc = await asyncio.create_subprocess_exec(
            "piper",
            "--model", PIPER_VOICE,
            "--output_file", out_path,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        await proc.communicate(input=text.encode("utf-8"))
        with open(out_path, "rb") as f:
            wav_bytes = f.read()
        os.unlink(out_path)
        # WAV → MP3 변환 (ffmpeg 필요)
        proc2 = await asyncio.create_subprocess_exec(
            "ffmpeg", "-i", "pipe:0", "-f", "mp3", "pipe:1",
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
        )
        mp3_bytes, _ = await proc2.communicate(input=wav_bytes)
        return mp3_bytes
    except Exception as e:
        print(f"[TTS] Piper 실패, edge-tts로 fallback: {e}")

    # Fallback: edge-tts
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
```

**CREATE: lars_ai_service/requirements.txt**
```
fastapi>=0.115.0
uvicorn[standard]>=0.30.0
python-multipart
httpx>=0.27.0
faster-whisper>=1.0.0
edge-tts>=6.1.9
```

**CREATE: lars_ai_service/Dockerfile**
```dockerfile
FROM python:3.11-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# piper 바이너리 설치
RUN apt-get update && apt-get install -y wget ffmpeg && \
    wget -O /usr/local/bin/piper https://github.com/rhasspy/piper/releases/latest/download/piper_linux_x86_64 && \
    chmod +x /usr/local/bin/piper

# Piper 한국어 모델 (빌드 시 포함하지 않음 — 볼륨 마운트 권장)
EXPOSE 8088

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8088"]
```

**CREATE: lars_ai_service/.env.example**
```
OLLAMA_URL=http://localhost:11434
WHISPER_MODEL=large-v3
PIPER_VOICE=ko_KR-kss-medium
```

**CREATE: lars_ai_service/routes/__init__.py** (빈 파일)

**CREATE: docker-compose.ai.yml** (프로젝트 루트 /test/LARS/)

```yaml
# LARS AI Service — AI PC(192.168.0.100)에서 실행
# 사전 요건: Docker Desktop + NVIDIA Container Toolkit 설치, Ollama 별도 실행
version: "3.9"

services:
  lars-ai:
    build: ./lars_ai_service
    container_name: lars-ai-service
    ports:
      - "8088:8088"
    environment:
      - OLLAMA_URL=http://host.docker.internal:11434
      - WHISPER_MODEL=large-v3
      - PIPER_VOICE=ko_KR-kss-medium
    volumes:
      - piper_models:/root/.local/share/piper
      - whisper_models:/root/.cache/huggingface
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8088/health"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  piper_models:
  whisper_models:
```

### LARS AI Service 실행 방법 (AI PC에서)

```bash
# 1. Ollama 설치 및 모델 다운로드 (Windows — https://ollama.com)
ollama pull qwen2.5:32b

# 2. LARS AI Service 실행 (Docker 없이 직접 실행 방법)
cd /path/to/LARS/lars_ai_service
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8088

# 3. 또는 Docker Compose로 실행 (NVIDIA Container Toolkit 필요)
cd /test/LARS
docker-compose -f docker-compose.ai.yml up -d

# 4. 헬스체크
curl http://192.168.0.100:8088/health
```

---

## Task 3.5-E: Admin AI 설정 API + 간단한 UI

### 수정 대상 1: backend/api/routes/admin.py

기존 admin.py에 아래 엔드포인트를 추가한다.
(기존 user 관련 엔드포인트는 건드리지 않음)

```python
# admin.py 하단에 추가

import httpx
from core.config import get_settings
from llm.factory import reset_provider

class AIConfigUpdate(BaseModel):
    ai_mode: str  # disabled | local | internal | cloud
    ai_service_url: str | None = None
    cloud_llm_base_url: str | None = None
    cloud_llm_model: str | None = None
    cloud_llm_api_key: str | None = None
    local_llm_model: str | None = None

@router.get("/ai-config", dependencies=[Depends(require_role(["admin"]))])
async def get_ai_config():
    s = get_settings()
    return {
        "ai_mode": s.AI_MODE,
        "ai_service_url": s.AI_SERVICE_URL,
        "cloud_llm_base_url": s.CLOUD_LLM_BASE_URL,
        "cloud_llm_model": s.CLOUD_LLM_MODEL,
        "cloud_api_key_set": bool(s.CLOUD_LLM_API_KEY),
        "local_llm_model": s.LOCAL_LLM_MODEL,
    }

@router.post("/ai-config/test", dependencies=[Depends(require_role(["admin"]))])
async def test_ai_connection():
    """현재 AI_MODE 설정으로 연결 테스트"""
    s = get_settings()

    if s.AI_MODE == "disabled":
        return {"success": False, "message": "AI 모드가 비활성화 상태입니다."}

    if s.AI_MODE == "internal":
        try:
            async with httpx.AsyncClient(timeout=10.0) as client:
                resp = await client.get(f"{s.AI_SERVICE_URL}/health")
                if resp.status_code == 200:
                    return {"success": True, "message": f"LARS AI Service 연결 성공: {s.AI_SERVICE_URL}"}
                return {"success": False, "message": f"응답 코드: {resp.status_code}"}
        except Exception as e:
            return {"success": False, "message": f"연결 실패: {e}"}

    if s.AI_MODE == "local":
        try:
            async with httpx.AsyncClient(timeout=5.0) as client:
                resp = await client.get(f"{s.OLLAMA_URL}/api/tags")
                if resp.status_code == 200:
                    models = [m["name"] for m in resp.json().get("models", [])]
                    return {"success": True, "message": f"Ollama 연결 성공. 모델: {models}"}
        except Exception as e:
            return {"success": False, "message": f"Ollama 연결 실패: {e}"}

    if s.AI_MODE == "cloud":
        return {"success": bool(s.CLOUD_LLM_API_KEY), "message": "API Key " + ("설정됨" if s.CLOUD_LLM_API_KEY else "미설정")}

    return {"success": False, "message": "알 수 없는 AI 모드"}
```

### 수정 대상 2: .WebUI/src/pages/AdminPage.tsx

기존 AdminPage에 AI 설정 섹션을 추가한다.
(기존 사용자 관리 UI는 유지)

```tsx
// AdminPage의 JSX 하단 (또는 탭 구조가 있다면 별도 탭)에 추가

const [aiConfig, setAiConfig] = useState<any>(null);
const [testResult, setTestResult] = useState<{success: boolean; message: string} | null>(null);

// useEffect로 GET /admin/ai-config 호출하여 aiConfig 로드

// UI 섹션:
<div className="bg-white rounded-lg shadow p-6 mt-6">
  <h2 className="text-lg font-bold mb-4">AI 서비스 설정</h2>
  {/* AI 모드 표시 (읽기 전용) */}
  <div className="grid grid-cols-2 gap-4 text-sm">
    <div><span className="text-gray-500">현재 모드:</span>
      <span className={`ml-2 px-2 py-0.5 rounded text-xs font-medium ${
        aiConfig?.ai_mode === 'disabled' ? 'bg-gray-100 text-gray-600' :
        aiConfig?.ai_mode === 'internal' ? 'bg-green-100 text-green-700' :
        aiConfig?.ai_mode === 'cloud' ? 'bg-blue-100 text-blue-700' :
        'bg-yellow-100 text-yellow-700'
      }`}>{aiConfig?.ai_mode ?? '로딩 중'}</span>
    </div>
    {aiConfig?.ai_mode === 'internal' && (
      <div><span className="text-gray-500">AI Service URL:</span>
        <span className="ml-2 font-mono text-xs">{aiConfig.ai_service_url}</span>
      </div>
    )}
  </div>
  <button
    onClick={async () => {
      const res = await apiClient.post('/admin/ai-config/test');
      setTestResult(res.data);
    }}
    className="mt-4 px-4 py-2 bg-blue-600 text-white rounded text-sm hover:bg-blue-700"
  >
    연결 테스트
  </button>
  {testResult && (
    <div className={`mt-3 p-3 rounded text-sm ${testResult.success ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
      {testResult.success ? '✓ ' : '✗ '}{testResult.message}
    </div>
  )}
  <p className="mt-4 text-xs text-gray-400">
    AI 모드 변경은 .env 파일에서 AI_MODE 값을 수정하고 서버를 재시작하세요.
  </p>
</div>
```

---

## Task 3.5-F: backend/.env 업데이트

**MODIFY: backend/.env** (실제 환경에 맞게 수정)

```env
# Database
DATABASE_URL=postgresql+asyncpg://lars:lars_password@localhost:5432/lars_db
REDIS_URL=redis://localhost:6379

# Auth
JWT_SECRET_KEY=your-secret-key-change-in-production

# AI 모드 설정
# disabled: AI 기능 끄기 (NAS 기본)
# local: 동일 머신 Ollama (개발용)
# internal: LARS AI Service (운영 권장)
# cloud: OpenAI 등 외부 API
AI_MODE=internal

# LARS AI Service (AI_MODE=internal일 때 사용)
AI_SERVICE_URL=http://192.168.0.100:8088

# 로컬 Ollama (AI_MODE=local일 때)
OLLAMA_URL=http://localhost:11434
LOCAL_LLM_MODEL=qwen2.5:32b

# 클라우드 (AI_MODE=cloud일 때)
CLOUD_LLM_BASE_URL=https://api.openai.com/v1
CLOUD_LLM_MODEL=gpt-4o
CLOUD_LLM_API_KEY=

# 스케줄러
SCHEDULER_TIMEZONE=Asia/Seoul
PSI_MONITOR_INTERVAL_MINUTES=15

# Whisper (AI_MODE=local일 때만 사용)
WHISPER_MODEL_SIZE=medium
```

---

## 생성/수정할 파일 전체 목록

```
CREATE: backend/llm/ai_service_provider.py
MODIFY: backend/llm/factory.py         ← get_llm_provider() 반환 타입 변경 + reset_provider() 추가
MODIFY: backend/core/config.py         ← AI_MODE + 스케줄러 설정 추가
MODIFY: backend/main.py                ← 전역 에러핸들러 + config 기반 스케줄러
MODIFY: backend/services/voice_service.py  ← AI_MODE 기반 분기
MODIFY: backend/services/bom_service.py    ← PK 보존 upsert
MODIFY: backend/api/routes/admin.py    ← ai-config + test 엔드포인트 추가
MODIFY: .WebUI/src/pages/AdminPage.tsx ← AI 설정 섹션 추가
MODIFY: backend/.env                   ← AI_MODE=internal 설정

CREATE: lars_ai_service/main.py
CREATE: lars_ai_service/routes/__init__.py
CREATE: lars_ai_service/routes/llm.py
CREATE: lars_ai_service/routes/stt.py
CREATE: lars_ai_service/routes/tts.py
CREATE: lars_ai_service/requirements.txt
CREATE: lars_ai_service/Dockerfile
CREATE: lars_ai_service/.env.example

CREATE: docker-compose.ai.yml          ← 프로젝트 루트
```

**DO NOT TOUCH:**
- backend/agent/ (lars_agent.py, tools.py)
- backend/workers/psi_monitor.py
- backend/parsers/
- backend/models/
- backend/api/routes/ai.py (단, provider None 체크만 추가)
- backend/api/routes/tickets.py
- .WebUI/src/pages/ (AdminPage 제외)
- vite.config.ts

---

## 검증 방법

```bash
# 1. 백엔드 타입/문법 확인
cd /test/LARS/backend
python -c "from core.config import get_settings; s = get_settings(); print('AI_MODE:', s.AI_MODE)"

# 2. factory 동작 확인 (disabled 모드)
python -c "
import os; os.environ['AI_MODE'] = 'disabled'
from llm.factory import get_llm_provider
p = get_llm_provider()
assert p is None, 'disabled 모드에서 None이어야 함'
print('OK: disabled 모드 None 반환')
"

# 3. BOM upsert 검증 (2회 import 후 ID 변화 없어야 함)
# (DB 실행 중인 상태에서)
python -c "
import asyncio
from core.database import get_session_context
from sqlmodel import select
from models.bom import BomItem

async def check():
    async with get_session_context() as s:
        r = await s.execute(select(BomItem).limit(3))
        for i in r.scalars():
            print(f'id={i.id} sort_order={i.sort_order} part={i.part_number}')

asyncio.run(check())
"

# 4. LARS AI Service 독립 실행 테스트 (AI PC에서)
cd /test/LARS/lars_ai_service
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8088 &
curl http://localhost:8088/health  # {"status":"ok","service":"lars-ai"}

# 5. Admin AI 연결 테스트 API (백엔드 실행 중)
curl -X POST http://localhost:8000/api/v1/admin/ai-config/test \
  -H "Authorization: Bearer {admin_token}"

# 6. 프론트엔드 타입 체크
cd /test/LARS/.WebUI
npx tsc --noEmit
```

---

## 완료 기준

- [ ] backend/llm/ai_service_provider.py 생성 완료
- [ ] factory.py: AI_MODE=disabled 시 None 반환
- [ ] config.py: AI_MODE, AI_SERVICE_URL, SCHEDULER_TIMEZONE 필드 포함
- [ ] main.py: 전역 에러 핸들러 3개 등록, 스케줄러 config 기반
- [ ] voice_service.py: AI_MODE 분기 처리 (faster-whisper NAS에서 로드하지 않음)
- [ ] bom_service.py: PK 보존 upsert (sort_order 기준 UPDATE/INSERT/DELETE)
- [ ] lars_ai_service/ 디렉토리 및 7개 파일 생성
- [ ] docker-compose.ai.yml 생성 (프로젝트 루트)
- [ ] admin.py: GET /admin/ai-config, POST /admin/ai-config/test 추가
- [ ] AdminPage.tsx: AI 설정 섹션 + 연결 테스트 버튼
- [ ] npx tsc --noEmit 오류 0건
- [ ] python 문법 오류 없음

---

## 에러 처리 지침

- `get_llm_provider()`가 None을 반환하는 경우: 호출부(ai.py 라우트)에서 HTTP 503 반환
- LARS AI Service 연결 실패: 서버 로그에 경고 출력 후 HTTP 503 반환 (서버 crash 금지)
- BOM upsert 중 DB 에러: session.rollback() 후 HTTPException(500) 반환
- LARS AI Service의 STT/TTS 실패: 빈 문자열/빈 bytes 반환 (AI 실패가 전체 채팅을 막으면 안 됨)
