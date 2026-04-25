# Phase 1 Coder Instructions

> 작성자: Project Leader  
> 작성일: 2026-04-26  
> 대상: Gemini Pro 3.1 (Coder)  
> 기준 문서: `LARS_Project/New_LARS_Project.md` (반드시 먼저 읽을 것)  
> 작업 디렉토리: `/test/LARS/`

---

## 사전 공지 (Coder 필독)

- **이 문서의 모든 작업을 시작하기 전에 `/test/LARS/LARS_PROJECT/New_LARS_Project.md`를 전체 읽어야 한다.**
- DataFrame 처리는 **Polars만 허용**한다. `import pandas` 는 어떤 파일에도 등장해서는 안 된다.
- DB 쓰기는 **SQLModel ORM 또는 Polars `write_database`** 를 사용한다. 서비스 레이어에서 raw SQL을 직접 작성하지 않는다.
- 모든 public 함수에는 **type hint + 한 줄 docstring**이 있어야 한다.
- **git commit/push는 Coder가 하지 않는다.** 코드 작성만 담당한다.
- 작업 완료 시 각 태스크 하단의 "검증 방법" 명령어를 직접 실행하고 결과를 보고한다.

---

## Phase 1 목표

> 인프라 + BOM/DP 모듈 + JWT Auth + Import 파이프라인 end-to-end 동작

**최종 검증 기준:**
TestSet의 실제 Excel 파일을 import하면, `GET /api/v1/bom/models/{model_code}`가 올바른 계층 트리를 반환해야 한다.

---

## Task 1-A: 삭제 작업

**작업 내용:** 구 C# 관련 파일 삭제

**삭제 대상:**
```
/test/LARS/LARS.sln
/test/LARS/TestSet/TestHarness/     ← 디렉토리 전체
```

**유지 대상 (절대 삭제 금지):**
```
/test/LARS/TestSet/RawFiles/
/test/LARS/TestSet/ResultFiles/
/test/LARS/TestSet/VerificationOutput/
/test/LARS/TestSet/AutoReport(Dev).xlsb
/test/LARS/VBA/
/test/LARS/.agent/
/test/LARS/.WebUI/
```

**검증 방법:**
```bash
ls /test/LARS/LARS.sln 2>/dev/null && echo "FAIL: 파일이 남아 있음" || echo "OK"
ls /test/LARS/TestSet/TestHarness/ 2>/dev/null && echo "FAIL: 디렉토리가 남아 있음" || echo "OK"
ls /test/LARS/TestSet/RawFiles/ && echo "OK: RawFiles 유지됨"
```

---

## Task 1-B: Docker Compose 작성

**작업 내용:** `/test/LARS/docker-compose.yml` 파일 생성

**요구사항:**
- PostgreSQL 16 (pgvector/pgvector:pg16 이미지)
  - DB명: `lars_db`, 사용자: `lars`, 비밀번호: `lars_secret`
  - 포트: `5432:5432`
  - 볼륨: `postgres_data`
- Redis 7 Alpine
  - 포트: `6379:6379`
- Ollama 최신 버전
  - 포트: `11434:11434`
  - 볼륨: `ollama_data`
  - GPU 설정은 주석 처리된 선택 옵션으로 포함 (deploy.resources.reservations.devices)

**생성할 파일:** `/test/LARS/docker-compose.yml`

**검증 방법:**
```bash
docker compose -f /test/LARS/docker-compose.yml config --quiet && echo "OK: YAML 문법 유효"
```

---

## Task 1-C: Backend 기반 구조

**작업 내용:** FastAPI backend 기본 구조 파일 생성 및 `requirements.txt` 업데이트

### 1-C-1: requirements.txt 교체

**파일:** `/test/LARS/backend/requirements.txt`

**포함해야 할 패키지:**
```
# Web
fastapi>=0.115.0
uvicorn[standard]>=0.30.0
python-multipart

# Data
polars>=1.0.0
fastexcel
openpyxl
xlsxwriter

# DB
sqlmodel>=0.0.21
alembic>=1.13.0
asyncpg
databases

# Cache / Queue
redis>=5.0.0
celery>=5.4.0

# Auth
python-jose[cryptography]>=3.3.0
passlib[bcrypt]>=1.7.4

# AI
faster-whisper>=1.0.0
httpx>=0.27.0

# Config
python-dotenv>=1.0.0

# Dev
pytest
pytest-asyncio
httpx  # 테스트용
```

### 1-C-2: core/config.py 생성

**파일:** `/test/LARS/backend/core/config.py`

**요구사항:**
- Pydantic `BaseSettings` 사용
- `.env` 파일 자동 로드
- 다음 필드 포함:

```python
# DB
DATABASE_URL: str           # postgresql+asyncpg://lars:lars_secret@localhost:5432/lars_db
REDIS_URL: str              # redis://localhost:6379/0

# Auth
JWT_SECRET_KEY: str
JWT_ALGORITHM: str = "HS256"
ACCESS_TOKEN_EXPIRE_MINUTES: int = 60
REFRESH_TOKEN_EXPIRE_DAYS: int = 30

# Local LLM
OLLAMA_URL: str = "http://localhost:11434"
LOCAL_LLM_MODEL: str = "qwen2.5:7b"

# Cloud LLM
CLOUD_LLM_BASE_URL: str = "https://api.openai.com/v1"
CLOUD_LLM_MODEL: str = "gpt-4o"
CLOUD_LLM_API_KEY: str = ""

# Whisper
WHISPER_MODEL_SIZE: str = "medium"
```

- 싱글턴 패턴: `get_settings()` 함수로 캐싱된 인스턴스 반환

### 1-C-3: core/database.py 재작성

**파일:** `/test/LARS/backend/core/database.py`

**요구사항:**
- `create_async_engine` (asyncpg 드라이버)
- `AsyncSession` + `async_sessionmaker`
- FastAPI `Depends`용 `get_session()` 제너레이터
- Alembic용 동기 엔진도 별도 제공 (`get_sync_engine()`)
- 엔진 생성 시 `pool_pre_ping=True`

### 1-C-4: core/security.py 생성

**파일:** `/test/LARS/backend/core/security.py`

**요구사항:**
- `hash_password(plain: str) -> str` — bcrypt 해시
- `verify_password(plain: str, hashed: str) -> bool`
- `create_access_token(data: dict, expires_delta: timedelta | None = None) -> str` — JWT 생성
- `create_refresh_token(data: dict) -> str`
- `decode_token(token: str) -> dict` — 실패 시 `HTTPException(401)` 발생

### 1-C-5: core/deps.py 생성

**파일:** `/test/LARS/backend/core/deps.py`

**요구사항:**
- `get_current_user(token: str = Depends(oauth2_scheme), session = Depends(get_session)) -> User`
  - Bearer 토큰 검증 → DB에서 User 조회 → 비활성 사용자 차단
- `require_role(*roles: str)` — 호출 예시: `Depends(require_role("admin", "manager"))`
  - 허용되지 않은 역할이면 `HTTPException(403)` 발생

### 1-C-6: .env.example 생성

**파일:** `/test/LARS/backend/.env.example`

`New_LARS_Project.md` 섹션 8.4의 환경변수 템플릿을 그대로 작성한다.

**검증 방법:**
```bash
cd /test/LARS/backend && python -c "from core.config import get_settings; s = get_settings(); print('OK:', s.JWT_ALGORITHM)"
cd /test/LARS/backend && python -c "from core.database import get_sync_engine; print('OK: database imported')"
cd /test/LARS/backend && python -c "from core.security import hash_password, verify_password; h = hash_password('test'); assert verify_password('test', h); print('OK: bcrypt working')"
```

---

## Task 1-D: SQLModel 모델 전체 생성

**작업 내용:** DB 스키마에 대응하는 SQLModel 모델 파일들 생성

**기준:** `New_LARS_Project.md` 섹션 5의 SQL 스키마와 정확히 일치해야 함

**생성할 파일 목록:**

### `/test/LARS/backend/models/user.py`
```
테이블: users
필드: id, email, display_name, role, is_active, hashed_pw, created_at, updated_at
role 허용값: "admin" | "manager" | "internal" | "partner" | "viewer"
```

### `/test/LARS/backend/models/vendor.py`
```
테이블: vendors
필드: id, code, name, is_active, created_at, updated_at
```

### `/test/LARS/backend/models/bom.py`
```
테이블 1: bom_models
  필드: id, model_code(UNIQUE), description, version, is_active, import_batch_id, created_at, updated_at

테이블 2: bom_items
  필드: id, model_id(FK→bom_models), level, part_number, description, qty, uom,
        vendor_id(FK→vendors, nullable), vendor_raw, supply_type,
        path, sort_order, import_batch_id, created_at
  인덱스: model_id, part_number, path, (part_number, model_id) 복합
```

### `/test/LARS/backend/models/daily_plan.py`
```
테이블 1: production_lines
  필드: id, code(UNIQUE), name, is_active

테이블 2: daily_plans
  필드: id, plan_date, line_id(FK→production_lines), import_batch_id, created_at, updated_at
  UNIQUE: (plan_date, line_id)

테이블 3: daily_plan_lots
  필드: id, plan_id(FK→daily_plans), wo_number, model_id(FK→bom_models, nullable),
        model_code, lot_number, planned_qty, input_qty, output_qty,
        planned_start, sort_order, import_batch_id, created_at
```

### `/test/LARS/backend/models/part_list.py`
```
테이블: part_list_snapshots
  필드: id, lot_id(FK→daily_plan_lots), part_number, description, uom,
        vendor_id(FK→vendors, nullable), vendor_raw,
        required_qty, snapshot_date, import_batch_id, created_at
```

### `/test/LARS/backend/models/item_master.py`
```
테이블: item_master
  필드: id, level, description, part_number(UNIQUE), vendor_id(FK, nullable),
        vendor_raw, tracking_user_id(FK→users, nullable), is_active,
        import_batch_id, created_at, updated_at
```

### `/test/LARS/backend/models/psi.py`
```
테이블: psi_records
  필드: id, item_id(FK→item_master), psi_date, required_qty, available_qty,
        shortage_qty(Computed: required_qty - available_qty),
        notes, last_updated_by(FK→users, nullable), created_at, updated_at
  UNIQUE: (item_id, psi_date)

주의: shortage_qty는 GENERATED ALWAYS AS 컬럼이므로 SQLModel에서 server_default 또는
      sa_column으로 처리하되, Python 쓰기 대상에서 제외해야 함.
      대안: 서비스 레이어에서 Python으로 계산 후 저장 (PostgreSQL 지원 여부 확인 필요).
      PostgreSQL 17 미만에서 GENERATED STORED 지원 확인 후 결정할 것.
```

### `/test/LARS/backend/models/efficiency.py`
```
테이블 1: workers
  필드: id, name, employee_id(UNIQUE, nullable), line_id(FK→production_lines), is_active, created_at

테이블 2: logistics_efficiency
  필드: id, worker_id(FK→workers), item_id(FK→item_master), model_id(FK→bom_models, nullable),
        recorded_date, target_qty, actual_qty, efficiency_rate(Computed),
        notes, is_realtime, import_batch_id, created_at
```

### `/test/LARS/backend/models/wip.py`
```
테이블 1: factory_locations
  필드: id, code(UNIQUE), name, zone, x_coord, y_coord, is_active, created_at

테이블 2: standard_wip
  필드: id, item_id(FK→item_master), location_id(FK→factory_locations),
        target_qty, safety_stock, notes, is_active, import_batch_id, created_at, updated_at
  UNIQUE: (item_id, location_id)
```

### `/test/LARS/backend/models/ticket.py`
```
테이블: tickets
  필드: id, title, description, priority, status, category,
        related_item_id(FK→item_master, nullable), related_model_id(FK→bom_models, nullable),
        assigned_to(FK→users, nullable), created_by_agent,
        created_at, updated_at, resolved_at
```

### `/test/LARS/backend/models/import_batch.py`
```
테이블: import_batches
  필드: id, source_type, source_name, target_table,
        records_inserted, records_updated, records_failed,
        status, error_log(JSON), started_by(FK→users, nullable), started_at, finished_at
```

### `/test/LARS/backend/models/ai.py`
```
테이블 1: meeting_records
  필드: id, recorded_at, duration_sec, audio_path, transcript, summary,
        participants(JSON list), action_items(JSON), created_at

테이블 2: agent_logs
  필드: id, role, provider_tier, provider_model,
        input_tokens, output_tokens, duration_ms, success, error_msg, created_at
```

**공통 요구사항:**
- SQLModel의 `SQLModel` 베이스 클래스 사용 (`table=True`)
- 관계(relationship)는 Phase 1에서 선택 사항. 서비스 레이어에서 JOIN으로 처리해도 무방.
- 모든 모델 파일에서 순환 import 주의 (필요 시 `TYPE_CHECKING` 블록 활용)

**검증 방법:**
```bash
cd /test/LARS/backend && python -c "
from models.user import User
from models.bom import BomModel, BomItem
from models.daily_plan import DailyPlan, DailyPlanLot, ProductionLine
from models.item_master import ItemMaster
from models.psi import PsiRecord
print('OK: 모든 모델 import 성공')
"
```

---

## Task 1-E: Alembic 설정 및 마이그레이션

**작업 내용:** Alembic 초기화 + 전체 스키마 마이그레이션 파일 생성

### 1-E-1: Alembic 초기화

**실행 위치:** `/test/LARS/backend/`

```bash
cd /test/LARS/backend
alembic init alembic
```

### 1-E-2: alembic/env.py 수정

**수정 내용:**
- `target_metadata` = 모든 SQLModel 모델의 메타데이터 (`SQLModel.metadata`)
- 동기 DB URL 사용 (`DATABASE_URL`에서 `asyncpg` → `psycopg2` 또는 `psycopg` 치환)
- `.env` 로드 (`python-dotenv`)

```python
# alembic/env.py 핵심 부분 예시
from sqlmodel import SQLModel
import models.user, models.vendor, models.bom, models.daily_plan
import models.part_list, models.item_master, models.psi
import models.efficiency, models.wip, models.ticket
import models.import_batch, models.ai

target_metadata = SQLModel.metadata
```

### 1-E-3: 마이그레이션 파일 생성

```bash
cd /test/LARS/backend
alembic revision --autogenerate -m "001_initial_schema"
```

생성된 파일에서 다음 사항 수동 확인 후 수정:
- `pgvector` 확장 활성화 코드 추가: `op.execute("CREATE EXTENSION IF NOT EXISTS vector")`
- 복합 인덱스가 누락된 경우 수동 추가
- `GENERATED ALWAYS AS` 컬럼이 있다면 수동 SQL로 처리

**검증 방법:**
```bash
# Docker PostgreSQL이 실행 중인 상태에서
cd /test/LARS/backend
alembic upgrade head && echo "OK: 마이그레이션 성공"
```

---

## Task 1-F: Auth API 엔드포인트

**작업 내용:** JWT 기반 로그인/갱신/내 정보 API 구현

### 생성할 파일

**`/test/LARS/backend/schemas/auth.py`**
```python
# Pydantic 스키마
class LoginRequest(BaseModel):
    email: str
    password: str

class TokenResponse(BaseModel):
    access_token: str
    refresh_token: str
    token_type: str = "bearer"

class UserMe(BaseModel):
    id: int
    email: str
    display_name: str
    role: str
```

**`/test/LARS/backend/api/routes/auth.py`**

엔드포인트:
- `POST /auth/login` — 이메일/비밀번호 검증 → `TokenResponse` 반환
- `POST /auth/refresh` — refresh_token 검증 → 새 `access_token` 반환
- `GET /auth/me` — Bearer 토큰 → `UserMe` 반환

**`/test/LARS/backend/api/router.py`**

```python
from fastapi import APIRouter
from api.routes import auth

router = APIRouter(prefix="/api/v1")
router.include_router(auth.router, prefix="/auth", tags=["auth"])
```

**`/test/LARS/backend/main.py` 재작성**

```python
# 포함 사항:
# - FastAPI app 생성 (lifespan 컨텍스트: DB 연결 확인, Redis ping)
# - CORS 미들웨어 (origins는 .env에서 로드)
# - router 마운트 (api/router.py)
# - /health 엔드포인트 (DB + Redis 상태 반환)
```

**초기 Admin 사용자 생성 스크립트:** `/test/LARS/backend/create_admin.py`
```python
# python create_admin.py 실행 시 admin@lars.local / admin1234 로 admin 사용자 생성
# 이미 존재하면 "이미 존재합니다" 출력 후 종료
```

**검증 방법:**
```bash
# 서버 시작 후:
cd /test/LARS/backend
uvicorn main:app --reload --port 8000 &
sleep 3

# 헬스 체크
curl -s http://localhost:8000/health | python3 -m json.tool

# Admin 생성
python create_admin.py

# 로그인 테스트
TOKEN=$(curl -s -X POST http://localhost:8000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@lars.local","password":"admin1234"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

echo "Token: ${TOKEN:0:20}..."

# /me 테스트
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:8000/api/v1/auth/me \
  | python3 -m json.tool
```

---

## Task 1-G: BOM Parser

**작업 내용:** BOM Excel 파일을 Polars DataFrame으로 변환하는 파서 구현

### ✅ 실제 확인된 BOM 파일 구조 (loborover/AutoReport BOMDB 기준)

**파일 위치:** `/test/AutoReport/BOMDB/`  
**파일명 패턴:** `{MODEL_CODE}.{SUFFIX}@CVZ.EKHQ 1.0.xlsx`  
예시: `LSGL6335X.ARSELGA@CVZ.EKHQ 1.0.xlsx` → model_code=`LSGL6335X`, suffix=`ARSELGA`

**시트명:** `ag-grid`  
**헤더 행:** Row 1 (0-indexed: 0)  
**데이터 시작:** Row 2 (0-indexed: 1)

**컬럼 매핑 (0-indexed, Row 1 기준):**

| 인덱스 | 헤더명 | 사용 여부 | 매핑 필드 |
|---|---|---|---|
| 0 | (None, 빈 열) | 무시 | — |
| 1 | `Lvl` | **필수** | level (도트 파싱 필요) |
| 2 | `Parent Part No(모)` | 선택 | parent_part_number |
| 3 | `Part Name(자)` | **필수** | part_name |
| 4 | `Description` | **필수** | description |
| 5 | `Part No` | **필수** | part_number |
| 6 | `Qty` | **필수** | qty |
| 7 | `UOM` | **필수** | uom |
| 8 | `Company Name` | 선택 | vendor_raw (형식: `EKHQ_{회사명}_{코드}`) |
| 13 | `R` | **필수** | row_type ('P'=우선공급사, 'B'=BOM구조, 'S'=대체품) |
| 16 | `Supply Type` | 선택 | supply_type |
| 28 | `Maker` | 선택 | maker |
| 37 | `Maker Code` | 선택 | maker_code (예: 'KR011661') |
| 38 | `Currency Code` | 무시 | — |
| 39 | `Unit Price` | 무시 | — |

**`Lvl` 컬럼 파싱 규칙:**
- `'0'` → level=0 (루트 모델)
- `'.1'` → level=1 (점 1개)
- `'..2'` → level=2 (점 2개)
- `'...3'` → level=3 (점 3개)
- `'*S*'` → 대체품 표시 (level은 context에서 유지, R='S'와 함께 등장)
- 파싱 방법: `len(lvl_str) - len(lvl_str.lstrip('.'))` 로 level 계산

**중복 행 처리 규칙 (중요):**
- 동일 `Part No`가 R='P' 행과 R='B' 행으로 2번 등장함
- R='P': 공급사 정보 있음 (`Company Name`, `Maker Code` 포함)
- R='B': 공급사 정보 없음 (BOM 구조용)
- **처리 방법: R='P' 행이 있으면 R='P'를 사용하고 R='B'를 드롭. R='P'가 없으면 R='B' 유지. R='S' 행은 별도 `is_substitute=True` 플래그로 저장하거나 드롭.**
- 그룹 기준: `(parent_part_number, part_number, level)` 조합으로 중복 판단

### 생성할 파일

**`/test/LARS/backend/parsers/bom_parser.py`**

```python
import re
import os
import polars as pl
from pathlib import Path


class ParseError(Exception):
    pass


def parse(file_path: str) -> pl.DataFrame:
    """
    BOM Excel 파일을 정규화된 Polars DataFrame으로 변환한다.

    파일명에서 model_code와 suffix를 추출한다.
    예: 'LSGL6335X.ARSELGA@CVZ.EKHQ 1.0.xlsx' → model_code='LSGL6335X', suffix='ARSELGA'

    반환 컬럼:
    - model_code: str       (예: 'LSGL6335X')
    - suffix: str           (예: 'ARSELGA')
    - level: int            (0=루트, 1=1단계 조립, ...)
    - part_number: str      (Part No 컬럼)
    - part_name: str | None (Part Name(자) 컬럼)
    - description: str | None (Description 컬럼)
    - qty: float
    - uom: str
    - vendor_raw: str | None   (Company Name, 예: 'EKHQ_(주)현대정밀_KR011661')
    - maker_code: str | None   (Maker Code, 예: 'KR011661')
    - supply_type: str | None  (Supply Type 컬럼)
    - parent_part_number: str | None (Parent Part No(모) 컬럼)
    - row_type: str            ('P', 'B', 'S')
    - is_substitute: bool      (row_type == 'S')
    - sort_order: int          (원본 파일 행 순서, 0부터)
    - path: str                (materialized path, 예: '0', '0.1', '0.1.2')
    """


def _extract_model_info(file_path: str) -> tuple[str, str]:
    """
    파일명에서 (model_code, suffix) 추출.
    'LSGL6335X.ARSELGA@CVZ.EKHQ 1.0.xlsx' → ('LSGL6335X', 'ARSELGA')
    """


def _parse_level(lvl_str: str) -> int:
    """
    Lvl 문자열에서 레벨 정수 추출.
    '0' → 0, '.1' → 1, '..2' → 2, '*S*' → -1 (대체품 표시)
    """


def _compute_paths(levels: list[int]) -> list[str]:
    """
    레벨 배열에서 materialized path 배열을 계산한다.
    예: [0, 1, 2, 2, 1, 2] → ['0', '0.1', '0.1.2', '0.1.3', '0.4', '0.4.5']
    루트(level=0)는 '0'으로 시작.
    """


def _deduplicate(df: pl.DataFrame) -> pl.DataFrame:
    """
    동일 (parent_part_number, part_number, level) 그룹에서
    R='P' 행이 있으면 R='P' 유지, 없으면 R='B' 유지.
    R='S' (대체품)는 is_substitute=True 플래그로 유지하되 트리에서 제외 옵션 제공.
    """
```

**에러 처리:**
- 필수 컬럼 없음: `ParseError(f"Required column '{col}' not found. File: {file_path}")`
- 파일명에서 모델 코드 추출 실패: `ParseError(f"Cannot extract model_code from filename: {basename}")`
- 빈 파일 / 헤더만 있음: `ParseError("BOM file contains no data rows")`
- 부분 실패 (개별 행 파싱 오류): 경고 로그 후 해당 행 제외, 나머지 반환

**`/test/LARS/backend/parsers/validator.py`**

```python
from dataclasses import dataclass

@dataclass
class RowError:
    row_index: int
    column: str
    message: str

@dataclass
class ValidationResult:
    is_valid: bool
    errors: list[RowError]
    valid_row_count: int
    invalid_row_count: int

def validate_bom(df: pl.DataFrame) -> ValidationResult:
    """
    BOM DataFrame 검증:
    - part_number null 없어야 함
    - level 0 이상 정수여야 함
    - qty 0보다 커야 함
    """

def validate_daily_plan(df: pl.DataFrame) -> ValidationResult:
    """DP DataFrame 검증."""
```

**검증 방법:**
```bash
cd /test/LARS/backend

python3 -c "
import glob
from parsers.bom_parser import parse

files = glob.glob('/test/AutoReport/BOMDB/*.xlsx')
print(f'BOM 파일 수: {len(files)}')

# 샘플 2개 파싱 테스트
for f in files[:2]:
    df = parse(f)
    print(f'\\n파일: {f.split(\"/\")[-1]}')
    print('Schema:', df.schema)
    print('Shape:', df.shape)
    print('Level 분포:', df.group_by(\"level\").len().sort(\"level\"))
    print('First 3 rows:')
    print(df.select(['model_code','suffix','level','part_number','description','qty','path']).head(3))
    # 필수 검증
    assert 'part_number' in df.columns
    assert 'level' in df.columns
    assert 'path' in df.columns
    assert 'model_code' in df.columns
    assert df.filter(df['level'] == 0).shape[0] == 1, '루트 행이 1개여야 함'
    assert not df['part_number'].is_null().any(), 'part_number에 null 없어야 함'
    print('OK')
"
```

---

## Task 1-H: Daily Plan Parser (두 가지 포맷 지원)

**작업 내용:** DP 파일을 Polars DataFrame으로 변환하는 파서 구현 — Excel과 CSV 두 가지 포맷 지원

### ✅ 실제 확인된 DP 파일 구조 (loborover/AutoReport DPDB 기준)

#### 포맷 1: Excel (`Excel_Export_[MMDD_hhmmss].xlsx`)

**파일 위치:** `/test/AutoReport/DPDB/`  
**시트명:** `Sheet1`  
**헤더 구조:**
- Row 1 (0-indexed: 0): 그룹 헤더 (`No`, `+`, `+`, ...) — **스킵**
- Row 2 (0-indexed: 1): 한국어 컬럼명 그룹 — **스킵**
- Row 3 (0-indexed: 2): 실제 컬럼명 헤더 — **이 행을 헤더로 사용**
- Row 4 (0-indexed: 3): 합계 요약 행 (Col 2 = 'Sum') — **스킵**
- Row 5+ (0-indexed: 4+): **실제 데이터**

**컬럼 매핑 (Row 3 기준 헤더):**

| 인덱스 | 헤더명 | 사용 여부 | 매핑 필드 |
|---|---|---|---|
| 0 | `No` | 선택 | sort_order |
| 1 | `조직` | 선택 | organization ('CVZ') |
| 2 | `공장` | 선택 | factory |
| 3 | `생산 라인` | **필수** | line_code (예: 'C11') |
| 4 | `W/O` | **필수** | wo_number |
| 5 | `모델` | **필수** | model_code |
| 6 | `Suffix` | **필수** | suffix |
| 7 | `부품번호` | 선택 | full_part_no (model.suffix) |
| 17 | `Planned Start Time` | **필수** | planned_start (datetime) |
| 18 | `Planned End Time` | 선택 | planned_end |
| 19 | `Plan Due Date` | 선택 | due_date |
| 20 | `Production Start Date` | **필수** | production_start (plan_date 결정에 사용) |
| 35 | `W/O 계획수량` | **필수** | planned_qty (int) |
| 36 | `W/O Input` | **필수** | input_qty (int, 없으면 0) |
| 37 | `W/O실적` | **필수** | output_qty (int, 없으면 0) |

**plan_date 결정 로직:**
1. `Production Start Date` (Col 20) 가 있으면 그 날짜 사용
2. 없으면 `Planned Start Time` (Col 17) 의 날짜 부분 사용
3. 하나의 Excel 파일에 여러 날짜가 섞여 있을 수 있음 → 행마다 날짜를 개별 추출

**파일명 날짜 (보조 정보):**
`Excel_Export_[0422_082308].xlsx` → 월=04, 일=22, 시=08, 분=23, 초=08 (연도는 Planned Start Time에서 결정)

#### 포맷 2: CSV (`Production_Plan_Assembly[R+F]_{timestamp}_CVZ.csv`)

**파일 위치:** `/test/AutoReport/DPDB/`  
**인코딩:** UTF-8-BOM (`utf-8-sig`)  
**헤더 구조:**
- Row 1 (0-indexed: 0): 컬럼 헤더 — **이 행을 헤더로 사용**
- Row 2 (0-indexed: 1): 전체 합계 행 (Plant/Line 모두 비어 있음) — **스킵**
- Row 3 (0-indexed: 2): 라인별 소계 행 (Line='C11'이지만 Demand ID 없음) — **스킵**
- Row 4+ (0-indexed: 3+): **실제 데이터**

**컬럼 매핑 (헤더 행 기준):**

| 헤더명 | 사용 여부 | 매핑 필드 |
|---|---|---|
| `Plant` | 선택 | organization |
| `Line` | **필수** | line_code |
| `Demand ID` | **필수** | wo_number |
| `Model.Suffix` | 선택 | full_part_no |
| `Model` | **필수** | model_code |
| `Suffix` | **필수** | suffix |
| `PST` | **필수** | planned_start (datetime, 예: '2026-04-22 08:00:00') |
| `Lot Qty` | **필수** | planned_qty (float→int) |
| `Result Qty` | **필수** | output_qty (float→int) |
| `Closed Qty` | 선택 | closed_qty |
| `MM/DD` 형식 열들 | **필수** | 일별 계획수량 (PSI 계산용 JSON 저장) |

**주의:** CSV의 날짜 컬럼들 (`04/22`, `04/23`, ...) 은 PSI 계산에 활용됨. 이 데이터는 별도 `daily_qty_json: str` 컬럼에 JSON 형태로 보존 (`{"2026-04-22": 161.0, "2026-04-23": 0.0, ...}`).  
연도는 `PST` 컬럼에서 추출.

### 생성할 파일

**`/test/LARS/backend/parsers/daily_plan_parser.py`**

```python
import polars as pl
from pathlib import Path
from datetime import date


class ParseError(Exception):
    pass


def parse_excel(file_path: str) -> pl.DataFrame:
    """
    Excel_Export_[MMDD_hhmmss].xlsx 형식 DP 파일 파싱.

    반환 컬럼:
    - line_code: str           (생산 라인, 예: 'C11')
    - wo_number: str           (W/O)
    - model_code: str          (모델)
    - suffix: str              (Suffix)
    - plan_date: date          (Production Start Date 또는 Planned Start Time의 날짜)
    - planned_start: datetime | None  (Planned Start Time)
    - planned_qty: int         (W/O 계획수량)
    - input_qty: int           (W/O Input, 없으면 0)
    - output_qty: int          (W/O실적, 없으면 0)
    - sort_order: int          (No 컬럼 또는 행 순서)
    """


def parse_csv(file_path: str) -> pl.DataFrame:
    """
    Production_Plan_Assembly[R+F]_{timestamp}_CVZ.csv 형식 DP 파일 파싱.

    반환 컬럼:
    - line_code: str
    - wo_number: str           (Demand ID)
    - model_code: str          (Model)
    - suffix: str              (Suffix)
    - plan_date: date          (PST에서 추출)
    - planned_start: datetime | None  (PST)
    - planned_qty: int         (Lot Qty)
    - input_qty: int           (0으로 초기화, CSV에 없음)
    - output_qty: int          (Result Qty)
    - sort_order: int          (행 순서)
    - daily_qty_json: str      (MM/DD 컬럼들을 {"YYYY-MM-DD": qty} JSON 문자열로 변환)
    """


def parse(file_path: str) -> pl.DataFrame:
    """
    파일 확장자에 따라 parse_excel 또는 parse_csv 자동 선택.
    두 포맷 모두 동일한 컬럼 구조 반환 (daily_qty_json은 CSV에만 있음).
    """
    if file_path.endswith('.csv'):
        return parse_csv(file_path)
    else:
        return parse_excel(file_path)
```

**데이터 정제 규칙:**
- `planned_qty`, `input_qty`, `output_qty`: None/NaN → 0으로 처리
- `plan_date`가 없는 행: `planned_start` 날짜로 대체. 그것도 없으면 행 드롭 + 경고 로그
- `wo_number`가 None/빈 문자열인 행: 드롭 (합계 행 필터링)
- `model_code`가 없거나 빈 행: 드롭

**검증 방법:**
```bash
cd /test/LARS/backend

python3 -c "
import glob
from parsers.daily_plan_parser import parse

# Excel 포맷 테스트
excel_files = glob.glob('/test/AutoReport/DPDB/Excel_Export*.xlsx')
print(f'Excel DP 파일 수: {len(excel_files)}')
for f in excel_files[:2]:
    df = parse(f)
    print(f'\\n파일: {f.split(\"/\")[-1]}')
    print('Schema:', df.schema)
    print('Shape:', df.shape)
    print(df.select(['line_code','wo_number','model_code','plan_date','planned_qty']).head(3))
    assert 'model_code' in df.columns
    assert 'planned_qty' in df.columns
    assert 'plan_date' in df.columns
    assert 'line_code' in df.columns
    assert df['wo_number'].is_null().sum() == 0, 'wo_number에 null 없어야 함'
    print('OK: Excel')

# CSV 포맷 테스트
csv_files = glob.glob('/test/AutoReport/DPDB/Production_Plan*.csv')
print(f'\\nCSV DP 파일 수: {len(csv_files)}')
for f in csv_files[:1]:
    df = parse(f)
    print(f'파일: {f.split(\"/\")[-1]}')
    print('Shape:', df.shape)
    print(df.select(['line_code','wo_number','model_code','plan_date','planned_qty']).head(3))
    assert 'daily_qty_json' in df.columns, 'CSV 파싱 시 daily_qty_json 컬럼 있어야 함'
    print('OK: CSV')
"
```

---

## Task 1-I: BOM 서비스 + API

**작업 내용:** BOM 비즈니스 로직 + FastAPI 라우터 구현

### 생성할 파일

**`/test/LARS/backend/schemas/bom.py`**

```python
class BomItemRead(BaseModel):
    id: int
    level: int
    part_number: str
    description: str | None
    qty: float
    uom: str
    vendor_raw: str | None
    supply_type: str | None
    path: str
    children: list["BomItemRead"] = []  # 트리 구조용

class BomModelRead(BaseModel):
    id: int
    model_code: str
    description: str | None
    version: str

class BomTreeResponse(BaseModel):
    model: BomModelRead
    items: list[BomItemRead]  # flat list (트리 변환은 프론트엔드 또는 서비스에서)

class ReverseResult(BaseModel):
    part_number: str
    models: list[BomModelRead]
```

**`/test/LARS/backend/services/bom_service.py`**

```python
async def get_bom_tree(session: AsyncSession, model_code: str) -> BomTreeResponse | None:
    """모델 코드로 BOM 전체 트리 조회."""

async def bom_reverse_lookup(session: AsyncSession, part_number: str) -> ReverseResult:
    """특정 파트 번호가 사용되는 모든 모델 반환."""

async def list_models(session: AsyncSession, search: str | None = None, is_active: bool = True) -> list[BomModelRead]:
    """BOM 모델 목록 반환."""

async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """
    BOM DataFrame을 DB에 upsert.
    - bom_models에 model_code가 없으면 INSERT, 있으면 UPDATE
    - bom_items는 model_id + sort_order 기준으로 upsert
    - 반환: 삽입/갱신된 레코드 수
    """
```

**`/test/LARS/backend/api/routes/bom.py`**

```python
# 엔드포인트:
GET /bom/models                      → list_models()
GET /bom/models/{model_code}         → get_bom_tree()
GET /bom/reverse?part_number={pn}    → bom_reverse_lookup()
```

모든 엔드포인트는 `require_role("internal", "manager", "admin")` 보호 적용.

**router.py에 BOM 라우터 추가:**
```python
router.include_router(bom.router, prefix="/bom", tags=["bom"])
```

**검증 방법:**
```bash
# 서버 실행 중 상태에서
TOKEN=<로그인에서 얻은 토큰>

# BOM 모델 목록
curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/api/v1/bom/models | python3 -m json.tool

# 특정 모델 BOM 트리 (실제 import한 모델 코드 사용)
curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/api/v1/bom/models/TEST_MODEL | python3 -m json.tool
```

---

## Task 1-J: Import 파이프라인

**작업 내용:** 파일 업로드 → Preview → DB 저장 파이프라인 구현

### 생성할 파일

**`/test/LARS/backend/schemas/import_batch.py`**

```python
class BatchCreate(BaseModel):
    source_type: str
    source_name: str
    target_table: str

class BatchRead(BaseModel):
    id: int
    source_name: str
    target_table: str
    status: str
    records_inserted: int
    records_updated: int
    records_failed: int
    error_log: dict | None
    started_at: datetime
    finished_at: datetime | None

class PreviewRow(BaseModel):
    row_index: int
    data: dict
    is_valid: bool
    errors: list[str]

class PreviewResponse(BaseModel):
    batch_id: int
    total_rows: int
    valid_rows: int
    invalid_rows: int
    preview: list[PreviewRow]  # 상위 20행
```

**`/test/LARS/backend/api/routes/import_pipeline.py`**

```python
# 엔드포인트:

POST /import/upload
  - multipart/form-data: file + target_table (bom | daily_plan | item_master)
  - 파일을 data/raw/{timestamp}_{filename} 에 저장
  - ImportBatch 레코드 생성 (status='pending')
  - 반환: {"batch_id": int, "status": "pending"}

GET /import/preview/{batch_id}
  - Polars로 파일 파싱
  - validator로 검증
  - 반환: PreviewResponse

POST /import/batches/{batch_id}/process
  - BatchStatus가 'pending' 또는 'failed'인 경우만 실행
  - 재파싱 → 검증 → 서비스 레이어 import_from_df() 호출
  - ImportBatch 상태 업데이트
  - BOM import 시: bom_service.import_from_df() 호출
  - 반환: BatchRead

GET /import/batches
  - ImportBatch 목록 (최신 20건)
  - 반환: list[BatchRead]
```

**주의:**
- Phase 1에서는 Celery 없이 동기 처리로 구현해도 됨 (Phase 2에서 Celery로 전환)
- 업로드된 파일 저장 경로: `/test/LARS/data/raw/`
- `data/raw/` 디렉토리가 없으면 자동 생성

**검증 방법:**
```bash
TOKEN=<로그인 토큰>

# BOM 파일 업로드
BATCH=$(curl -s -X POST http://localhost:8000/api/v1/import/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/test/LARS/TestSet/RawFiles/$(ls /test/LARS/TestSet/RawFiles/*.xlsx | head -1 | xargs basename)" \
  -F "target_table=bom" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['batch_id'])")

echo "Batch ID: $BATCH"

# Preview
curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/api/v1/import/preview/$BATCH | python3 -m json.tool

# Process
curl -s -X POST -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/api/v1/import/batches/$BATCH/process | python3 -m json.tool

# BOM 모델 목록 확인 (import 결과)
curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/api/v1/bom/models | python3 -m json.tool
```

---

## Phase 1 최종 통합 검증

모든 Task가 완료된 후 아래 시퀀스를 순서대로 실행하고 각 단계 성공 여부를 보고한다.

```bash
# 1. 인프라 확인
docker compose -f /test/LARS/docker-compose.yml ps

# 2. DB 마이그레이션 상태
cd /test/LARS/backend && alembic current

# 3. 서버 시작
uvicorn main:app --reload --port 8000 &
sleep 3

# 4. 헬스 체크
curl -s http://localhost:8000/health

# 5. Admin 생성
python create_admin.py

# 6. 로그인
TOKEN=$(curl -s -X POST http://localhost:8000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@lars.local","password":"admin1234"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

# 7. BOM Excel import (AutoReport BOMDB 실제 파일 사용)
BATCH=$(curl -s -X POST http://localhost:8000/api/v1/import/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/test/AutoReport/BOMDB/LSGL6335X.ARSELGA@CVZ.EKHQ 1.0.xlsx" \
  -F "target_table=bom" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['batch_id'])")

curl -s -X POST -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/api/v1/import/batches/$BATCH/process

# 8. BOM 트리 조회 (실제 import된 모델 코드로)
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8000/api/v1/bom/models" \
  | python3 -c "import sys,json; models=json.load(sys.stdin); print([m['model_code'] for m in models])"

# 9. BOM Reverse 조회 (LSGL6335X BOM에 실제로 존재하는 파트로 테스트)
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8000/api/v1/bom/reverse?part_number=MGJ64584003" \
  | python3 -m json.tool
```

**Phase 1 완료 기준:**
- [ ] Task 1-A: LARS.sln, TestHarness 삭제 완료
- [ ] Task 1-B: docker-compose.yml YAML 검증 통과
- [ ] Task 1-C: core 모듈 모두 import 성공, bcrypt 동작
- [ ] Task 1-D: 모든 SQLModel 모델 import 성공
- [ ] Task 1-E: alembic upgrade head 성공
- [ ] Task 1-F: 로그인 → JWT 토큰 반환, /auth/me 응답 정상
- [ ] Task 1-G: BOM 파서로 TestSet 파일 파싱 성공, path 컬럼 검증
- [ ] Task 1-H: DP 파서로 TestSet 파일 파싱 성공
- [ ] Task 1-I: BOM import 후 `/bom/models/{code}` 올바른 응답
- [ ] Task 1-J: 파일 업로드 → preview → process 전체 플로우 동작

---

*작성: Project Leader | 2026-04-26*  
*다음 지시문: `LARS_Project/Phase2_Coder_Instructions.md` (Phase 2 시작 전 작성 예정)*
