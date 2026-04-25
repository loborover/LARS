# LARS Platform: System Design Blueprint

> 작성일: 2026-04-23  
> 개정일: 2026-04-26 (VBA 마이그레이션 방향 폐기 → 신규 업무 기반 설계로 전면 재수립)  
> 상태: v3 — Project Leader 승인 완료, Phase 1 구현 대기 중  
> Canonical Reference: 이 문서가 모든 Coder 지시문의 기준 문서임

---

## 1. 현황 평가 및 자산 처리 방향

### 1.1 기존 자산 처리

| 구분 | 경로 | 상태 | 처리 방향 |
|---|---|---|---|
| VBA 원본 코드 | `VBA/` | 완전 분석 완료 | **참조 유지** (마이그레이션 대상 아님) |
| Python FastAPI 스켈레톤 | `backend/` | 뼈대만 존재 | **전면 재작성** |
| React WebUI | `.WebUI/` | Mock 데이터 연결 상태 | **유지 + 실데이터 연결** |
| C# TestHarness | `TestSet/TestHarness/` | 구 분석용 임시 코드 | **전면 삭제** |
| LARS.sln | `LARS.sln` | C# 솔루션 파일 | **삭제** |
| 기획/아키텍처 문서 | `LARS_Project/` | 본 문서로 통합 | **유지 + 본 문서 기준** |
| Excel 테스트 데이터 | `TestSet/`, `data/` | 실제 업무 데이터 | **유지 (Import 검증용)** |
| Agent 프롬프트 시스템 | `.agent/` | 운영 중 | **유지 (변경 금지)** |

### 1.2 설계 방향 전환 배경

VBA 로직의 직접 마이그레이션은 기술적으로 가능하나 전략적으로 비효율적이다.
VBA는 실무 업무 로직의 **참조 자료**로만 활용하고, 실제 비즈니스 요구사항을 바탕으로 시스템을 신규 설계한다.

**핵심 비즈니스 요구사항:** BOM 조회, 일일 생산계획 관리, 자재수급 일정 조율 및 공급 일정 확정 업무를 웹 기반으로 디지털화하고, AI 자연어 인터페이스로 접근성을 높인다.

---

## 2. 핵심 설계 원칙

1. **AI 모델 종속성 없음**: 특정 LLM Provider에 의존하는 코드를 작성하지 않는다. 모든 LLM 호출은 `LLMProvider` 추상 레이어를 경유한다.
2. **Local-First AI**: 음성 전사, 현황 체크, 단순 조회는 로컬 모델이 처리한다. 인터넷 없이도 핵심 기능이 동작해야 한다.
3. **Cloud AI는 복잡한 추론 전용**: 다중 문서 교차 분석, 전략적 판단 등 고난도 작업만 Cloud LLM에 위임한다.
4. **역할 기반 프롬프트**: 각 Agent 역할마다 독립적인 System Prompt를 보유하며, LLM이 교체되어도 동일한 역할 행동을 보장한다.
5. **Polars 전용 데이터 처리**: 모든 DataFrame 연산에서 Pandas 사용 금지.
6. **Import 우선, Auto-sync 확장**: 수동 Excel/CSV import가 현재 방식이며, 향후 사내 DB 자동 연동으로 확장 가능한 구조를 사전에 설계한다.
7. **웹 서버 형태, 반응형**: 데스크톱·태블릿·스마트폰에서 무리 없이 사용 가능해야 한다.
8. **버전 관리 책임**: AI는 코드 작성만 담당, `git commit/push`는 사용자 전담.

---

## 3. 핵심 비즈니스 모듈 (8개)

| 모듈 | 한국어 명칭 | 설명 |
|---|---|---|
| **BOM** | 자재명세서 | 모델별 계층형 부품 구성 (Level, PartNumber, Description, Qty, UOM, Vendor) |
| **DP** | 일일 생산계획 | 날짜·라인별 생산 Lot 계획 (Date, Line, Model, Lot, Qty) |
| **PL** | PartList | BOM × DP 기반 로트별 부품 소모량 보고서 |
| **IT** | ItemMaster | 사용자별 추적 품목 마스터 (Level, Description, PartNumber, Vendor, 담당자) |
| **fnBOM_Reverse** | BOM 역조회 | 특정 Part가 어떤 Model에 사용되는지 역조회 |
| **PSI** | 공급망 지수 | 행=Part(IT기준), 열=Date, 값=필요량/보유량 행렬 |
| **물류효율표** | 물류효율 | 작업자×품목별 효율 추적 (현재 수동, 향후 실시간) |
| **표준재공표** | 표준재공 | 공장 Location × Target Part 위치 정보 |

---

## 4. 확정 기술 스택

### 4.1 Backend

| 역할 | 기술 | 선택 이유 |
|---|---|---|
| API Framework | **FastAPI** | 비동기 I/O, OpenAPI 자동 생성, Tool Calling 스키마 호환 |
| 데이터 처리 | **Polars** | Rust 기반, Excel/CSV 대용량 처리, pandas 금지 |
| RDBMS | **PostgreSQL 16** | 트랜잭션, JSON, pgvector 확장 |
| Vector Search | **pgvector** | 별도 Vector DB 없이 자연어 검색 내재화 |
| 캐시 / Broker | **Redis** | BOM 트리 캐싱(TTL 1시간), Celery 브로커 |
| 비동기 작업 | **Celery** | Excel 파싱, PL/PSI 재계산 백그라운드 처리 |
| ORM | **SQLModel** | SQLAlchemy + Pydantic 통합 |
| DB 마이그레이션 | **Alembic** | 스키마 버전 관리 |
| Auth | **JWT (python-jose) + bcrypt** | RBAC: admin/manager/internal/partner/viewer |

### 4.2 AI Layer (모델 무관 구조)

| 역할 | 기술 | 설명 |
|---|---|---|
| **LLM 추상 레이어** | 자체 구현 `LLMProvider` | 모든 LLM 호출의 단일 인터페이스 |
| **Local STT** | **Faster-Whisper** | 오픈소스 음성→텍스트, GPU/CPU 모두 동작 |
| **Local LLM** | **Ollama** | Llama 3.2, Qwen2.5, Gemma3 등 로컬 실행 |
| **Local TTS** | **Piper TTS** | 오픈소스 텍스트→음성, 한국어 모델 지원 |
| **Cloud LLM** | OpenAI API 호환 포맷 | 어떤 Cloud Provider도 동일 인터페이스 사용 |
| **임베딩** | **nomic-embed-text** (Local) | pgvector 저장, 자연어 검색 |
| **VoIP** | **PJSIP + SIP.js** | 사내 전화망 연동, 통화 녹음 후 Whisper 전사 |

### 4.3 Frontend

| 역할 | 기술 |
|---|---|
| Framework | React 18 + Vite + TypeScript (기존 `.WebUI` 확장) |
| UI | shadcn/ui (기존 컴포넌트 재활용) |
| 라우팅 | **react-router-dom v6** |
| 서버 상태 | **TanStack Query (React Query v5)** |
| 클라이언트 상태 | **Zustand** |
| HTTP 클라이언트 | **Axios** (JWT 인터셉터 포함) |
| 실시간 업데이트 | **WebSocket** (FastAPI WebSocket 엔드포인트) |
| 파일 업로드 | **react-dropzone** |

---

## 5. 데이터베이스 스키마 (전체)

### 설계 원칙
- `BIGSERIAL PRIMARY KEY` — 모든 테이블 공통
- `TIMESTAMPTZ` — 모든 시간 컬럼
- `is_active BOOLEAN DEFAULT TRUE` — 마스터 테이블 소프트 삭제
- `import_batch_id BIGINT` — 모든 imported 데이터 테이블 (auto-sync 확장 기반)
- `shortage_qty` — GENERATED ALWAYS AS 컬럼 (데이터 일관성 자동 보장)

### 5.1 마스터 테이블

```sql
-- 공급업체
CREATE TABLE vendors (
    id          BIGSERIAL PRIMARY KEY,
    code        TEXT NOT NULL UNIQUE,
    name        TEXT NOT NULL,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    updated_at  TIMESTAMPTZ DEFAULT NOW()
);

-- 사용자 (내부 + 외부 협력사)
CREATE TABLE users (
    id           BIGSERIAL PRIMARY KEY,
    email        TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL,
    role         TEXT NOT NULL DEFAULT 'viewer',  -- admin|manager|internal|partner|viewer
    is_active    BOOLEAN NOT NULL DEFAULT TRUE,
    hashed_pw    TEXT NOT NULL,
    created_at   TIMESTAMPTZ DEFAULT NOW(),
    updated_at   TIMESTAMPTZ DEFAULT NOW()
);

-- 생산 라인
CREATE TABLE production_lines (
    id        BIGSERIAL PRIMARY KEY,
    code      TEXT NOT NULL UNIQUE,
    name      TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);
```

### 5.2 BOM 모듈

```sql
-- BOM 모델 헤더
CREATE TABLE bom_models (
    id              BIGSERIAL PRIMARY KEY,
    model_code      TEXT NOT NULL UNIQUE,
    description     TEXT,
    version         TEXT NOT NULL DEFAULT '1.0',
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    import_batch_id BIGINT,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- BOM 라인 항목 (path 컬럼으로 트리 구조 — VBA Lvl 방식 호환)
CREATE TABLE bom_items (
    id              BIGSERIAL PRIMARY KEY,
    model_id        BIGINT NOT NULL REFERENCES bom_models(id) ON DELETE CASCADE,
    level           SMALLINT NOT NULL,        -- VBA "Lvl"
    part_number     TEXT NOT NULL,            -- VBA "Part No"
    description     TEXT,                     -- VBA "Description"
    qty             NUMERIC(10,4) NOT NULL DEFAULT 1,
    uom             TEXT NOT NULL DEFAULT 'EA',
    vendor_id       BIGINT REFERENCES vendors(id) ON DELETE SET NULL,
    vendor_raw      TEXT,                     -- import 원본 텍스트
    supply_type     TEXT,
    path            TEXT NOT NULL,            -- 계층 경로: "0.1.3" (서브트리 쿼리용)
    sort_order      INT NOT NULL DEFAULT 0,
    import_batch_id BIGINT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_bom_items_model_id ON bom_items(model_id);
CREATE INDEX idx_bom_items_part_number ON bom_items(part_number);
CREATE INDEX idx_bom_items_path ON bom_items(path);
CREATE INDEX idx_bom_items_part_model ON bom_items(part_number, model_id);  -- fnBOM_Reverse용
```

**fnBOM_Reverse 쿼리:**
```sql
SELECT DISTINCT bm.model_code, bm.description
FROM bom_items bi JOIN bom_models bm ON bi.model_id = bm.id
WHERE bi.part_number = :part_number AND bm.is_active = TRUE;
```

### 5.3 DP 모듈

```sql
-- 일별 생산계획 헤더
CREATE TABLE daily_plans (
    id              BIGSERIAL PRIMARY KEY,
    plan_date       DATE NOT NULL,
    line_id         BIGINT NOT NULL REFERENCES production_lines(id),
    import_batch_id BIGINT,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (plan_date, line_id)
);

-- Lot 단위 계획
CREATE TABLE daily_plan_lots (
    id              BIGSERIAL PRIMARY KEY,
    plan_id         BIGINT NOT NULL REFERENCES daily_plans(id) ON DELETE CASCADE,
    wo_number       TEXT,
    model_id        BIGINT REFERENCES bom_models(id) ON DELETE SET NULL,
    model_code      TEXT NOT NULL,
    lot_number      TEXT NOT NULL,
    planned_qty     INT NOT NULL DEFAULT 0,
    input_qty       INT NOT NULL DEFAULT 0,
    output_qty      INT NOT NULL DEFAULT 0,
    planned_start   TIMESTAMPTZ,
    sort_order      INT NOT NULL DEFAULT 0,
    import_batch_id BIGINT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_daily_plan_lots_plan_id ON daily_plan_lots(plan_id);
CREATE INDEX idx_daily_plan_lots_model ON daily_plan_lots(model_code);
CREATE INDEX idx_daily_plans_date_line ON daily_plans(plan_date, line_id);
```

### 5.4 PL 스냅샷 (BOM × DP 계산 결과 materialized)

```sql
CREATE TABLE part_list_snapshots (
    id              BIGSERIAL PRIMARY KEY,
    lot_id          BIGINT NOT NULL REFERENCES daily_plan_lots(id) ON DELETE CASCADE,
    part_number     TEXT NOT NULL,
    description     TEXT,
    uom             TEXT NOT NULL DEFAULT 'EA',
    vendor_id       BIGINT REFERENCES vendors(id),
    vendor_raw      TEXT,
    required_qty    NUMERIC(12,4) NOT NULL,   -- bom_qty * lot.planned_qty
    snapshot_date   DATE NOT NULL,
    import_batch_id BIGINT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_pl_snapshot_lot_id ON part_list_snapshots(lot_id);
CREATE INDEX idx_pl_snapshot_date ON part_list_snapshots(snapshot_date);
CREATE INDEX idx_pl_snapshot_part ON part_list_snapshots(part_number);
```

### 5.5 IT (Item Master)

```sql
CREATE TABLE item_master (
    id               BIGSERIAL PRIMARY KEY,
    level            SMALLINT NOT NULL DEFAULT 1,
    description      TEXT NOT NULL,
    part_number      TEXT NOT NULL UNIQUE,
    vendor_id        BIGINT REFERENCES vendors(id) ON DELETE SET NULL,
    vendor_raw       TEXT,
    tracking_user_id BIGINT REFERENCES users(id) ON DELETE SET NULL,
    is_active        BOOLEAN NOT NULL DEFAULT TRUE,
    import_batch_id  BIGINT,
    created_at       TIMESTAMPTZ DEFAULT NOW(),
    updated_at       TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_item_master_part ON item_master(part_number);
CREATE INDEX idx_item_master_user ON item_master(tracking_user_id);
```

### 5.6 PSI

```sql
CREATE TABLE psi_records (
    id              BIGSERIAL PRIMARY KEY,
    item_id         BIGINT NOT NULL REFERENCES item_master(id) ON DELETE CASCADE,
    psi_date        DATE NOT NULL,
    required_qty    NUMERIC(12,4) NOT NULL DEFAULT 0,   -- BOM×DP 계산
    available_qty   NUMERIC(12,4),                       -- 수동 입력
    shortage_qty    NUMERIC(12,4) GENERATED ALWAYS AS
                    (COALESCE(required_qty, 0) - COALESCE(available_qty, 0)) STORED,
    notes           TEXT,
    last_updated_by BIGINT REFERENCES users(id),
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (item_id, psi_date)
);

CREATE INDEX idx_psi_item_date ON psi_records(item_id, psi_date);
CREATE INDEX idx_psi_date ON psi_records(psi_date);
CREATE INDEX idx_psi_shortage ON psi_records(shortage_qty) WHERE shortage_qty > 0;
```

### 5.7 물류효율표

```sql
CREATE TABLE workers (
    id          BIGSERIAL PRIMARY KEY,
    name        TEXT NOT NULL,
    employee_id TEXT UNIQUE,
    line_id     BIGINT REFERENCES production_lines(id),
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE logistics_efficiency (
    id              BIGSERIAL PRIMARY KEY,
    worker_id       BIGINT NOT NULL REFERENCES workers(id) ON DELETE CASCADE,
    item_id         BIGINT NOT NULL REFERENCES item_master(id) ON DELETE CASCADE,
    model_id        BIGINT REFERENCES bom_models(id) ON DELETE SET NULL,
    recorded_date   DATE NOT NULL,
    target_qty      NUMERIC(10,2),
    actual_qty      NUMERIC(10,2),
    efficiency_rate NUMERIC(5,4) GENERATED ALWAYS AS
                    (CASE WHEN target_qty > 0 THEN actual_qty / target_qty ELSE NULL END) STORED,
    notes           TEXT,
    is_realtime     BOOLEAN NOT NULL DEFAULT FALSE,  -- False=수동, True=향후 실시간
    import_batch_id BIGINT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_efficiency_worker ON logistics_efficiency(worker_id);
CREATE INDEX idx_efficiency_item ON logistics_efficiency(item_id);
CREATE INDEX idx_efficiency_date ON logistics_efficiency(recorded_date);
```

### 5.8 표준재공표

```sql
CREATE TABLE factory_locations (
    id        BIGSERIAL PRIMARY KEY,
    code      TEXT NOT NULL UNIQUE,
    name      TEXT NOT NULL,
    zone      TEXT,
    x_coord   NUMERIC(8,2),
    y_coord   NUMERIC(8,2),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE standard_wip (
    id              BIGSERIAL PRIMARY KEY,
    item_id         BIGINT NOT NULL REFERENCES item_master(id) ON DELETE CASCADE,
    location_id     BIGINT NOT NULL REFERENCES factory_locations(id) ON DELETE CASCADE,
    target_qty      NUMERIC(10,2) NOT NULL DEFAULT 0,
    safety_stock    NUMERIC(10,2),
    notes           TEXT,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    import_batch_id BIGINT,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (item_id, location_id)
);
```

### 5.9 공통 지원 테이블

```sql
-- Import 이력 (auto-sync 확장 기반)
CREATE TABLE import_batches (
    id               BIGSERIAL PRIMARY KEY,
    source_type      TEXT NOT NULL,  -- 'excel_upload' | 'csv_upload' | 'api_sync'(향후)
    source_name      TEXT NOT NULL,
    target_table     TEXT NOT NULL,
    records_inserted INT DEFAULT 0,
    records_updated  INT DEFAULT 0,
    records_failed   INT DEFAULT 0,
    status           TEXT NOT NULL DEFAULT 'pending',  -- pending|processing|success|failed
    error_log        JSONB,
    started_by       BIGINT REFERENCES users(id),
    started_at       TIMESTAMPTZ DEFAULT NOW(),
    finished_at      TIMESTAMPTZ
);

-- 업무 Ticket (AI 자동 생성 포함)
CREATE TABLE tickets (
    id               BIGSERIAL PRIMARY KEY,
    title            TEXT NOT NULL,
    description      TEXT,
    priority         TEXT NOT NULL DEFAULT 'normal',  -- critical|high|normal|low
    status           TEXT NOT NULL DEFAULT 'open',    -- open|in_progress|resolved|closed
    category         TEXT,     -- supply_shortage|bom_change|psi_alert|general
    related_item_id  BIGINT REFERENCES item_master(id),
    related_model_id BIGINT REFERENCES bom_models(id),
    assigned_to      BIGINT REFERENCES users(id),
    created_by_agent TEXT,     -- AI 역할명 (수동 생성 시 NULL)
    created_at       TIMESTAMPTZ DEFAULT NOW(),
    updated_at       TIMESTAMPTZ DEFAULT NOW(),
    resolved_at      TIMESTAMPTZ
);

-- 통화/회의 녹음
CREATE TABLE meeting_records (
    id           BIGSERIAL PRIMARY KEY,
    recorded_at  TIMESTAMPTZ NOT NULL,
    duration_sec INT,
    audio_path   TEXT NOT NULL,
    transcript   TEXT,
    summary      TEXT,
    participants TEXT[],
    action_items JSONB,
    created_at   TIMESTAMPTZ DEFAULT NOW()
);

-- Agent 실행 로그
CREATE TABLE agent_logs (
    id             BIGSERIAL PRIMARY KEY,
    role           TEXT NOT NULL,
    provider_tier  TEXT NOT NULL,
    provider_model TEXT NOT NULL,
    input_tokens   INT,
    output_tokens  INT,
    duration_ms    INT,
    success        BOOLEAN DEFAULT TRUE,
    error_msg      TEXT,
    created_at     TIMESTAMPTZ DEFAULT NOW()
);
```

---

## 6. API 엔드포인트 (전체)

**Base URL: `/api/v1`**

**RBAC 권한:** `admin > manager > internal > partner > viewer`

| 영역 | 주요 엔드포인트 | 최소 권한 |
|---|---|---|
| **Auth** | POST `/auth/login`, POST `/auth/refresh`, GET `/auth/me` | public / any |
| **BOM** | GET `/bom/models`, GET `/bom/models/{code}`, GET `/bom/reverse?part_number=` | internal |
| **BOM Import** | POST `/bom/import` | manager |
| **DP** | GET `/dp`, GET `/dp/{id}/lots` | internal |
| **DP Import** | POST `/dp/import` | manager |
| **PL** | GET `/pl?date=&line=`, POST `/pl/compute`, GET `/pl/export` | internal |
| **IT** | GET `/items`, POST `/items`, PUT `/items/{id}` | internal/manager |
| **IT Import** | POST `/items/import` | manager |
| **PSI** | GET `/psi?date_from=&date_to=`, PUT `/psi/{item_id}/{date}`, POST `/psi/recompute` | internal |
| **효율표** | GET `/efficiency`, POST `/efficiency`, POST `/efficiency/import` | internal |
| **WIP** | GET `/wip`, GET `/wip/locations`, POST `/wip/import` | internal |
| **Import** | POST `/import/upload`, GET `/import/preview/{batch_id}`, POST `/import/batches/{id}/process` | manager |
| **AI** | POST `/ai/chat`, POST `/ai/voice/transcribe`, POST `/ai/voice/tts` | internal |
| **Dashboard** | GET `/dashboard/summary`, WS `/ws/dashboard` | internal |
| **Tickets** | GET `/tickets`, PUT `/tickets/{id}` | internal |
| **Admin** | GET/POST `/admin/users`, GET/POST `/admin/vendors`, GET/POST `/admin/lines` | admin |

---

## 7. Import 파이프라인

```
파일 업로드 (Excel/CSV)
    → POST /import/upload
    → ImportBatch 생성 (status='pending'), 파일 data/raw/ 저장
    → GET /import/preview/{batch_id}
    → Polars 파싱 → validator.validate() → 상위 20행 + 오류 목록 반환
    → 사용자 확인 → POST /import/batches/{id}/process
    → Celery 태스크: 재파싱 → 검증 → DB upsert
    → BOM import 시 → PL 스냅샷 재계산 트리거
    → DP import 시 → PSI required_qty 재계산 트리거
    → WebSocket push → 대시보드 업데이트
```

**Parser 계약:**
```python
def parse(file_path: str) -> pl.DataFrame:
    """순수 함수. 실패 시 ParseError 발생."""
```

**Validator 계약:**
```python
def validate(df: pl.DataFrame, target_table: str) -> ValidationResult:
    """필수 컬럼, 타입, FK 참조 검증. is_valid + errors: list[RowError] 반환."""
```

**Auto-sync 확장:** 파일 업로드 단계만 API sync로 교체. 파싱/검증/DB 쓰기 파이프라인은 동일하게 재사용됨.

---

## 8. LLM Provider 추상 레이어 설계

### 8.1 핵심 인터페이스

```python
# backend/llm/base.py
from abc import ABC, abstractmethod
from dataclasses import dataclass
from enum import Enum

class MessageRole(str, Enum):
    SYSTEM = "system"
    USER = "user"
    ASSISTANT = "assistant"
    TOOL = "tool"

@dataclass
class Message:
    role: MessageRole
    content: str
    tool_call_id: str | None = None
    tool_calls: list[dict] | None = None

@dataclass
class LLMResponse:
    content: str | None
    tool_calls: list[dict] | None = None
    usage: dict | None = None
    raw: dict | None = None

class LLMProvider(ABC):
    @abstractmethod
    async def chat(self, messages: list[Message], tools: list[dict] | None = None,
                   temperature: float = 0.3, max_tokens: int = 4096, **kwargs) -> LLMResponse: ...

    @abstractmethod
    async def embed(self, text: str) -> list[float]: ...

    @abstractmethod
    async def transcribe(self, audio_path: str, language: str = "ko") -> str: ...

    @property
    @abstractmethod
    def tier(self) -> str: ...  # 'local' 또는 'cloud'
```

### 8.2 Provider 구현체

**OllamaProvider** (`backend/llm/providers/ollama_provider.py`): Ollama 로컬 서버 연동.  
**OpenAICompatibleProvider** (`backend/llm/providers/openai_compatible_provider.py`): base_url + api_key만 교체하면 OpenAI/Anthropic/Azure/Groq 모두 동작.  
**FasterWhisperProvider** (`backend/llm/providers/whisper_provider.py`): STT 전용. GPU 없으면 CPU 자동 폴백.

### 8.3 Task Tier Router

```python
# backend/llm/router.py
TASK_TIER_MAP = {
    "transcribe":         "local",
    "meeting_summary":    "local",
    "status_check":       "local",
    "ticket_create":      "local",
    "simple_query":       "local",
    "report_generate":    "cloud",
    "anomaly_analysis":   "cloud",
    "cross_doc_analysis": "cloud",
    "strategic_advice":   "cloud",
}
```

### 8.4 환경변수 (.env.example)

```ini
# Local LLM
OLLAMA_URL=http://localhost:11434
LOCAL_LLM_MODEL=qwen2.5:7b

# Cloud LLM (OpenAI 호환 포맷 — base_url만 교체하면 어떤 Provider든 동작)
CLOUD_LLM_BASE_URL=https://api.openai.com/v1
CLOUD_LLM_MODEL=gpt-4o
CLOUD_LLM_API_KEY=sk-...

# Whisper
WHISPER_MODEL_SIZE=medium

# DB
DATABASE_URL=postgresql+asyncpg://lars:lars_secret@localhost:5432/lars_db
REDIS_URL=redis://localhost:6379/0

# Auth
JWT_SECRET_KEY=<random-32-char-string>
JWT_ALGORITHM=HS256
ACCESS_TOKEN_EXPIRE_MINUTES=60
REFRESH_TOKEN_EXPIRE_DAYS=30
```

---

## 9. LARS 역할 기반 Agent 프롬프트 체계

### 9.1 역할 목록

| 역할 ID | 처리 티어 | 핵심 책임 |
|---|---|---|
| `task_dispatcher` | Local | 사용자 요청 → 역할 자동 라우팅 |
| `logistics_monitor` | Local | 재고/생산 이상 탐지, 경보 Ticket 자동 발행 |
| `meeting_scribe` | Local | 음성 전사 → 구조화된 회의록 생성 |
| `ticket_agent` | Local | Ticket 발행, 상태 변경, 담당자 할당 |
| `report_generator` | Cloud | BOM/DP/PSI 교차 분석 통합 리포트 |
| `data_analyst` | Cloud | 복잡한 다차원 데이터 쿼리 및 추론 |

### 9.2 Agent Tools (역할별 접근 가능 Tool)

| Tool | 역할 |
|---|---|
| `query_psi` | logistics_monitor, data_analyst, report_generator |
| `get_bom_tree` | report_generator, data_analyst |
| `get_dp_summary` | logistics_monitor, report_generator |
| `create_ticket` | ticket_agent, logistics_monitor |
| `list_tickets` | ticket_agent |
| `update_ticket` | ticket_agent |
| `bom_reverse_lookup` | data_analyst, report_generator |

### 9.3 Agent 실행 루프

```python
# backend/agent/agent.py
class LARSAgent:
    def __init__(self, role: str, provider: LLMProvider): ...

    async def run(self, user_input: str, max_tool_rounds: int = 5) -> str:
        # 1. system_prompt + user_input 구성
        # 2. provider.chat(messages, tools) 호출
        # 3. tool_calls 있으면 execute_tool() 결과를 메시지에 추가 후 재호출
        # 4. max_tool_rounds 내에 응답 텍스트 반환
```

---

## 10. 음성/전화 통합 설계

```
전화 수신/발신 (PJSIP/SIP.js)
    → 통화 녹음 (WAV/OGG, data/recordings/)
    → Faster-Whisper 로컬 전사 (Celery 비동기)
    → LARSAgent(role="meeting_scribe", provider=local_llm)
    → 구조화 회의록 → PostgreSQL + WebUI 표시
    → 액션 아이템 → ticket_agent → Ticket 자동 발행
```

**요구 인프라:** 사내 IP-PBX (Asterisk/FreePBX) 또는 SIP Trunk

---

## 11. 프론트엔드 라우팅 구조

```
/login               → LoginPage (public)
/dashboard           → DashboardPage (KPI + 경보 + PSI 요약)
/bom                 → BOMListPage (모델 검색/목록)
/bom/:modelCode      → BOMDetailPage (BOMTree 컴포넌트 + fnBOM_Reverse 패널)
/dp                  → DailyPlanPage (날짜/라인별 계획)
/pl                  → PartListPage (BOM×DP 보고서)
/items               → ItemMasterPage (추적 품목 관리)
/items/:id           → ItemDetailPage (품목 상세 + BOM 사용처)
/psi                 → PSIPage (PSIMatrix — inline 편집, 부족분 하이라이트)
/efficiency          → EfficiencyPage (작업자×품목 효율)
/wip                 → WIPPage (공장 Location 지도 + 재공 현황)
/ai                  → AIChatPage (VoiceInputButton + 텍스트 채팅)
/import              → ImportPage (파일 업로드 → Preview → 확인)
/tickets             → TicketListPage
/admin               → AdminPage (사용자/벤더/라인 관리)
```

**모바일 대응 (768px 미만):**
- 하단 탭바 (Dashboard / BOM+DP / PSI / AI Chat / 메뉴)
- 테이블: 가로 스크롤 + 카드뷰 전환
- PSI Matrix: 첫 열(품목명) 고정 + 가로 스크롤

---

## 12. 백엔드 디렉토리 구조

```
backend/
├── main.py                    # FastAPI app factory, lifespan, middleware, CORS
├── requirements.txt
├── .env.example
├── alembic.ini
├── alembic/versions/
│   └── 001_initial_schema.py  # 전체 테이블 생성
│
├── core/
│   ├── config.py              # Pydantic BaseSettings
│   ├── database.py            # async engine + get_session
│   ├── redis.py               # Redis 연결 + 캐시 헬퍼
│   ├── security.py            # JWT + bcrypt
│   └── deps.py                # Depends: get_current_user, require_role
│
├── models/                    # SQLModel ORM (DB 스키마와 1:1 대응)
│   ├── user.py
│   ├── vendor.py
│   ├── bom.py                 # BomModel, BomItem
│   ├── daily_plan.py          # ProductionLine, DailyPlan, DailyPlanLot
│   ├── part_list.py           # PartListSnapshot
│   ├── item_master.py
│   ├── psi.py
│   ├── efficiency.py          # Worker, LogisticsEfficiency
│   ├── wip.py                 # FactoryLocation, StandardWip
│   ├── ticket.py
│   ├── import_batch.py
│   └── ai.py                  # MeetingRecord, AgentLog
│
├── schemas/                   # Pydantic request/response (ORM과 분리)
│   ├── auth.py
│   ├── bom.py                 # BomModelRead, BomItemRead, BomTreeNode, ReverseResult
│   ├── daily_plan.py
│   ├── part_list.py
│   ├── item_master.py
│   ├── psi.py                 # PsiMatrix, PsiCellUpdate, ShortageAlert
│   ├── efficiency.py
│   ├── wip.py
│   ├── ticket.py
│   ├── import_batch.py        # BatchRead, BatchStatus, PreviewRow
│   └── ai.py
│
├── services/                  # 비즈니스 로직 (Polars 중심)
│   ├── bom_service.py         # BOM 트리 빌드, fnBOM_Reverse
│   ├── daily_plan_service.py
│   ├── part_list_service.py   # PL 계산 (BOM × DP), 스냅샷 저장
│   ├── item_master_service.py
│   ├── psi_service.py         # PSI 행렬 빌드, 부족분 감지, 재계산
│   ├── efficiency_service.py
│   ├── wip_service.py
│   ├── ticket_service.py
│   └── dashboard_service.py   # KPI 요약 집계
│
├── parsers/                   # Excel → Polars DataFrame (순수 함수)
│   ├── bom_parser.py          # *.@CVZ.*.xlsx 파싱
│   ├── daily_plan_parser.py   # Excel_Export_*.xlsx 파싱
│   ├── item_master_parser.py
│   ├── efficiency_parser.py
│   └── validator.py           # 컬럼 검증, 타입 강제, FK 체크
│
├── api/
│   ├── router.py              # 전체 라우터 마운트
│   └── routes/
│       ├── auth.py
│       ├── bom.py
│       ├── daily_plan.py
│       ├── part_list.py
│       ├── item_master.py
│       ├── psi.py
│       ├── efficiency.py
│       ├── wip.py
│       ├── tickets.py
│       ├── import_pipeline.py
│       ├── dashboard.py
│       ├── ai.py
│       ├── admin.py
│       └── websocket.py
│
├── llm/
│   ├── base.py
│   ├── factory.py
│   ├── router.py
│   └── providers/
│       ├── ollama_provider.py
│       ├── openai_compatible_provider.py
│       └── whisper_provider.py
│
├── agent/
│   ├── prompts.py             # 역할별 System Prompt
│   ├── tools.py               # Tool 스키마 + 실행 핸들러
│   └── agent.py               # LARSAgent 실행 루프
│
├── workers/
│   └── tasks.py               # Celery 태스크
│
└── voice/
    ├── sip_handler.py
    └── recorder.py
```

---

## 13. 프론트엔드 디렉토리 구조 (.WebUI 확장)

신규 추가 패키지:
```
react-router-dom@6, @tanstack/react-query@5, zustand, axios, react-dropzone
```

```
.WebUI/src/
├── App.tsx                    # react-router-dom 전체 라우팅 + Auth 보호
├── lib/
│   ├── api.ts                 # Axios 기본 클라이언트 (JWT 인터셉터)
│   └── queryClient.ts         # TanStack Query 클라이언트 설정
├── stores/
│   ├── authStore.ts           # JWT 토큰, 현재 사용자
│   ├── uiStore.ts             # 사이드바/모달 상태
│   └── wsStore.ts             # WebSocket 연결 상태
├── hooks/
│   ├── useAuth.ts
│   ├── useBOM.ts
│   ├── useDP.ts
│   ├── usePSI.ts
│   └── useImport.ts
├── pages/                     # 14개 페이지 (라우팅 구조 참조)
├── components/
│   ├── layout/
│   │   ├── AppShell.tsx       # 헤더 + nav rail + 푸터
│   │   ├── MobileNav.tsx      # 모바일 하단 탭바
│   │   └── ProtectedRoute.tsx # Auth 가드
│   ├── bom/
│   │   ├── BOMTree.tsx        # 들여쓰기 기반 계층 렌더링
│   │   └── BOMReversePanel.tsx
│   ├── psi/
│   │   └── PSIMatrix.tsx      # 스프레드시트형 인라인 편집 그리드
│   ├── import/
│   │   ├── FileDropzone.tsx
│   │   ├── PreviewTable.tsx
│   │   └── ValidationErrors.tsx
│   ├── ai/
│   │   ├── ChatMessageList.tsx
│   │   └── VoiceInputButton.tsx
│   └── shared/
│       ├── DataTable.tsx      # 범용 정렬/필터 테이블
│       ├── DateRangePicker.tsx
│       └── ExportButton.tsx
└── types/
    ├── api.ts                 # API 응답 타입 (backend schemas 미러)
    └── common.ts              # 공통 타입 (페이지네이션, 정렬, 필터)
```

---

## 14. Phase별 실행 로드맵

### Phase 1 — Foundation (1~3주)
**목표:** 인프라 + BOM/DP 모듈 + Import 파이프라인 동작

- Docker Compose (PostgreSQL 16 + pgvector + Redis + Ollama)
- SQLModel 모델 전체 + Alembic `001_initial_schema.py`
- `core/config.py`, `core/security.py`, `core/deps.py`
- JWT Auth 엔드포인트 (`/auth/login`, `/auth/me`, `/auth/refresh`)
- `parsers/bom_parser.py` (*.@CVZ.*.xlsx 파싱)
- `parsers/daily_plan_parser.py` (Excel_Export_*.xlsx 파싱)
- `parsers/validator.py`
- `services/bom_service.py` (BOM 트리 빌드, fnBOM_Reverse)
- `services/daily_plan_service.py`
- Import 파이프라인 end-to-end (upload → preview → process)
- **검증:** TestSet Excel 파일 import → `GET /api/v1/bom/models/{code}` 올바른 계층 트리 반환

### Phase 2 — 비즈니스 모듈 + 프론트엔드 (4~6주)
**목표:** 8개 모듈 전체 API + React 실데이터 연결

- PL 계산 서비스 (`part_list_service.py`)
- IT/PSI/효율/WIP 서비스 + API 라우터
- React 라우터 + JWT 인증 흐름 (LoginPage, ProtectedRoute, 토큰 갱신)
- PSIMatrix (인라인 편집 가능), BOMTree, Import 플로우
- WebSocket 대시보드 알림 (PSI 부족, 신규 Ticket)
- **검증:** 로그인 → DP import → PL 자동 계산 → PSI 보유량 입력 → 부족분 하이라이트

### Phase 3 — AI 통합 (7~9주)
**목표:** LLM 레이어 + 음성 인터페이스 + Ticket 에이전트

- LLM 레이어 전체 구현 (base, providers, factory, router)
- Agent 툴: `query_psi`, `get_bom_tree`, `create_ticket`, `list_tickets`, `get_dp_summary`, `bom_reverse_lookup`
- STT (`/ai/voice/transcribe` — Faster-Whisper)
- TTS (`/ai/voice/tts` — Piper Korean)
- AIChatPage + VoiceInputButton
- `logistics_monitor`: 15분 주기 PSI 체크 → 자동 Ticket
- **검증:** 한국어 음성 "오늘 C11 라인 PSI 부족 항목 알려줘" → 한국어 음성 응답

### Phase 4 — 고급 기능 + 운영 준비 (10~12주)
**목표:** 외부 파트너 권한 + 운영 강화

- 파트너 사용자: 담당 품목만 PSI 조회 (BOM/admin 접근 불가)
- Cloud LLM 역할 (`report_generator`, `data_analyst`)
- `meeting_scribe` 역할 + meeting_records
- 모바일 하단 탭바, PSI 행렬 반응형
- Redis 캐싱 (BOM 트리 TTL 1시간, import 시 무효화)
- 부하 테스트: PSI 동시 50명 접속
- **검증:** 외부 파트너 로그인 → 담당 품목만 접근 가능, BOM/admin 접근 불가

---

## 15. 로컬 개발 환경

```yaml
# docker-compose.yml
version: "3.9"
services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_DB: lars_db
      POSTGRES_USER: lars
      POSTGRES_PASSWORD: lars_secret
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama

volumes:
  postgres_data:
  ollama_data:
```

```bash
# 개발 시작
docker compose up -d
ollama pull qwen2.5:7b          # 로컬 LLM (~4.7GB)
ollama pull nomic-embed-text    # 로컬 임베딩 모델

cd backend
pip install -r requirements.txt
alembic upgrade head
uvicorn main:app --reload --port 8000

# Celery Worker (별도 터미널)
celery -A workers.tasks worker --loglevel=info
```

---

*이 문서는 `LARS_Project/` 아래에서 관리됩니다. v3 — 2026-04-26 Project Leader 승인.*  
*Coder 지시문 참조: `LARS_Project/Phase1_Coder_Instructions.md`*
