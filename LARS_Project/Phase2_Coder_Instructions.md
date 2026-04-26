# Phase 2 Coder Instructions

> 작성자: Project Leader
> 작성일: 2026-04-26
> 대상: Coder (Gemini)
> Phase: 2 — 비즈니스 모듈 전체 + 프론트엔드 실데이터 연결

---

## 사전 확인 사항 (코딩 전 필수)

Phase 1에서 구현된 파일 목록을 확인하고, 아래 파일들을 반드시 읽어라:

- `/test/LARS/LARS_Project/New_LARS_Project.md` — Canonical Reference (전체 설계)
- `/test/LARS/backend/models/daily_plan.py` — DailyPlan, DailyPlanLot, ProductionLine 모델
- `/test/LARS/backend/models/psi.py` — PsiRecord 모델
- `/test/LARS/backend/models/item_master.py` — ItemMaster 모델
- `/test/LARS/backend/models/part_list.py` — PartListSnapshot 모델
- `/test/LARS/backend/models/efficiency.py` — Worker, LogisticsEfficiency 모델
- `/test/LARS/backend/models/wip.py` — FactoryLocation, StandardWip 모델
- `/test/LARS/backend/parsers/daily_plan_parser.py` — 파서 출력 컬럼 구조 확인
- `/test/LARS/backend/api/routes/import_pipeline.py` — 기존 import 파이프라인 로직
- `/test/LARS/backend/services/bom_service.py` — import_from_df() 패턴 참고
- `/test/LARS/.WebUI/package.json` — 현재 설치된 패키지 목록

---

## Phase 2 완료 기준 (검증 시퀀스)

다음 순서로 전체 플로우가 동작해야 한다:

1. `http://localhost:3000/login` → 이메일/비밀번호 입력 → JWT 발급 → `/dashboard` 이동
2. `/import` → DP Excel 파일 업로드 → 미리보기 → 처리 → 성공 메시지
3. DP 처리 완료 시 PL 자동 계산 → `part_list_snapshots` DB 적재 확인
4. `/psi` → PSI 매트릭스 테이블 렌더링 (행=IT 품목, 열=날짜, 셀=required_qty)
5. PSI 셀의 `available_qty` 인라인 편집 → 저장 → `shortage_qty` 계산 후 하이라이트
6. `/bom` → 모델 목록 → 클릭 → BOMDetail (flat list로 계층 구조 시각화)

---

## 전역 제약 조건

- **DataFrame 처리**: Polars만 사용 (pandas import 절대 금지)
- **DB 쓰기**: SQLModel ORM 또는 `session.execute(sa.insert(...))` (서비스 레이어 raw SQL 금지)
- **비동기**: 모든 FastAPI 라우트와 서비스 함수는 `async def`
- **타입 힌트**: 모든 public 함수에 타입 힌트 필수
- **프론트엔드 HTTP**: axios 클라이언트 통해서만 API 호출 (fetch 직접 사용 금지)
- **상태 관리**: Zustand store (전역 auth 상태), TanStack Query (서버 데이터)
- **CSS**: Tailwind CSS v4 + shadcn/ui 컴포넌트 (현재 설치된 버전 그대로 사용)

---

## Task 2-A: DB 마이그레이션 002 + 모델 업데이트

### 목적
`daily_plan_lots` 테이블에 `daily_qty_json` 컬럼 추가.  
CSV 형식 DP 파일에는 날짜별 수량 정보(예: `{"2026-04-22": 5, "2026-04-23": 8}`)가 있고, 이를 PSI 계산에 활용해야 함.

### 작업 내용

**MODIFY: `backend/models/daily_plan.py`**  
`DailyPlanLot` 클래스에 아래 필드 추가:
```python
daily_qty_json: Optional[str] = Field(default=None)  # JSON string {"YYYY-MM-DD": qty, ...}
```

**CREATE: `backend/alembic/versions/002_add_daily_qty_json.py`**  
`down_revision = '773a5b8ef6e1'`로 설정.

```python
def upgrade() -> None:
    op.add_column('daily_plan_lots',
        sa.Column('daily_qty_json', sa.Text(), nullable=True)
    )

def downgrade() -> None:
    op.drop_column('daily_plan_lots', 'daily_qty_json')
```

### 검증
```bash
cd /test/LARS/backend && source venv/bin/activate
alembic upgrade head
# Expected: Running upgrade 773a5b8ef6e1 -> <new_rev>, 002_add_daily_qty_json
```

---

## Task 2-B: DP 서비스 + API + Import 파이프라인 업데이트

### 목적
- `daily_plan_service.py` — DP DataFrame을 DB에 저장하는 서비스
- `routes/dp.py` — DP 조회 API
- `import_pipeline.py` 수정 — daily_plan 처리 시 실제 DB 저장 + PL 재계산 트리거

### 입력 (daily_plan_parser.py 출력 컬럼 구조)

Excel 포맷:
```
wo_number: str, model_code: str, lot_number: str,
planned_qty: int, input_qty: int, output_qty: int,
plan_date: date, line_code: str, sort_order: int
```

CSV 포맷 (위 컬럼 + 추가):
```
daily_qty_json: str  # JSON 문자열, 없으면 '{}'
suffix: str
```

### CREATE: `backend/services/daily_plan_service.py`

```python
async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """
    DP DataFrame을 DB에 저장.
    Returns: 삽입된 DailyPlanLot 수
    """
```

구현 로직:
1. `line_code` 기준으로 `ProductionLine` 조회, 없으면 `ProductionLine(code=line_code, name=line_code)` 생성 후 flush
2. `plan_date + line_id` UNIQUE 기준으로 `DailyPlan` upsert. 기존 있으면 `import_batch_id` 업데이트, 없으면 생성
3. 기존 `DailyPlanLot`을 `plan_id` 기준으로 `DELETE` 후 새로 INSERT
4. `daily_qty_json` 컬럼이 df에 있으면 저장, 없으면 `'{}'`
5. `model_id`: `bom_models` 테이블에서 `model_code` 조회 후 FK 설정 (없으면 None)
6. `session.commit()` 후 삽입된 총 lot 수 반환

```python
async def list_plans(
    session: AsyncSession,
    date_from: Optional[date] = None,
    date_to: Optional[date] = None,
    line_code: Optional[str] = None
) -> List[dict]:
    """
    DailyPlan + ProductionLine 조인 조회.
    반환: [{plan_id, plan_date, line_code, line_name, lot_count, import_batch_id}]
    """

async def get_lots_by_plan(session: AsyncSession, plan_id: int) -> List[DailyPlanLot]:
    """plan_id로 DailyPlanLot 목록 조회 (sort_order 정렬)"""

async def get_dates_in_df(df: pl.DataFrame) -> List[date]:
    """DataFrame에서 unique plan_date 목록 추출 (PSI 재계산 트리거용)"""
```

### CREATE: `backend/schemas/daily_plan.py`

```python
class DailyPlanRead(BaseModel):
    plan_id: int
    plan_date: date
    line_code: str
    line_name: str
    lot_count: int
    import_batch_id: Optional[int]

class DailyPlanLotRead(BaseModel):
    id: int
    wo_number: Optional[str]
    model_code: str
    lot_number: str
    planned_qty: int
    input_qty: int
    output_qty: int
```

### CREATE: `backend/api/routes/dp.py`

```python
router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

GET  /dp?date_from=&date_to=&line_code=  → List[DailyPlanRead]
GET  /dp/{plan_id}/lots                  → List[DailyPlanLotRead]
```

### MODIFY: `backend/api/routes/import_pipeline.py`

`process_batch()` 내 `elif batch.target_table == "daily_plan":` 블록을 다음으로 교체:

```python
elif batch.target_table == "daily_plan":
    df = daily_plan_parser.parse(file_path)
    val_res = validator.validate_daily_plan(df)
    if not val_res.is_valid:
        raise Exception(f"Validation failed: {val_res.errors[0].message if val_res.errors else 'unknown'}")
    inserted = await daily_plan_service.import_from_df(session, df, batch.id)
    batch.records_inserted = inserted
    # PL 재계산 트리거 (part_list_service는 Task 2-C에서 구현)
    dates = await daily_plan_service.get_dates_in_df(df)
    await part_list_service.recompute_for_dates(session, dates, batch.id)
```

`import daily_plan_service, part_list_service` import 추가 필요.

### 검증
```bash
cd /test/LARS/backend && source venv/bin/activate && uvicorn main:app --reload &
# 테스트용 DP 파일 업로드
curl -X POST http://localhost:8000/api/v1/import/upload \
  -H "Authorization: Bearer <token>" \
  -F "file=@/test/AutoReport/DPDB/Excel_Export_[0104_151307].xlsx" \
  -F "target_table=daily_plan"
# batch_id 반환 확인
curl http://localhost:8000/api/v1/import/preview/{batch_id} -H "Authorization: Bearer <token>"
curl -X POST http://localhost:8000/api/v1/import/batches/{batch_id}/process -H "Authorization: Bearer <token>"
curl "http://localhost:8000/api/v1/dp" -H "Authorization: Bearer <token>"
# Expected: plan 목록 반환 (records_inserted > 0)
```

---

## Task 2-C: PL 계산 서비스 + API

### 목적
BOM × DP 로트별 부품 소모량 계산 → `part_list_snapshots` 저장.

### 비즈니스 로직
- 각 `DailyPlanLot`의 `model_code`로 `BomModel` 조회 → `BomItem` 목록 가져옴
- `required_qty = bom_item.qty × lot.planned_qty`
- 같은 날짜 내 동일 `description`의 수량 합산 (PL 조회 시 집계)
- `PartListSnapshot` 저장: `lot_id`, `part_number`, `description`, `required_qty`, `snapshot_date`, `uom`, `vendor_raw`, `import_batch_id`

### CREATE: `backend/services/part_list_service.py`

```python
async def recompute_for_dates(
    session: AsyncSession,
    dates: List[date],
    batch_id: int
) -> int:
    """
    주어진 날짜들의 DailyPlanLot × BomItem을 계산해 part_list_snapshots에 저장.
    Returns: 총 삽입된 snapshot 레코드 수
    """
```

구현 로직:
1. `dates` 내 모든 `DailyPlan.plan_date`에 해당하는 `DailyPlanLot` 조회
2. 해당 lots의 기존 `PartListSnapshot` 레코드 삭제 (`lot_id IN (...)`)
3. 각 lot의 `model_code`로 `BomModel` 조회, 없으면 건너뜀
4. 해당 모델의 `BomItem` 전체 조회
5. `PartListSnapshot` 생성:
   - `lot_id = lot.id`
   - `part_number = bom_item.part_number`
   - `description = bom_item.description`
   - `required_qty = bom_item.qty × lot.planned_qty` (float)
   - `snapshot_date = lot의 DailyPlan.plan_date`
   - `uom = bom_item.uom`
   - `vendor_raw = bom_item.vendor_raw`
   - `import_batch_id = batch_id`
6. `session.add_all(snapshots)` 후 `commit()`
7. PSI required_qty 재계산 트리거: `await psi_service.recompute_required_for_dates(session, dates)`

```python
async def get_pl_summary(
    session: AsyncSession,
    plan_date: date,
    line_code: Optional[str] = None
) -> List[dict]:
    """
    plan_date 기준 part_list_snapshots 집계.
    Returns: [{part_number, description, total_required_qty, uom, vendor_raw}]
    description 기준 required_qty 합산, 내림차순 정렬
    """

async def export_pl_to_df(session: AsyncSession, plan_date: date) -> pl.DataFrame:
    """PL 집계 결과를 Polars DataFrame으로 반환 (Excel export용)"""
```

### CREATE: `backend/schemas/part_list.py`

```python
class PartListItem(BaseModel):
    part_number: str
    description: Optional[str]
    total_required_qty: float
    uom: str
    vendor_raw: Optional[str]

class PartListResponse(BaseModel):
    plan_date: date
    line_code: Optional[str]
    items: List[PartListItem]
    total_items: int
```

### CREATE: `backend/api/routes/pl.py`

```python
router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

GET /pl?plan_date=YYYY-MM-DD&line_code=  → PartListResponse
POST /pl/compute                         → {"computed": int} (선택된 날짜 수동 재계산)
GET /pl/export?plan_date=YYYY-MM-DD      → StreamingResponse (Excel 파일)
```

`/pl/export` 구현 시: `export_pl_to_df()`로 DataFrame 생성 후 `polars.write_excel()`으로 바이트 반환.  
`StreamingResponse(content=excel_bytes, media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")`

### 검증
```bash
curl "http://localhost:8000/api/v1/pl?plan_date=2026-01-04" -H "Authorization: Bearer <token>"
# Expected: items 목록, total_required_qty > 0
```

---

## Task 2-D: IT 품목 마스터 서비스 + API

### 목적
IT(Item Tracking) 관리자가 추적 대상 부품 목록을 관리함. BOM의 부품 중 수급 관리가 필요한 품목만 등록.

### CREATE: `backend/services/item_master_service.py`

```python
async def list_items(
    session: AsyncSession,
    search: Optional[str] = None,
    is_active: bool = True
) -> List[ItemMaster]:
    """search는 description 또는 part_number 부분 일치"""

async def get_item(session: AsyncSession, item_id: int) -> Optional[ItemMaster]:
    pass

async def create_item(session: AsyncSession, data: ItemMasterCreate, user_id: int) -> ItemMaster:
    """tracking_user_id = user_id 설정"""

async def update_item(session: AsyncSession, item_id: int, data: ItemMasterUpdate) -> Optional[ItemMaster]:
    pass

async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """
    IT 엑셀에서 파싱된 DataFrame upsert.
    part_number 기준 upsert (기존 레코드 update, 없으면 insert).
    Returns: 처리된 레코드 수
    """

async def get_bom_usage(session: AsyncSession, item_id: int) -> List[dict]:
    """
    해당 IT 품목이 사용된 BOM 모델 목록.
    ItemMaster.part_number 기준으로 BomItem 역조회.
    Returns: [{model_code, description, qty, level, path}]
    """
```

### CREATE: `backend/schemas/item_master.py`

```python
class ItemMasterRead(BaseModel):
    id: int
    level: int
    description: str
    part_number: str
    vendor_raw: Optional[str]
    tracking_user_id: Optional[int]
    is_active: bool

class ItemMasterCreate(BaseModel):
    level: int = 1
    description: str
    part_number: str
    vendor_raw: Optional[str] = None

class ItemMasterUpdate(BaseModel):
    description: Optional[str] = None
    vendor_raw: Optional[str] = None
    is_active: Optional[bool] = None

class ItemBomUsage(BaseModel):
    model_code: str
    description: Optional[str]
    qty: float
    level: int
    path: str
```

### CREATE: `backend/api/routes/items.py`

```python
router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

GET    /items                     → List[ItemMasterRead]
POST   /items                     → ItemMasterRead
GET    /items/{item_id}           → ItemMasterRead
PUT    /items/{item_id}           → ItemMasterRead
GET    /items/{item_id}/bom-usage → List[ItemBomUsage]
POST   /items/import              → {"imported": int}  # Form: file upload
```

`POST /items/import`: UploadFile을 받아 임시 저장 후 파싱 → `import_from_df()` 호출.  
IT 엑셀 컬럼 매핑: `Level`→level, `품명`→description, `품번`→part_number, `업체`→vendor_raw

### 검증
```bash
curl -X POST http://localhost:8000/api/v1/items \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"level": 1, "description": "테스트 부품", "part_number": "TEST001", "vendor_raw": "테스트업체"}'
# Expected: ItemMasterRead 반환 (id 포함)
curl http://localhost:8000/api/v1/items -H "Authorization: Bearer <token>"
# Expected: 방금 생성한 항목 포함 목록
```

---

## Task 2-E: PSI 서비스 + API

### 목적
PSI(Physical Supply Index) 매트릭스: 행=IT 품목, 열=날짜, 값=required_qty/available_qty/shortage_qty

### shortage_qty 처리 방침
PostgreSQL `GENERATED ALWAYS AS` 컬럼은 현재 마이그레이션에 없음. **서비스 레이어에서 계산**:
```python
shortage_qty = (available_qty or 0) - required_qty
# shortage_qty < 0 이면 부족 상태
```

### CREATE: `backend/services/psi_service.py`

```python
async def recompute_required_for_dates(
    session: AsyncSession,
    dates: List[date]
) -> int:
    """
    주어진 날짜들의 part_list_snapshots에서 required_qty를 집계하여 psi_records 업데이트.
    매핑: PartListSnapshot.part_number → ItemMaster.part_number
    Returns: upsert된 레코드 수
    """
```

구현 로직:
1. `dates` 내 `part_list_snapshots` 조회 (`snapshot_date IN dates`)
2. `part_number` 기준으로 `ItemMaster` 조회 (IN 쿼리, 한 번에)
3. 없는 part_number는 건너뜀 (IT에 등록된 품목만 PSI 추적)
4. `(item_id, psi_date)` 기준으로 `required_qty` 합산
5. 기존 `PsiRecord` 있으면 `required_qty` 업데이트 (available_qty는 보존), 없으면 INSERT
6. `session.commit()`

```python
async def get_matrix(
    session: AsyncSession,
    date_from: date,
    date_to: date,
    item_ids: Optional[List[int]] = None
) -> dict:
    """
    PSI 매트릭스 데이터 반환.
    Returns: {
        "dates": ["2026-01-04", "2026-01-05", ...],
        "items": [{"id": 1, "part_number": "...", "description": "..."}, ...],
        "cells": {
            "1_2026-01-04": {"required_qty": 10, "available_qty": 8, "shortage_qty": -2},
            ...
        }
    }
    """

async def update_cell(
    session: AsyncSession,
    item_id: int,
    psi_date: date,
    available_qty: float,
    notes: Optional[str],
    user_id: int
) -> PsiRecord:
    """
    PSI 셀의 available_qty 업데이트.
    기존 레코드 없으면 생성 (required_qty=0으로).
    """

async def get_shortage_summary(session: AsyncSession, as_of_date: date) -> List[dict]:
    """
    as_of_date 기준으로 shortage_qty < 0인 항목 목록.
    Returns: [{item_id, part_number, description, psi_date, required_qty, available_qty, shortage_qty}]
    """
```

### CREATE: `backend/schemas/psi.py`

```python
class PsiCellRead(BaseModel):
    required_qty: float
    available_qty: Optional[float]
    shortage_qty: float  # 서비스에서 계산: (available_qty or 0) - required_qty

class PsiMatrixResponse(BaseModel):
    dates: List[str]  # "YYYY-MM-DD" 형식
    items: List[dict]  # {"id": int, "part_number": str, "description": str}
    cells: Dict[str, PsiCellRead]  # key: "{item_id}_{YYYY-MM-DD}"

class PsiCellUpdate(BaseModel):
    available_qty: float
    notes: Optional[str] = None

class PsiShortageItem(BaseModel):
    item_id: int
    part_number: str
    description: Optional[str]
    psi_date: date
    required_qty: float
    available_qty: Optional[float]
    shortage_qty: float
```

### CREATE: `backend/api/routes/psi.py`

```python
router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

GET  /psi?date_from=YYYY-MM-DD&date_to=YYYY-MM-DD  → PsiMatrixResponse
PUT  /psi/{item_id}/{psi_date}                      → PsiCellRead  (Body: PsiCellUpdate)
POST /psi/recompute                                 → {"recomputed": int}
GET  /psi/shortage?as_of_date=YYYY-MM-DD            → List[PsiShortageItem]
```

`PUT /psi/{item_id}/{psi_date}`: `psi_date` 파라미터는 `date` 타입으로 FastAPI가 자동 변환.  
업데이트 후 `shortage_qty` 재계산하여 응답에 포함.

### 검증
```bash
# PSI 매트릭스 조회 (DP import 후 실행)
curl "http://localhost:8000/api/v1/psi?date_from=2026-01-04&date_to=2026-01-10" \
  -H "Authorization: Bearer <token>"
# Expected: dates 배열, items 배열, cells 딕셔너리 반환

# available_qty 업데이트
curl -X PUT "http://localhost:8000/api/v1/psi/1/2026-01-04" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"available_qty": 50}'
# Expected: shortage_qty 계산된 PsiCellRead 반환
```

---

## Task 2-F: 효율표 + 표준재공 + 대시보드 + Admin API

### 목적
나머지 4개 API 그룹을 간결하게 구현. Phase 2에서는 기본 CRUD + 조회만.

### CREATE: `backend/services/efficiency_service.py`

```python
async def list_efficiency(
    session: AsyncSession,
    date_from: Optional[date] = None,
    date_to: Optional[date] = None
) -> List[dict]:
    """Worker + ItemMaster + LogisticsEfficiency 조인 조회"""

async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """
    엑셀 파싱 df: worker_name(str), item_part_number(str), recorded_date(date),
                  target_qty(float), actual_qty(float)
    Worker를 name으로 조회 또는 생성, ItemMaster를 part_number로 조회
    """
```

### CREATE: `backend/services/wip_service.py`

```python
async def list_wip(session: AsyncSession, location_code: Optional[str] = None) -> List[dict]:
    """FactoryLocation + ItemMaster + StandardWip 조인 조회"""

async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """
    엑셀 파싱 df: location_code(str), item_part_number(str), target_qty(float), safety_stock(float)
    """

async def list_locations(session: AsyncSession) -> List[FactoryLocation]:
    pass
```

### CREATE: `backend/api/routes/efficiency.py`

```python
router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

GET  /efficiency?date_from=&date_to=  → List[dict]
POST /efficiency/import               → {"imported": int}  # UploadFile
```

### CREATE: `backend/api/routes/wip.py`

```python
router = APIRouter(dependencies=[Depends(require_role("internal", "manager", "admin"))])

GET  /wip?location_code=  → List[dict]
GET  /wip/locations       → List[dict]  # [{id, code, name, zone}]
POST /wip/import          → {"imported": int}
```

### CREATE: `backend/api/routes/dashboard.py`

```python
router = APIRouter()

@router.get("/summary", dependencies=[Depends(get_current_user)])
async def get_summary(session: AsyncSession = Depends(get_session)) -> dict:
    """
    Returns:
    {
        "total_bom_models": int,   # bom_models 테이블 count
        "total_it_items": int,      # item_master 테이블 count
        "shortage_count": int,      # psi_records에서 (available_qty or 0) - required_qty < 0 인 건수
        "recent_imports": List[{source_name, target_table, status, finished_at}]  # 최근 5건
    }
    """
```

### CREATE: `backend/api/routes/admin.py`

```python
router = APIRouter(dependencies=[Depends(require_role("admin"))])

GET  /admin/users              → List[dict]  # [{id, email, display_name, role, is_active}]
POST /admin/users              → dict        # 신규 사용자 생성
PUT  /admin/users/{user_id}    → dict        # role, is_active 변경
GET  /admin/vendors            → List[dict]
POST /admin/vendors            → dict
```

신규 사용자 생성 시 `core/security.py`의 `hash_password()` 사용.

### CREATE: `backend/api/routes/ws.py`

```python
from fastapi import APIRouter, WebSocket, WebSocketDisconnect
from typing import List

router = APIRouter()

class ConnectionManager:
    def __init__(self):
        self.active_connections: List[WebSocket] = []

    async def connect(self, websocket: WebSocket):
        await websocket.accept()
        self.active_connections.append(websocket)

    def disconnect(self, websocket: WebSocket):
        self.active_connections.remove(websocket)

    async def broadcast(self, message: dict):
        import json
        for connection in self.active_connections:
            try:
                await connection.send_text(json.dumps(message))
            except Exception:
                pass

manager = ConnectionManager()

@router.websocket("/ws/dashboard")
async def websocket_dashboard(websocket: WebSocket):
    await manager.connect(websocket)
    try:
        while True:
            data = await websocket.receive_text()  # keep-alive ping
    except WebSocketDisconnect:
        manager.disconnect(websocket)
```

`manager`를 다른 서비스에서 import하여 `await ws.manager.broadcast({"type": "import_complete", ...})` 형태로 사용.

---

## Task 2-G: Backend 라우터 통합 업데이트

### MODIFY: `backend/api/router.py`

현재 파일 전체를 아래로 교체:

```python
from fastapi import APIRouter
from api.routes import auth, bom, import_pipeline, dp, pl, items, psi, efficiency, wip, dashboard, admin
from api.routes.ws import router as ws_router

router = APIRouter(prefix="/api/v1")
router.include_router(auth.router, prefix="/auth", tags=["auth"])
router.include_router(bom.router, prefix="/bom", tags=["bom"])
router.include_router(import_pipeline.router, prefix="/import", tags=["import"])
router.include_router(dp.router, prefix="/dp", tags=["dp"])
router.include_router(pl.router, prefix="/pl", tags=["pl"])
router.include_router(items.router, prefix="/items", tags=["items"])
router.include_router(psi.router, prefix="/psi", tags=["psi"])
router.include_router(efficiency.router, prefix="/efficiency", tags=["efficiency"])
router.include_router(wip.router, prefix="/wip", tags=["wip"])
router.include_router(dashboard.router, prefix="/dashboard", tags=["dashboard"])
router.include_router(admin.router, prefix="/admin", tags=["admin"])
```

### MODIFY: `backend/main.py`

`app.include_router(api_router)` 아래에 WebSocket 라우터 추가:

```python
from api.routes.ws import router as ws_router
app.include_router(ws_router)
```

### 백엔드 전체 검증
```bash
cd /test/LARS/backend && source venv/bin/activate
uvicorn main:app --reload --port 8000
curl http://localhost:8000/openapi.json | python3 -c "
import json, sys
data = json.load(sys.stdin)
paths = list(data['paths'].keys())
required = ['/api/v1/dp', '/api/v1/pl', '/api/v1/items', '/api/v1/psi', '/api/v1/efficiency', '/api/v1/wip', '/api/v1/dashboard/summary', '/api/v1/admin/users']
missing = [r for r in required if not any(p.startswith(r) for p in paths)]
print('Missing routes:', missing)
assert not missing, 'Some routes not registered!'
print('All routes registered successfully')
"
```

---

## Task 2-H: 프론트엔드 패키지 설치 + 기반 구조 재설정

### 목적
현재 `/test/LARS/.WebUI/`는 mock 데이터 기반 단일 페이지. React Router + Auth + API 클라이언트 기반으로 완전 재설정.

### STEP 1: 패키지 설치
```bash
cd /test/LARS/.WebUI
npm install react-router-dom@6 @tanstack/react-query@5 zustand axios react-dropzone
npm install -D @types/react-router-dom
```

### STEP 2: 디렉토리 구조 생성
기존 파일 `src/App.tsx`는 완전 교체. 기존 `src/components/`, `src/lib/`, `src/types/` 내 mock 관련 파일은 보존하되 사용하지 않음.

생성할 디렉토리/파일:
```
src/
├── api/
│   └── client.ts         # axios 인스턴스 + 인터셉터
├── stores/
│   └── auth.ts           # Zustand auth store
├── hooks/
│   └── useAuth.ts        # auth store selector hook
├── pages/
│   ├── LoginPage.tsx
│   ├── DashboardPage.tsx
│   ├── BOMListPage.tsx
│   ├── BOMDetailPage.tsx
│   ├── DailyPlanPage.tsx
│   ├── PartListPage.tsx
│   ├── ItemMasterPage.tsx
│   ├── PSIPage.tsx
│   ├── EfficiencyPage.tsx
│   ├── WIPPage.tsx
│   ├── ImportPage.tsx
│   ├── TicketListPage.tsx
│   ├── AdminPage.tsx
│   └── AIChatPage.tsx
├── components/
│   ├── layout/
│   │   ├── AppLayout.tsx     # 사이드바 + 헤더 + 메인 영역
│   │   └── ProtectedRoute.tsx
│   ├── bom/
│   │   └── BOMTree.tsx       # BOM 계층 트리 컴포넌트
│   └── psi/
│       └── PSIMatrix.tsx     # PSI 매트릭스 (인라인 편집)
└── App.tsx                   # 라우터 설정
```

### CREATE: `src/api/client.ts`

```typescript
import axios from 'axios';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:8000/api/v1';

export const apiClient = axios.create({
  baseURL: API_BASE,
  headers: { 'Content-Type': 'application/json' },
});

// Request interceptor: Authorization 헤더 주입
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('access_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Response interceptor: 401 시 refresh token으로 재시도
apiClient.interceptors.response.use(
  (res) => res,
  async (error) => {
    const originalRequest = error.config;
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;
      try {
        const refreshToken = localStorage.getItem('refresh_token');
        const res = await axios.post(`${API_BASE}/auth/refresh`, { refresh_token: refreshToken });
        const { access_token } = res.data;
        localStorage.setItem('access_token', access_token);
        originalRequest.headers.Authorization = `Bearer ${access_token}`;
        return apiClient(originalRequest);
      } catch {
        localStorage.removeItem('access_token');
        localStorage.removeItem('refresh_token');
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);
```

### CREATE: `src/stores/auth.ts`

```typescript
import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface UserInfo {
  id: number;
  email: string;
  display_name: string;
  role: string;
}

interface AuthState {
  user: UserInfo | null;
  isAuthenticated: boolean;
  login: (accessToken: string, refreshToken: string, user: UserInfo) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      isAuthenticated: false,
      login: (accessToken, refreshToken, user) => {
        localStorage.setItem('access_token', accessToken);
        localStorage.setItem('refresh_token', refreshToken);
        set({ user, isAuthenticated: true });
      },
      logout: () => {
        localStorage.removeItem('access_token');
        localStorage.removeItem('refresh_token');
        set({ user: null, isAuthenticated: false });
      },
    }),
    { name: 'lars-auth' }
  )
);
```

### MODIFY: `src/App.tsx` (완전 교체)

```typescript
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProtectedRoute } from './components/layout/ProtectedRoute';
import { AppLayout } from './components/layout/AppLayout';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import BOMListPage from './pages/BOMListPage';
import BOMDetailPage from './pages/BOMDetailPage';
import DailyPlanPage from './pages/DailyPlanPage';
import PartListPage from './pages/PartListPage';
import ItemMasterPage from './pages/ItemMasterPage';
import PSIPage from './pages/PSIPage';
import EfficiencyPage from './pages/EfficiencyPage';
import WIPPage from './pages/WIPPage';
import ImportPage from './pages/ImportPage';
import AdminPage from './pages/AdminPage';
import AIChatPage from './pages/AIChatPage';

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, staleTime: 30000 } },
});

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<ProtectedRoute />}>
            <Route element={<AppLayout />}>
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/bom" element={<BOMListPage />} />
              <Route path="/bom/:modelCode" element={<BOMDetailPage />} />
              <Route path="/dp" element={<DailyPlanPage />} />
              <Route path="/pl" element={<PartListPage />} />
              <Route path="/items" element={<ItemMasterPage />} />
              <Route path="/psi" element={<PSIPage />} />
              <Route path="/efficiency" element={<EfficiencyPage />} />
              <Route path="/wip" element={<WIPPage />} />
              <Route path="/import" element={<ImportPage />} />
              <Route path="/ai" element={<AIChatPage />} />
              <Route path="/admin" element={<AdminPage />} />
              <Route path="/" element={<Navigate to="/dashboard" replace />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
```

### CREATE: `src/components/layout/ProtectedRoute.tsx`

```typescript
import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../../stores/auth';

export function ProtectedRoute() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />;
}
```

### CREATE: `src/components/layout/AppLayout.tsx`

사이드바 + 헤더 레이아웃. 데스크탑: 왼쪽 고정 사이드바(240px). 모바일(768px 미만): 하단 탭바.

사이드바 메뉴 항목:
```
Dashboard   → /dashboard
BOM         → /bom
DP          → /dp
PL          → /pl
IT 품목     → /items
PSI         → /psi
효율표      → /efficiency
표준재공    → /wip
Import      → /import
AI 어시스턴트 → /ai
관리자      → /admin  (role === 'admin'인 경우만 표시)
```

헤더: 현재 사용자 이름(display_name) + 역할(role) + 로그아웃 버튼.

`<main>` 영역에 `<Outlet />` 렌더링.

### CREATE: `src/main.tsx` 수정 불필요, `src/vite-env.d.ts`에 추가:
```typescript
interface ImportMetaEnv {
  readonly VITE_API_BASE: string;
}
```

### CREATE: `.WebUI/.env.local`
```
VITE_API_BASE=http://localhost:8000/api/v1
```

---

## Task 2-I: 인증 페이지 + BOM 페이지 + Import 페이지

### CREATE: `src/pages/LoginPage.tsx`

- 이메일 + 비밀번호 form
- `apiClient.post('/auth/login', {email, password})` 호출
- 성공: `useAuthStore.login(access_token, refresh_token, user)` 후 `/dashboard`로 이동
- 실패: 에러 메시지 표시 ("이메일 또는 비밀번호가 올바르지 않습니다")
- 디자인: 화면 중앙 카드, LARS 로고 텍스트, 다크 테마 유지

### CREATE: `src/pages/BOMListPage.tsx`

```typescript
// useQuery: GET /bom/models?search=&is_active=true
// 검색 input (debounce 300ms)
// 테이블: model_code, description, version, 상세보기 버튼
// 상세보기 클릭 → useNavigate('/bom/:modelCode')
```

### CREATE: `src/pages/BOMDetailPage.tsx`

```typescript
// useParams: modelCode
// useQuery: GET /bom/models/:modelCode
// BOMTree 컴포넌트에 items 전달
// 역조회 섹션: part_number 입력 → GET /bom/reverse?part_number= → 모델 목록 표시
```

### CREATE: `src/components/bom/BOMTree.tsx`

```typescript
// Props: items: BomItemRead[]  (flat list, path 컬럼으로 계층 결정)
// 렌더링: level에 따라 들여쓰기 (1rem × level)
// 각 행: level 표시(뱃지), part_number, description, qty, uom, vendor_raw
// supply_type === 'S' 이면 "대체품" 뱃지 표시
// 펼침/접힘은 구현하지 않음 (flat list 그대로 표시, 들여쓰기로 시각화)
```

### CREATE: `src/pages/ImportPage.tsx`

Import 플로우 UI (3단계):

**STEP 1: 업로드**
- `react-dropzone` 사용 드래그앤드롭 영역
- target_table 선택 (radio: BOM / 일일계획 / IT 품목)
- `POST /import/upload` 호출 → `batch_id` 저장

**STEP 2: 미리보기**
- `GET /import/preview/{batch_id}` 호출
- 총 행수, 유효 행수, 오류 행수 표시
- 상위 20행 테이블 (오류 행은 빨간색 배경)
- "처리 시작" 버튼

**STEP 3: 처리**
- `POST /import/batches/{batch_id}/process` 호출
- 로딩 스피너 (처리 중 버튼 비활성화)
- 성공: "✓ {records_inserted}건 처리 완료" 메시지
- 실패: error_log 내용 표시
- "다시 Import" 버튼으로 STEP 1로 복귀

### CREATE: `src/pages/DailyPlanPage.tsx`

```typescript
// 날짜 범위 필터 (date_from, date_to input)
// useQuery: GET /dp?date_from=&date_to=
// 테이블: plan_date, line_code, lot_count
// 행 클릭 → 해당 plan의 lot 목록 모달 or 펼침 (GET /dp/{plan_id}/lots)
```

---

## Task 2-J: PSI 매트릭스 페이지 + 나머지 페이지

### CREATE: `src/pages/PSIPage.tsx`

```typescript
// 날짜 범위 선택 (기본: 오늘 기준 -7일 ~ +14일)
// useQuery: GET /psi?date_from=&date_to=
// PSIMatrix 컴포넌트에 데이터 전달
// useMutation: PUT /psi/{item_id}/{psi_date} (인라인 편집용)
```

### CREATE: `src/components/psi/PSIMatrix.tsx`

```typescript
// Props:
//   data: PsiMatrixResponse
//   onCellUpdate: (itemId: number, date: string, value: number) => Promise<void>

// 렌더링:
//   고정 첫 열: 품번 + 품명 (sticky left)
//   날짜 열: 스크롤 가능
//   각 셀: required_qty / available_qty (편집 가능) / shortage_qty
//   shortage_qty < 0: 셀 배경 빨간색 (bg-red-900/40)
//   shortage_qty === 0: 녹색 (bg-green-900/20)
//   available_qty 셀: 클릭 시 숫자 input으로 전환, blur/Enter 시 onCellUpdate 호출

// 모바일 대응: 첫 열 고정(sticky), 가로 스크롤
```

### CREATE: `src/pages/DashboardPage.tsx`

```typescript
// useQuery: GET /dashboard/summary
// 4개 카드: BOM 모델 수, IT 품목 수, 수급 부족 항목 수, 최근 Import 건수
// 최근 Import 목록 테이블 (상위 5건): source_name, target_table, status, finished_at
// WebSocket 연결: ws://localhost:8000/ws/dashboard
//   → import_complete 이벤트 수신 시 쿼리 refetch
```

### CREATE: `src/pages/ItemMasterPage.tsx`

```typescript
// 검색 input + useQuery: GET /items?search=
// 테이블: level, part_number, description, vendor_raw, is_active
// "+ 추가" 버튼 → 모달 폼 (POST /items)
// Import 버튼 → /import 페이지로 이동 (target_table=item_master 파라미터)
// 행 클릭 → BOM 사용처 조회 (GET /items/{id}/bom-usage) → 드로어 or 모달
```

### CREATE: `src/pages/PartListPage.tsx`

```typescript
// 날짜 선택 input (plan_date)
// useQuery: GET /pl?plan_date=YYYY-MM-DD
// 테이블: part_number, description, total_required_qty, uom, vendor_raw
// "Excel 다운로드" 버튼 → GET /pl/export?plan_date= → Blob 다운로드
```

### CREATE: `src/pages/EfficiencyPage.tsx`

```typescript
// 날짜 범위 필터
// useQuery: GET /efficiency?date_from=&date_to=
// 테이블: worker_name, item_description, recorded_date, target_qty, actual_qty, 달성률
// Import 버튼
```

### CREATE: `src/pages/WIPPage.tsx`

```typescript
// location_code 필터 (GET /wip/locations로 드롭다운 populate)
// useQuery: GET /wip?location_code=
// 테이블: location_code, item_part_number, item_description, target_qty, safety_stock
// Import 버튼
```

### CREATE: `src/pages/AdminPage.tsx`

```typescript
// role !== 'admin' 이면 "접근 권한 없음" 표시
// 탭: 사용자 관리 / 공급업체 관리
// 사용자 테이블: email, display_name, role, is_active + 역할 변경 버튼
// POST /admin/users 폼: email, display_name, role, password
```

### CREATE: `src/pages/AIChatPage.tsx`

```typescript
// Phase 3 구현 예정 - 현재는 "AI 기능은 Phase 3에서 구현됩니다" 안내 메시지만 표시
// 채팅 UI 레이아웃만 스켈레톤으로 구성 (입력창 + 전송 버튼, 비활성화 상태)
```

### CREATE: `src/pages/TicketListPage.tsx`

```typescript
// Phase 3 구현 예정 - "티켓 관리는 Phase 3에서 구현됩니다" 안내
// 빈 테이블 UI 스켈레톤
```

---

## Task 2-K: 통합 검증

### 검증 순서

**1단계: 백엔드 구동 확인**
```bash
cd /test/LARS && docker compose up -d  # PostgreSQL + Redis + Ollama
cd backend && source venv/bin/activate
alembic upgrade head  # 002 마이그레이션 적용 확인
uvicorn main:app --reload --port 8000 &
curl http://localhost:8000/health
# Expected: {"status": "ok", "db": "connected", "redis": "pending"}
```

**2단계: API 엔드포인트 등록 확인**
```bash
curl http://localhost:8000/openapi.json | python3 -c "
import json, sys
data = json.load(sys.stdin)
paths = list(data['paths'].keys())
print(f'Total endpoints: {len(paths)}')
for p in sorted(paths):
    print(p)
"
# Expected: /api/v1/dp, /api/v1/pl, /api/v1/items, /api/v1/psi 등 모두 포함
```

**3단계: 프론트엔드 구동**
```bash
cd /test/LARS/.WebUI && npm run dev &
# http://localhost:3000 접근 → /login 리다이렉트 확인
```

**4단계: 로그인 플로우**
```
http://localhost:3000/login
이메일: admin@lars.local
비밀번호: admin123  (Phase 1에서 create_admin.py로 생성된 계정)
→ /dashboard 이동 확인
→ 4개 요약 카드 렌더링 확인
```

**5단계: BOM 조회**
```
http://localhost:3000/bom
→ Phase 1에서 import된 모델 목록 표시 확인
→ 모델 클릭 → BOMDetail → BOMTree 계층 표시 확인
```

**6단계: DP Import + PL 자동 계산**
```
http://localhost:3000/import
→ target_table: "일일계획" 선택
→ /test/AutoReport/DPDB/ 의 Excel 파일 업로드
→ 미리보기 확인 → 처리 시작
→ 처리 완료 메시지 확인
→ http://localhost:3000/dp → 플랜 목록 확인
→ http://localhost:3000/pl?plan_date=... → 부품 소모량 목록 확인
```

**7단계: PSI 매트릭스**
```
http://localhost:3000/items
→ IT 품목 추가 (part_number가 BOM에 있는 부품번호 사용)
→ http://localhost:3000/psi
→ 날짜 범위 선택 → 매트릭스 렌더링 확인
→ available_qty 셀 클릭 → 숫자 입력 → blur → 셀 업데이트 확인
→ shortage_qty < 0 셀 빨간색 확인
```

**8단계: 타입 체크**
```bash
cd /test/LARS/.WebUI && npx tsc --noEmit
# Expected: 오류 0건
```

---

## 특이사항 및 주의점

1. **PSI-IT 연결**: PSI 재계산 시 `ItemMaster.part_number` = `PartListSnapshot.part_number` 정확히 일치해야 함. 대소문자, 공백 trim 처리 필요.

2. **DailyPlan UNIQUE constraint**: `(plan_date, line_id)` UNIQUE. 같은 날짜+라인으로 재 import 시 기존 DailyPlan을 업데이트하고 lots는 삭제 후 재삽입.

3. **PL 계산 BOM 없는 모델**: DP에는 있지만 BOM import가 안 된 모델은 PL 계산 건너뜀. 로그에 경고 출력.

4. **PSI 날짜 범위**: 기본 조회 범위를 제한하지 않으면 데이터가 많을 때 응답이 느릴 수 있음. `date_from`과 `date_to` 파라미터를 필수로 만들거나 최대 90일로 제한 권장.

5. **프론트엔드 CORS**: 백엔드 `main.py`의 `allow_origins=["*"]`는 개발 환경에서 유지. 프로덕션에서는 환경변수로 제한.

6. **WebSocket 토큰 인증**: `ws://localhost:8000/ws/dashboard` 연결 시 토큰을 쿼리 파라미터로 전달: `?token=<access_token>`. Phase 2에서는 인증 없이 연결 허용 (Phase 4에서 보안 강화).

7. **모바일 반응형**: `md:` (768px) 브레이크포인트 기준. 모바일에서는 사이드바 숨김 + 하단 탭바. 테이블은 `overflow-x-auto`로 가로 스크롤. PSI 매트릭스 첫 열은 `sticky left-0` 적용.

---

## 완료 기준 체크리스트

- [ ] Task 2-A: alembic 002 마이그레이션 적용 완료, `daily_qty_json` 컬럼 DB에 존재
- [ ] Task 2-B: DP import 완료 후 `daily_plans`, `daily_plan_lots` 테이블에 데이터 적재
- [ ] Task 2-C: PL 계산 완료 후 `part_list_snapshots` 테이블에 데이터 적재, `/pl?plan_date=` API 응답 정상
- [ ] Task 2-D: `/items` CRUD 정상, `/items/{id}/bom-usage` 응답 정상
- [ ] Task 2-E: PSI 매트릭스 API 정상, PUT으로 available_qty 업데이트 시 shortage_qty 재계산
- [ ] Task 2-F: `/efficiency`, `/wip`, `/dashboard/summary`, `/admin/users`, `/ws/dashboard` 모두 등록
- [ ] Task 2-G: `openapi.json`에 모든 라우터 등록 확인
- [ ] Task 2-H: `npm run dev` 정상, `/login` → JWT 로그인 → `/dashboard` 이동 동작
- [ ] Task 2-I: BOMListPage 모델 목록 렌더링, BOMTree 계층 시각화, ImportPage 3단계 플로우 동작
- [ ] Task 2-J: PSIMatrix 인라인 편집, shortage 하이라이트 동작
- [ ] Task 2-K: `npx tsc --noEmit` 오류 0건, 전체 플로우 검증 완료

완료 후 `/test/LARS/LARS_Project/Phase2_Coder_Report.md`를 작성하여 Project Leader에게 보고하라.
