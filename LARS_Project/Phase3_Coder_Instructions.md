# Phase 3 Coder Instructions

> 작성자: Project Leader
> 작성일: 2026-04-26
> 대상: Coder (Gemini)
> Phase: 3 — AI 통합 + 이식성 수정

---

## 이 프로젝트의 실행 환경

**중요**: 이 프로젝트는 Ubuntu 컨테이너(IP: 172.17.0.2) 위에서 개발되고 있으며, 다른 컴퓨터의 브라우저에서 원격으로 접속한다. 따라서 **모든 URL은 하드코딩된 localhost를 사용해선 안 된다**. Task 3-A가 이 문제의 영구 해결책이며, 가장 먼저 수행해야 한다.

---

## 사전 확인 사항 (코딩 전 필수)

- `/test/LARS/LARS_Project/New_LARS_Project.md` — LLM Provider 설계, Agent 프롬프트 (Canonical Reference)
- `/test/LARS/backend/models/ticket.py` — Ticket, (Ticket 관련 모델)
- `/test/LARS/backend/models/ai.py` — AgentLog 모델
- `/test/LARS/backend/services/psi_service.py` — `get_shortage_summary()` 함수 (모니터에서 호출)
- `/test/LARS/backend/api/routes/ws.py` — WebSocket manager (AI 알림 브로드캐스트에 사용)
- `/test/LARS/.WebUI/src/api/client.ts` — Axios 클라이언트 (현재 localhost 하드코딩)
- `/test/LARS/.WebUI/src/pages/DashboardPage.tsx` — WebSocket 연결 코드 (localhost 하드코딩)
- `/test/LARS/.WebUI/vite.config.ts` — Vite 설정 (proxy 추가 필요)

---

## Phase 3 완료 기준 (검증 시퀀스)

1. **어떤 PC의 브라우저에서도** `http://<서버IP>:3000` 접속 시 정상 동작
2. `/ai` 페이지에서 텍스트 메시지 전송 → AI가 한국어로 응답
3. `/ai` 페이지 마이크 버튼 → "오늘 PSI 부족 항목 알려줘" 한국어 음성 입력 → AI 한국어 텍스트 응답
4. PSI 모니터가 부족 항목 감지 시 Ticket 자동 생성 → `/tickets` 페이지에서 확인
5. `/tickets` 페이지에서 Ticket 상태 변경 (open → in_progress → resolved) 동작

---

## 전역 제약 조건

- **Polars만 사용** (pandas import 절대 금지)
- **비동기**: 모든 I/O 작업 async 처리
- **LLM 호출**: 반드시 `LLMProvider` 추상 클래스를 통해서만 (직접 httpx/openai 호출 금지)
- **하드코딩 URL 금지**: 프론트엔드에서 `localhost`, `172.17.0.2` 등 IP 직접 사용 금지
- **에러 처리**: LLM/STT/TTS 실패 시 텍스트 fallback 제공 (서비스 중단 없이)

---

## Task 3-A: 이식성 수정 — 가장 먼저 수행

### 문제
1. `client.ts`의 `VITE_API_BASE=http://localhost:8000/api/v1` → 원격 브라우저에서 클라이언트 PC의 localhost로 접속 시도 → 실패
2. `DashboardPage.tsx`의 `ws://localhost:8000/api/v1/ws/dashboard` → 동일 문제
3. 컨테이너 IP를 env에 하드코딩하면 이사할 때마다 수정 필요

### 해결 방식: Vite Dev Server Proxy

브라우저는 Vite 서버(`:3000`)에만 연결. Vite가 `/api`와 `/ws` 경로를 서버 내부의 FastAPI(`:8000`)로 투명하게 전달.

### MODIFY: `.WebUI/vite.config.ts`

기존 `server:` 블록을 아래로 교체:

```typescript
server: {
  host: '0.0.0.0',
  port: 3000,
  hmr: process.env.DISABLE_HMR !== 'true',
  proxy: {
    '/api': {
      target: 'http://localhost:8000',
      changeOrigin: true,
    },
    '/ws': {
      target: 'ws://localhost:8000',
      ws: true,
      changeOrigin: true,
    },
  },
},
```

### MODIFY: `.WebUI/src/api/client.ts`

`API_BASE` 라인을 아래로 교체:

```typescript
const API_BASE = import.meta.env.VITE_API_BASE ?? '/api/v1';
```

### MODIFY: `.WebUI/.env.local`

파일 전체를 아래로 교체:

```
# Vite proxy를 통해 백엔드에 연결 (하드코딩 IP 불필요)
# VITE_API_BASE는 비워두면 상대경로 /api/v1 사용
VITE_API_BASE=
```

### MODIFY: `.WebUI/src/pages/DashboardPage.tsx`

`useEffect` 내부의 WebSocket 연결 코드를 아래로 교체:

```typescript
useEffect(() => {
  const wsProto = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  const ws = new WebSocket(`${wsProto}//${window.location.host}/ws/dashboard`);
  ws.onmessage = () => { refetch(); };
  return () => ws.close();
}, [refetch]);
```

### 검증
```bash
cd /test/LARS/.WebUI && npm run dev &
# 다른 PC 또는 host에서 브라우저로 http://172.17.0.2:3000 접속
# /login 페이지 정상 로드, API 호출 성공 여부 확인 (Network 탭에서 /api/v1/* 200 응답 확인)
```

---

## Task 3-B: 패키지 추가 설치

### Backend requirements.txt에 추가

아래 패키지를 `requirements.txt`의 `# AI` 섹션에 추가:

```
openai>=1.30.0
apscheduler>=3.10.4
edge-tts>=6.1.9
```

설치:
```bash
cd /test/LARS/backend && source venv/bin/activate
pip install openai apscheduler edge-tts
```

**`edge-tts`**: 인터넷 연결 필요, Microsoft Azure TTS API를 무료로 사용. 한국어 고품질 음성 (`ko-KR-SunHiNeural`, `ko-KR-InJoonNeural`). 오프라인 환경에서는 Piper TTS로 교체 가능 (Phase 4).

**`faster-whisper`**: 이미 설치되어 있음. STT에 사용.

### Frontend 패키지 추가 없음
Phase 3 프론트엔드는 기존 패키지(axios, zustand, tanstack-query, react-dropzone)로 충분.

---

## Task 3-C: LLM Provider 추상화 레이어

### CREATE: `backend/llm/__init__.py` (빈 파일)

### CREATE: `backend/llm/base.py`

```python
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
```

### CREATE: `backend/llm/ollama_provider.py`

```python
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
```

### CREATE: `backend/llm/cloud_provider.py`

```python
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
```

### CREATE: `backend/llm/factory.py`

```python
from core.config import get_settings
from llm.base import LLMProvider

_provider: LLMProvider | None = None

def get_llm_provider() -> LLMProvider:
    """
    설정 기반으로 LLM Provider 반환.
    CLOUD_LLM_API_KEY가 설정된 경우 CloudProvider, 없으면 OllamaProvider.
    """
    global _provider
    if _provider is None:
        settings = get_settings()
        if settings.CLOUD_LLM_API_KEY:
            from llm.cloud_provider import CloudProvider
            _provider = CloudProvider()
        else:
            from llm.ollama_provider import OllamaProvider
            _provider = OllamaProvider()
    return _provider
```

---

## Task 3-D: LARS Agent + Tool 정의

### CREATE: `backend/agent/__init__.py` (빈 파일)

### CREATE: `backend/agent/tools.py`

Agent가 호출 가능한 5개 Tool. 각 함수는 DB 세션 없이 독립적으로 호출 가능하도록 설계.

```python
import json
from datetime import date, datetime
from typing import Optional
from sqlalchemy.ext.asyncio import AsyncSession
from services import psi_service, bom_service, ticket_service, daily_plan_service

# Tool 스키마 정의 (LLM에게 전달할 함수 명세)
TOOL_SCHEMAS = [
    {
        "name": "query_psi",
        "description": "PSI(수급 현황)에서 부족 항목을 조회합니다. 특정 날짜를 기준으로 shortage_qty < 0인 품목 목록을 반환합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "as_of_date": {
                    "type": "string",
                    "description": "조회 기준 날짜 (YYYY-MM-DD). 기본값은 오늘."
                }
            }
        }
    },
    {
        "name": "get_bom_tree",
        "description": "특정 모델의 BOM(자재명세서) 정보를 조회합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "model_code": {"type": "string", "description": "조회할 모델 코드"}
            },
            "required": ["model_code"]
        }
    },
    {
        "name": "get_dp_summary",
        "description": "특정 날짜의 일일 생산계획(DP) 요약을 조회합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "plan_date": {"type": "string", "description": "조회할 날짜 (YYYY-MM-DD)"}
            },
            "required": ["plan_date"]
        }
    },
    {
        "name": "create_ticket",
        "description": "업무 티켓을 생성합니다. 수급 부족, 긴급 자재 요청 등 현안 등록에 사용합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "title": {"type": "string", "description": "티켓 제목"},
                "description": {"type": "string", "description": "상세 내용"},
                "priority": {
                    "type": "string",
                    "enum": ["low", "normal", "high", "urgent"],
                    "description": "우선순위"
                },
                "category": {"type": "string", "description": "카테고리 (예: shortage, quality, logistics)"}
            },
            "required": ["title", "description"]
        }
    },
    {
        "name": "list_tickets",
        "description": "현재 열린 티켓 목록을 조회합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "status": {
                    "type": "string",
                    "enum": ["open", "in_progress", "resolved", "all"],
                    "description": "필터할 상태"
                }
            }
        }
    },
]

async def execute_tool(
    tool_name: str,
    tool_args: dict,
    session: AsyncSession
) -> str:
    """Tool 이름과 인자를 받아 실행하고 JSON 문자열로 결과 반환"""
    try:
        if tool_name == "query_psi":
            as_of_date = date.fromisoformat(tool_args.get("as_of_date", str(date.today())))
            result = await psi_service.get_shortage_summary(session, as_of_date)
            if not result:
                return json.dumps({"message": "부족 항목이 없습니다."}, ensure_ascii=False)
            return json.dumps([{
                "part_number": r["part_number"],
                "description": r["description"],
                "date": str(r["psi_date"]),
                "required_qty": r["required_qty"],
                "available_qty": r["available_qty"],
                "shortage_qty": r["shortage_qty"]
            } for r in result], ensure_ascii=False)

        elif tool_name == "get_bom_tree":
            model_code = tool_args["model_code"]
            tree = await bom_service.get_bom_tree(session, model_code)
            if not tree:
                return json.dumps({"error": f"모델 {model_code}을 찾을 수 없습니다."}, ensure_ascii=False)
            return json.dumps({
                "model_code": tree.model.model_code,
                "item_count": len(tree.items),
                "items": [{"level": i.level, "part_number": i.part_number, "description": i.description, "qty": i.qty} for i in tree.items[:20]]
            }, ensure_ascii=False)

        elif tool_name == "get_dp_summary":
            plan_date = date.fromisoformat(tool_args["plan_date"])
            plans = await daily_plan_service.list_plans(session, date_from=plan_date, date_to=plan_date)
            if not plans:
                return json.dumps({"message": f"{plan_date} 날짜의 생산계획이 없습니다."}, ensure_ascii=False)
            return json.dumps(plans, ensure_ascii=False, default=str)

        elif tool_name == "create_ticket":
            ticket = await ticket_service.create_ticket(
                session,
                title=tool_args["title"],
                description=tool_args.get("description", ""),
                priority=tool_args.get("priority", "normal"),
                category=tool_args.get("category"),
                created_by_agent="LARS-Agent"
            )
            return json.dumps({"created_ticket_id": ticket.id, "title": ticket.title}, ensure_ascii=False)

        elif tool_name == "list_tickets":
            status = tool_args.get("status", "open")
            tickets = await ticket_service.list_tickets(session, status=status if status != "all" else None)
            return json.dumps([{
                "id": t.id, "title": t.title, "status": t.status,
                "priority": t.priority, "created_at": str(t.created_at)
            } for t in tickets[:10]], ensure_ascii=False)

        else:
            return json.dumps({"error": f"알 수 없는 Tool: {tool_name}"})
    except Exception as e:
        return json.dumps({"error": str(e)}, ensure_ascii=False)
```

### CREATE: `backend/agent/lars_agent.py`

```python
import json
import re
from sqlalchemy.ext.asyncio import AsyncSession
from llm.base import LLMProvider
from agent.tools import TOOL_SCHEMAS, execute_tool

LARS_SYSTEM_PROMPT = """당신은 LARS(Logistics Agent & Reporting System)의 AI 어시스턴트입니다.
물류, 자재 수급, BOM, 생산계획 관련 질문에 답변합니다.
답변은 항상 한국어로 합니다. 전문적이고 간결하게 답변하세요.

사용 가능한 데이터:
- BOM (자재명세서): 모델별 부품 구성
- DP (일일 생산계획): 라인별 생산 수량
- PSI (수급 현황): 부품 필요량/보유량/부족분
- IT (추적 품목): 관리 대상 부품 마스터
- Ticket: 업무 현안 티켓

Tool을 사용할 때는 다음 JSON 형식으로 응답하세요:
<tool_call>{"name": "tool_name", "arguments": {...}}</tool_call>

Tool 결과를 받으면 한국어로 자연스럽게 해석하여 사용자에게 전달하세요."""

async def run(
    user_message: str,
    session: AsyncSession,
    llm: LLMProvider,
    conversation_history: list[dict] | None = None
) -> str:
    """
    사용자 메시지를 받아 LLM + Tool 조합으로 응답 생성.
    단일 턴: Tool 1회 호출 후 최종 응답 반환 (multi-hop은 Phase 4).
    """
    messages = list(conversation_history or [])
    messages.append({"role": "user", "content": user_message})

    # 1차 LLM 호출 (Tool 사용 여부 판단)
    first_response = await llm.chat(
        messages=messages,
        system=LARS_SYSTEM_PROMPT,
        max_tokens=1024,
    )

    # Tool 호출 추출
    tool_pattern = re.compile(r"<tool_call>(.*?)</tool_call>", re.DOTALL)
    match = tool_pattern.search(first_response)

    if not match:
        return first_response

    # Tool 실행
    try:
        tool_data = json.loads(match.group(1).strip())
        tool_name = tool_data["name"]
        tool_args = tool_data.get("arguments", {})
    except (json.JSONDecodeError, KeyError):
        return first_response

    tool_result = await execute_tool(tool_name, tool_args, session)

    # 2차 LLM 호출 (Tool 결과 기반 최종 응답)
    messages.append({"role": "assistant", "content": first_response})
    messages.append({
        "role": "user",
        "content": f"[Tool '{tool_name}' 실행 결과]\n{tool_result}\n\n위 결과를 바탕으로 사용자에게 한국어로 답변해주세요."
    })

    final_response = await llm.chat(
        messages=messages,
        system=LARS_SYSTEM_PROMPT,
        max_tokens=1024,
    )

    # <tool_call> 태그 제거 후 최종 응답 반환
    return tool_pattern.sub("", final_response).strip()
```

---

## Task 3-E: Voice (STT/TTS) 서비스

### CREATE: `backend/services/voice_service.py`

```python
import os
import tempfile
import asyncio
from faster_whisper import WhisperModel
from core.config import get_settings

_whisper_model: WhisperModel | None = None

def _get_whisper() -> WhisperModel:
    global _whisper_model
    if _whisper_model is None:
        settings = get_settings()
        _whisper_model = WhisperModel(
            settings.WHISPER_MODEL_SIZE,
            device="cpu",
            compute_type="int8"
        )
    return _whisper_model

async def transcribe(audio_bytes: bytes, ext: str = "wav") -> str:
    """
    오디오 바이트를 받아 한국어 텍스트로 변환.
    faster-whisper 사용. 동기 함수를 비동기로 래핑.
    """
    def _run():
        with tempfile.NamedTemporaryFile(suffix=f".{ext}", delete=False) as f:
            f.write(audio_bytes)
            tmp_path = f.name
        try:
            model = _get_whisper()
            segments, _ = model.transcribe(tmp_path, language="ko", beam_size=5)
            return " ".join(s.text.strip() for s in segments)
        finally:
            os.unlink(tmp_path)

    return await asyncio.to_thread(_run)

async def synthesize(text: str, voice: str = "ko-KR-SunHiNeural") -> bytes:
    """
    텍스트를 한국어 음성(MP3 bytes)으로 변환.
    edge-tts 사용 (인터넷 연결 필요).
    실패 시 빈 bytes 반환 (프론트엔드에서 텍스트만 표시).
    """
    try:
        import edge_tts
        communicate = edge_tts.Communicate(text, voice)
        audio_data = b""
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                audio_data += chunk["data"]
        return audio_data
    except Exception as e:
        print(f"[TTS] 음성 합성 실패: {e}")
        return b""
```

---

## Task 3-F: AI Chat + Voice API

### CREATE: `backend/api/routes/ai.py`

```python
import io
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
    try:
        llm = get_llm_provider()
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
        import base64
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
```

### MODIFY: `backend/api/router.py`

`ai` 라우터 등록 추가:

```python
from api.routes import auth, bom, import_pipeline, dp, pl, items, psi, efficiency, wip, dashboard, admin, ai, tickets

# ... 기존 라우터들 ...
router.include_router(ai.router, tags=["ai"])
router.include_router(tickets.router, prefix="/tickets", tags=["tickets"])
```

---

## Task 3-G: Ticket 서비스 + API

### CREATE: `backend/services/ticket_service.py`

```python
from datetime import datetime
from typing import Optional
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.ticket import Ticket

async def create_ticket(
    session: AsyncSession,
    title: str,
    description: str = "",
    priority: str = "normal",
    category: Optional[str] = None,
    related_item_id: Optional[int] = None,
    related_model_id: Optional[int] = None,
    assigned_to: Optional[int] = None,
    created_by_agent: Optional[str] = None,
) -> Ticket:
    ticket = Ticket(
        title=title,
        description=description,
        priority=priority,
        status="open",
        category=category,
        related_item_id=related_item_id,
        related_model_id=related_model_id,
        assigned_to=assigned_to,
        created_by_agent=created_by_agent,
    )
    session.add(ticket)
    await session.commit()
    await session.refresh(ticket)
    return ticket

async def list_tickets(
    session: AsyncSession,
    status: Optional[str] = None,
    priority: Optional[str] = None,
    category: Optional[str] = None,
    limit: int = 50,
) -> list[Ticket]:
    stmt = select(Ticket).order_by(Ticket.created_at.desc()).limit(limit)
    if status:
        stmt = stmt.where(Ticket.status == status)
    if priority:
        stmt = stmt.where(Ticket.priority == priority)
    if category:
        stmt = stmt.where(Ticket.category == category)
    result = await session.execute(stmt)
    return result.scalars().all()

async def update_ticket(
    session: AsyncSession,
    ticket_id: int,
    status: Optional[str] = None,
    assigned_to: Optional[int] = None,
    description: Optional[str] = None,
) -> Optional[Ticket]:
    stmt = select(Ticket).where(Ticket.id == ticket_id)
    result = await session.execute(stmt)
    ticket = result.scalar_one_or_none()
    if not ticket:
        return None
    if status:
        ticket.status = status
        if status == "resolved":
            ticket.resolved_at = datetime.utcnow()
    if assigned_to is not None:
        ticket.assigned_to = assigned_to
    if description is not None:
        ticket.description = description
    ticket.updated_at = datetime.utcnow()
    session.add(ticket)
    await session.commit()
    await session.refresh(ticket)
    return ticket
```

### CREATE: `backend/api/routes/tickets.py`

```python
from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel
from sqlalchemy.ext.asyncio import AsyncSession
from typing import Optional
from core.database import get_session
from core.deps import get_current_user, require_role
from services import ticket_service

router = APIRouter(dependencies=[Depends(get_current_user)])

class TicketCreate(BaseModel):
    title: str
    description: str = ""
    priority: str = "normal"
    category: Optional[str] = None

class TicketUpdate(BaseModel):
    status: Optional[str] = None
    assigned_to: Optional[int] = None
    description: Optional[str] = None

@router.get("")
async def list_tickets(
    status: Optional[str] = Query(None),
    priority: Optional[str] = Query(None),
    session: AsyncSession = Depends(get_session)
):
    tickets = await ticket_service.list_tickets(session, status=status, priority=priority)
    return [
        {
            "id": t.id, "title": t.title, "description": t.description,
            "status": t.status, "priority": t.priority, "category": t.category,
            "created_by_agent": t.created_by_agent,
            "created_at": str(t.created_at), "updated_at": str(t.updated_at),
            "resolved_at": str(t.resolved_at) if t.resolved_at else None,
        }
        for t in tickets
    ]

@router.post("")
async def create_ticket(
    body: TicketCreate,
    session: AsyncSession = Depends(get_session)
):
    ticket = await ticket_service.create_ticket(
        session, title=body.title, description=body.description,
        priority=body.priority, category=body.category
    )
    return {"id": ticket.id, "title": ticket.title, "status": ticket.status}

@router.put("/{ticket_id}")
async def update_ticket(
    ticket_id: int,
    body: TicketUpdate,
    session: AsyncSession = Depends(get_session)
):
    ticket = await ticket_service.update_ticket(
        session, ticket_id=ticket_id,
        status=body.status, assigned_to=body.assigned_to, description=body.description
    )
    if not ticket:
        raise HTTPException(status_code=404, detail="Ticket not found")
    return {"id": ticket.id, "status": ticket.status}
```

---

## Task 3-H: PSI 백그라운드 모니터

### 목적
15분마다 PSI 부족 항목을 체크하여 신규 부족 발생 시 Ticket 자동 생성 + WebSocket 브로드캐스트.

### MODIFY: `backend/main.py`

lifespan 함수를 아래로 교체:

```python
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from api.router import router as api_router
from api.routes.ws import router as ws_router, manager as ws_manager
from core.database import async_engine, get_session_context
from sqlmodel import text
import models  # 모든 모델 로드
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from workers.psi_monitor import check_psi_shortages

scheduler = AsyncIOScheduler(timezone="Asia/Seoul")

@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        async with async_engine.connect() as conn:
            await conn.execute(text("SELECT 1"))
        print("[LARS] DB 연결 성공")
    except Exception as e:
        print(f"[LARS] DB 연결 실패: {e}")

    # PSI 모니터 스케줄 등록 (15분마다)
    scheduler.add_job(
        check_psi_shortages,
        "interval",
        minutes=15,
        id="psi_monitor",
        kwargs={"ws_manager": ws_manager}
    )
    scheduler.start()
    print("[LARS] PSI 모니터 스케줄러 시작")

    yield

    scheduler.shutdown()
    print("[LARS] 스케줄러 종료")
```

### CREATE: `backend/workers/__init__.py` (빈 파일)

### CREATE: `backend/workers/psi_monitor.py`

```python
import json
from datetime import date
from sqlalchemy.ext.asyncio import AsyncSession
from core.database import get_session_context
from services import psi_service, ticket_service

async def check_psi_shortages(ws_manager=None):
    """
    PSI 부족 항목 체크 → 부족 항목 존재 시 Ticket 자동 생성 + WebSocket 브로드캐스트.
    APScheduler에서 15분마다 호출됨.
    """
    today = date.today()
    print(f"[PSI Monitor] 체크 시작: {today}")

    async with get_session_context() as session:
        shortages = await psi_service.get_shortage_summary(session, as_of_date=today)
        if not shortages:
            print("[PSI Monitor] 부족 항목 없음")
            return

        print(f"[PSI Monitor] {len(shortages)}건 부족 항목 감지")

        # 심각한 부족(shortage_qty < -10)만 Ticket 자동 생성
        critical = [s for s in shortages if s.get("shortage_qty", 0) < -10]
        for item in critical:
            title = f"[자동] {item['part_number']} 수급 부족 경보"
            desc = (
                f"품번: {item['part_number']}\n"
                f"품명: {item.get('description', '-')}\n"
                f"날짜: {item['psi_date']}\n"
                f"필요: {item['required_qty']}, 보유: {item.get('available_qty', 0)}, "
                f"부족: {item['shortage_qty']}"
            )
            await ticket_service.create_ticket(
                session,
                title=title,
                description=desc,
                priority="urgent",
                category="shortage",
                created_by_agent="psi-monitor",
            )

        # WebSocket 브로드캐스트
        if ws_manager:
            await ws_manager.broadcast({
                "type": "psi_shortage_alert",
                "shortage_count": len(shortages),
                "critical_count": len(critical),
                "checked_at": str(today),
            })
```

### CREATE: `backend/core/database.py` 수정

`get_session_context` async context manager를 추가 (PSI monitor가 독립적으로 세션 획득):

```python
from contextlib import asynccontextmanager

@asynccontextmanager
async def get_session_context():
    """APScheduler 등 FastAPI 의존성 주입 외부에서 사용할 세션 컨텍스트"""
    async with AsyncSession(async_engine) as session:
        yield session
```

기존 `database.py`의 import 구문 뒤에 위 코드를 추가. 기존 `get_session()` 함수는 그대로 유지.

---

## Task 3-I: 프론트엔드 AI Chat 페이지 완성

### MODIFY: `.WebUI/src/pages/AIChatPage.tsx` (완전 교체)

```typescript
import { useState, useRef, useEffect } from 'react';
import { apiClient } from '../api/client';

interface Message {
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

export default function AIChatPage() {
  const [messages, setMessages] = useState<Message[]>([
    { role: 'assistant', content: '안녕하세요! LARS AI 어시스턴트입니다. 무엇을 도와드릴까요?\n\n예시: "오늘 PSI 부족 항목 알려줘", "LSGL6335X BOM 조회해줘"', timestamp: new Date() }
  ]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [recording, setRecording] = useState(false);
  const [voiceMode, setVoiceMode] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const sendMessage = async (text: string) => {
    if (!text.trim() || loading) return;

    const userMsg: Message = { role: 'user', content: text, timestamp: new Date() };
    const history = messages.slice(-10).map(m => ({ role: m.role, content: m.content }));
    setMessages(prev => [...prev, userMsg]);
    setInput('');
    setLoading(true);

    try {
      const res = await apiClient.post('/ai/chat', {
        message: text,
        history,
        voice_response: voiceMode,
      });

      const { text: responseText, audio_base64 } = res.data;
      setMessages(prev => [...prev, { role: 'assistant', content: responseText, timestamp: new Date() }]);

      // 음성 모드이고 오디오 데이터가 있으면 재생
      if (voiceMode && audio_base64) {
        const audioData = atob(audio_base64);
        const audioArray = new Uint8Array(audioData.length);
        for (let i = 0; i < audioData.length; i++) {
          audioArray[i] = audioData.charCodeAt(i);
        }
        const blob = new Blob([audioArray], { type: 'audio/mpeg' });
        const url = URL.createObjectURL(blob);
        const audio = new Audio(url);
        audio.play();
        audio.onended = () => URL.revokeObjectURL(url);
      }
    } catch (err: any) {
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: `오류가 발생했습니다: ${err.response?.data?.detail || '서버 연결을 확인해주세요.'}`,
        timestamp: new Date()
      }]);
    } finally {
      setLoading(false);
    }
  };

  const startRecording = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const mediaRecorder = new MediaRecorder(stream);
      mediaRecorderRef.current = mediaRecorder;
      audioChunksRef.current = [];

      mediaRecorder.ondataavailable = (e) => {
        if (e.data.size > 0) audioChunksRef.current.push(e.data);
      };

      mediaRecorder.onstop = async () => {
        stream.getTracks().forEach(t => t.stop());
        const blob = new Blob(audioChunksRef.current, { type: 'audio/webm' });
        const formData = new FormData();
        formData.append('file', blob, 'recording.webm');

        try {
          const res = await apiClient.post('/ai/voice/transcribe', formData, {
            headers: { 'Content-Type': 'multipart/form-data' },
          });
          await sendMessage(res.data.text);
        } catch {
          setMessages(prev => [...prev, { role: 'assistant', content: '음성 인식에 실패했습니다.', timestamp: new Date() }]);
        }
      };

      mediaRecorder.start();
      setRecording(true);
    } catch {
      alert('마이크 접근 권한이 필요합니다.');
    }
  };

  const stopRecording = () => {
    mediaRecorderRef.current?.stop();
    setRecording(false);
  };

  const formatTime = (d: Date) =>
    d.toLocaleTimeString('ko-KR', { hour: '2-digit', minute: '2-digit' });

  return (
    <div className="flex flex-col h-full bg-gray-900 rounded-lg overflow-hidden">
      {/* 헤더 */}
      <div className="bg-gray-800 px-4 py-3 flex items-center justify-between border-b border-gray-700">
        <div>
          <h1 className="text-white font-bold">LARS AI 어시스턴트</h1>
          <p className="text-gray-400 text-xs">BOM·DP·PSI·Ticket 조회 및 관리</p>
        </div>
        <label className="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
          <input
            type="checkbox"
            checked={voiceMode}
            onChange={e => setVoiceMode(e.target.checked)}
            className="rounded"
          />
          음성 응답
        </label>
      </div>

      {/* 메시지 영역 */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {messages.map((msg, i) => (
          <div key={i} className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
            <div className={`max-w-[80%] rounded-2xl px-4 py-3 text-sm whitespace-pre-wrap ${
              msg.role === 'user'
                ? 'bg-blue-600 text-white rounded-br-sm'
                : 'bg-gray-700 text-gray-100 rounded-bl-sm'
            }`}>
              {msg.content}
              <div className={`text-xs mt-1 ${msg.role === 'user' ? 'text-blue-200' : 'text-gray-400'}`}>
                {formatTime(msg.timestamp)}
              </div>
            </div>
          </div>
        ))}
        {loading && (
          <div className="flex justify-start">
            <div className="bg-gray-700 text-gray-300 rounded-2xl rounded-bl-sm px-4 py-3 text-sm">
              <span className="animate-pulse">답변 생성 중...</span>
            </div>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* 입력 영역 */}
      <div className="bg-gray-800 border-t border-gray-700 p-3 flex items-end gap-2">
        <textarea
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              sendMessage(input);
            }
          }}
          placeholder="메시지 입력 (Enter 전송, Shift+Enter 줄바꿈)"
          className="flex-1 bg-gray-700 text-white rounded-xl px-4 py-2 text-sm resize-none focus:outline-none focus:ring-1 focus:ring-blue-500"
          rows={2}
          disabled={loading || recording}
        />
        {/* 마이크 버튼 */}
        <button
          onMouseDown={startRecording}
          onMouseUp={stopRecording}
          onTouchStart={startRecording}
          onTouchEnd={stopRecording}
          disabled={loading}
          className={`p-3 rounded-xl transition-all ${
            recording
              ? 'bg-red-600 animate-pulse text-white'
              : 'bg-gray-600 hover:bg-gray-500 text-gray-300'
          }`}
          title="누르고 있는 동안 녹음"
        >
          🎤
        </button>
        {/* 전송 버튼 */}
        <button
          onClick={() => sendMessage(input)}
          disabled={loading || !input.trim() || recording}
          className="px-4 py-3 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white rounded-xl text-sm font-medium transition-colors"
        >
          전송
        </button>
      </div>
    </div>
  );
}
```

---

## Task 3-J: 프론트엔드 Ticket 페이지 완성

### MODIFY: `.WebUI/src/pages/TicketListPage.tsx` (완전 교체)

```typescript
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';

const STATUS_LABELS: Record<string, string> = {
  open: '열림',
  in_progress: '진행 중',
  resolved: '완료',
};

const STATUS_COLORS: Record<string, string> = {
  open: 'bg-yellow-100 text-yellow-800',
  in_progress: 'bg-blue-100 text-blue-800',
  resolved: 'bg-green-100 text-green-800',
};

const PRIORITY_COLORS: Record<string, string> = {
  urgent: 'bg-red-600 text-white',
  high: 'bg-orange-500 text-white',
  normal: 'bg-gray-200 text-gray-700',
  low: 'bg-gray-100 text-gray-500',
};

export default function TicketListPage() {
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [showCreate, setShowCreate] = useState(false);
  const [newTicket, setNewTicket] = useState({ title: '', description: '', priority: 'normal', category: '' });
  const queryClient = useQueryClient();

  const { data: tickets = [], isLoading } = useQuery({
    queryKey: ['tickets', statusFilter],
    queryFn: async () => {
      const params: any = {};
      if (statusFilter) params.status = statusFilter;
      const res = await apiClient.get('/tickets', { params });
      return res.data;
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) =>
      apiClient.put(`/tickets/${id}`, { status }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tickets'] }),
  });

  const createMutation = useMutation({
    mutationFn: () => apiClient.post('/tickets', newTicket),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tickets'] });
      setShowCreate(false);
      setNewTicket({ title: '', description: '', priority: 'normal', category: '' });
    },
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">티켓 관리</h1>
        <button
          onClick={() => setShowCreate(true)}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700"
        >
          + 새 티켓
        </button>
      </div>

      {/* 필터 */}
      <div className="flex gap-2">
        {['', 'open', 'in_progress', 'resolved'].map(s => (
          <button
            key={s}
            onClick={() => setStatusFilter(s)}
            className={`px-3 py-1 rounded-full text-sm ${statusFilter === s ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-600'}`}
          >
            {s === '' ? '전체' : STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {/* 티켓 목록 */}
      {isLoading ? (
        <div className="text-center py-10 text-gray-400">로딩 중...</div>
      ) : tickets.length === 0 ? (
        <div className="text-center py-10 text-gray-400">티켓이 없습니다.</div>
      ) : (
        <div className="space-y-3">
          {tickets.map((t: any) => (
            <div key={t.id} className="bg-white rounded-lg shadow p-4">
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-xs text-gray-400">#{t.id}</span>
                    <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${PRIORITY_COLORS[t.priority] || PRIORITY_COLORS.normal}`}>
                      {t.priority}
                    </span>
                    <span className={`text-xs px-2 py-0.5 rounded-full ${STATUS_COLORS[t.status]}`}>
                      {STATUS_LABELS[t.status]}
                    </span>
                    {t.created_by_agent && (
                      <span className="text-xs px-2 py-0.5 rounded-full bg-purple-100 text-purple-700">
                        🤖 자동 생성
                      </span>
                    )}
                    {t.category && (
                      <span className="text-xs text-gray-500">[{t.category}]</span>
                    )}
                  </div>
                  <p className="font-medium mt-1">{t.title}</p>
                  {t.description && (
                    <p className="text-sm text-gray-500 mt-1 whitespace-pre-line">{t.description}</p>
                  )}
                  <p className="text-xs text-gray-400 mt-2">{new Date(t.created_at).toLocaleString('ko-KR')}</p>
                </div>
                {/* 상태 변경 버튼 */}
                <div className="flex flex-col gap-1 shrink-0">
                  {t.status === 'open' && (
                    <button
                      onClick={() => updateMutation.mutate({ id: t.id, status: 'in_progress' })}
                      className="text-xs px-2 py-1 bg-blue-100 text-blue-700 rounded hover:bg-blue-200"
                    >
                      진행 시작
                    </button>
                  )}
                  {t.status === 'in_progress' && (
                    <button
                      onClick={() => updateMutation.mutate({ id: t.id, status: 'resolved' })}
                      className="text-xs px-2 py-1 bg-green-100 text-green-700 rounded hover:bg-green-200"
                    >
                      완료 처리
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* 티켓 생성 모달 */}
      {showCreate && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl p-6 w-full max-w-md shadow-xl">
            <h2 className="text-lg font-bold mb-4">새 티켓 생성</h2>
            <div className="space-y-3">
              <input
                placeholder="제목 *"
                value={newTicket.title}
                onChange={e => setNewTicket(p => ({ ...p, title: e.target.value }))}
                className="w-full border rounded-lg px-3 py-2 text-sm"
              />
              <textarea
                placeholder="상세 내용"
                value={newTicket.description}
                onChange={e => setNewTicket(p => ({ ...p, description: e.target.value }))}
                className="w-full border rounded-lg px-3 py-2 text-sm"
                rows={3}
              />
              <div className="flex gap-2">
                <select
                  value={newTicket.priority}
                  onChange={e => setNewTicket(p => ({ ...p, priority: e.target.value }))}
                  className="border rounded-lg px-3 py-2 text-sm"
                >
                  <option value="low">낮음</option>
                  <option value="normal">보통</option>
                  <option value="high">높음</option>
                  <option value="urgent">긴급</option>
                </select>
                <input
                  placeholder="카테고리"
                  value={newTicket.category}
                  onChange={e => setNewTicket(p => ({ ...p, category: e.target.value }))}
                  className="flex-1 border rounded-lg px-3 py-2 text-sm"
                />
              </div>
            </div>
            <div className="flex gap-2 mt-4">
              <button
                onClick={() => createMutation.mutate()}
                disabled={!newTicket.title || createMutation.isPending}
                className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm disabled:opacity-50"
              >
                {createMutation.isPending ? '생성 중...' : '생성'}
              </button>
              <button
                onClick={() => setShowCreate(false)}
                className="px-4 py-2 bg-gray-100 text-gray-700 rounded-lg text-sm"
              >
                취소
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
```

---

## Task 3-K: AppLayout 사이드바 Ticket 메뉴 추가

### MODIFY: `.WebUI/src/components/layout/AppLayout.tsx`

사이드바 nav에 Ticket 링크 추가 (AI 어시스턴트 아래):

```typescript
<Link to="/tickets" className="block p-2 rounded hover:bg-gray-800">티켓 관리</Link>
```

---

## Task 3-L: 통합 검증

### 1단계: 백엔드 재기동
```bash
cd /test/LARS/backend && source venv/bin/activate
uvicorn main:app --host 0.0.0.0 --port 8000
# 확인: "[LARS] PSI 모니터 스케줄러 시작" 로그 출력
```

### 2단계: 프론트엔드 재기동
```bash
cd /test/LARS/.WebUI && npm run dev
```

### 3단계: 이식성 검증
```bash
# 다른 터미널 또는 컨테이너 외부에서
curl http://172.17.0.2:3000  # 프론트엔드 응답 확인
curl http://172.17.0.2:3000/api/v1/health  # Vite proxy → 백엔드 확인
# Expected: {"status": "ok", "db": "connected", ...}
```

### 4단계: AI Chat 검증
```bash
# JWT 토큰 획득
TOKEN=$(curl -s -X POST http://172.17.0.2:8000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@lars.local","password":"admin123"}' | python3 -c "import json,sys; print(json.load(sys.stdin)['access_token'])")

# AI 채팅 테스트
curl -X POST http://172.17.0.2:8000/api/v1/ai/chat \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message": "오늘 PSI 부족 항목 알려줘"}'
# Expected: {"text": "...(한국어 응답)...", "audio_base64": null}

# Ticket 생성 테스트
curl -X POST http://172.17.0.2:8000/api/v1/tickets \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title": "테스트 티켓", "description": "Phase 3 검증용", "priority": "normal"}'

curl http://172.17.0.2:8000/api/v1/tickets -H "Authorization: Bearer $TOKEN"
# Expected: 방금 생성한 티켓 포함 목록
```

### 5단계: TypeScript 검증
```bash
cd /test/LARS/.WebUI && npx tsc --noEmit
# Expected: 오류 0건
```

### 6단계: PSI 모니터 수동 트리거 테스트
```python
# Python REPL로 직접 실행 테스트
cd /test/LARS/backend && source venv/bin/activate && python3 -c "
import asyncio
from workers.psi_monitor import check_psi_shortages
asyncio.run(check_psi_shortages(ws_manager=None))
print('PSI 모니터 실행 완료')
"
```

---

## 완료 기준 체크리스트

- [ ] Task 3-A: 다른 PC 브라우저에서 `http://172.17.0.2:3000` 접속 및 API 통신 정상
- [ ] Task 3-A: WebSocket `/ws/dashboard` 동적 URL 적용 (DashboardPage 연결 오류 해소)
- [ ] Task 3-B: 패키지 설치 완료 (openai, apscheduler, edge-tts)
- [ ] Task 3-C: OllamaProvider, CloudProvider 구현 완료, `get_llm_provider()` factory 동작
- [ ] Task 3-D: LARS Agent `run()` 함수 — Tool 포함 한국어 응답 반환
- [ ] Task 3-E: `POST /ai/voice/transcribe` 음성 → 텍스트 변환 성공
- [ ] Task 3-E: `POST /ai/voice/tts` 텍스트 → MP3 bytes 반환 성공
- [ ] Task 3-F: Ticket CRUD API 전체 동작 확인
- [ ] Task 3-G: APScheduler 스케줄러 백엔드 구동 시 자동 시작 확인
- [ ] Task 3-H: AIChatPage — 텍스트 채팅 동작, 음성 녹음 버튼 동작
- [ ] Task 3-I: TicketListPage — Ticket 목록/생성/상태변경 동작
- [ ] Task 3-J: `npx tsc --noEmit` 오류 0건
- [ ] 전체 검증 시퀀스 완료

완료 후 `/test/LARS/LARS_Project/Phase3_Coder_Report.md`를 작성하여 Project Leader에게 보고하라.

---

## 특이사항 및 주의점

1. **Ollama 모델 다운로드**: Ollama가 실행 중이더라도 모델이 없으면 첫 호출 시 에러 발생.
   ```bash
   docker compose exec ollama ollama pull qwen2.5:7b
   # 또는 Ollama가 로컬 설치된 경우
   ollama pull qwen2.5:7b
   ```
   이 다운로드 없이 AI Chat을 테스트하려면 `.env`에 `CLOUD_LLM_API_KEY=<OpenAI-key>`를 설정하면 CloudProvider가 자동 선택됨.

2. **Faster-Whisper 첫 실행**: 첫 실행 시 모델 파일을 자동 다운로드 (medium 모델 약 1.5GB). 오프라인 환경에서는 미리 다운로드 필요.

3. **edge-tts 인터넷 필요**: TTS는 Microsoft 서버 호출. 내부 네트워크만 있는 경우 `synthesize()` 함수가 빈 bytes를 반환하고 프론트엔드는 텍스트만 표시 — 서비스는 중단되지 않음.

4. **APScheduler + asyncio**: FastAPI의 `lifespan` 내에서 `AsyncIOScheduler`를 사용. Uvicorn의 이벤트 루프와 동일한 루프에서 실행되므로 별도 설정 불필요.

5. **main.py의 ws_router 중복 등록 방지**: Phase 2에서 `app.include_router(ws_router)` 하나만 있는지 확인. `api/router.py`에 ws 라우터가 등록되지 않도록 주의.
