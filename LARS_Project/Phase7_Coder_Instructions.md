# Phase 7 Coder Instructions — ItemMaster 강화 (업체명 파싱 + Redis 캐싱 + 조건부 Background Rebuild + BOM 역조회)

> 작성일: 2026-05-16
> 작성자: Chief
> 대상: Coder (Gemini)
> 기준 문서: `LARS_Project/New_LARS_Project.md`

---

## 배경 및 목적

ItemMaster에 4가지 기능을 추가한다:

1. **업체명 파싱**: `vendor_raw`의 `EKHQ_서브원_KR131893` 형식에서 업체명(`서브원`)만 추출하여 표시
2. **Redis 캐싱**: 전체 ItemMaster를 Redis에 상시 캐싱, DB 왕복 없이 즉시 반환
3. **조건부 Background Rebuild**: BOM import_batch가 마지막 rebuild 이후 새로 추가된 경우에만 rebuild 실행, 비동기 Background로 실행
4. **BOM 역조회 강화**: 각 품목이 어떤 모델에서 얼마나 사용되는지 집계하여 UI에서 확인

---

## 현재 코드 현황 (Coder 필독)

| 파일 | 현황 |
|---|---|
| `backend/models/item_master.py` | `vendor_raw`, `lower_vendor_raw` 컬럼 존재 (원시 텍스트) |
| `backend/models/import_batch.py` | `target_table`, `status`, `finished_at` 컬럼 존재 |
| `backend/models/bom.py` | `BomModel.updated_at` 존재 |
| `backend/schemas/item_master.py` | `ItemMasterRead`에 `vendor_raw` 그대로 노출 |
| `backend/services/item_master_service.py` | `list_items()`, `rebuild_from_bom()`, `get_bom_usage()` 존재 |
| `backend/core/config.py` | `REDIS_URL: str` 설정됨, Redis 클라이언트 미구현 |
| `backend/requirements.txt` | `redis>=5.0.0` (asyncio 내장) 이미 포함 |
| `.WebUI/src/pages/ItemMasterPage.tsx` | `vendor_raw` 직접 표시, "사용처 조회" 버튼 미구현(alert) |

---

## Task 7-A: 업체명 파싱

### 파싱 규칙

`vendor_raw` 가 `^[A-Z]+_(.+)_KR\d+$` 패턴이면 중간 부분만 추출한다:

```
"EKHQ_서브원_KR131893"               → "서브원"
"EKHQ_(주)파라콤_KR120680"           → "(주)파라콤"
"EKHQ_경성정밀주식회사 주촌지점_KR112997" → "경성정밀주식회사 주촌지점"
"SAMWHA ELECTRIC Co., LTD"          → "SAMWHA ELECTRIC Co., LTD"  (원본 반환)
None                                → None
```

### 7-A-1: services/item_master_service.py에 helper 추가

```python
import re

_VENDOR_PATTERN = re.compile(r'^[A-Z]+_(.+)_KR\d+$')

def parse_vendor_name(vendor_raw: str | None) -> str | None:
    if not vendor_raw:
        return None
    m = _VENDOR_PATTERN.match(vendor_raw)
    return m.group(1) if m else vendor_raw
```

### 7-A-2: schemas/item_master.py — ItemMasterRead에 computed 필드 추가

```python
class ItemMasterRead(BaseModel):
    id: int
    level: int
    description: str
    part_number: str
    vendor_raw: Optional[str]
    vendor_name: Optional[str]        # ← 신규: parse_vendor_name(vendor_raw) 결과
    lower_vendor_raw: Optional[str]
    lower_vendor_name: Optional[str]  # ← 신규: parse_vendor_name(lower_vendor_raw) 결과
    inventory_qty: float
    defect_qty: float
    is_picked: bool
    tracking_user_id: Optional[int]
    is_active: bool
```

**DB 컬럼 추가 아님** — Alembic 마이그레이션 불필요.

### 7-A-3: services/item_master_service.py — _to_read() helper

```python
def _to_read(item: ItemMaster) -> ItemMasterRead:
    return ItemMasterRead(
        **item.model_dump(),
        vendor_name=parse_vendor_name(item.vendor_raw),
        lower_vendor_name=parse_vendor_name(item.lower_vendor_raw),
    )
```

`list_items()` → `[_to_read(i) for i in items]`  
`get_item()` → `_to_read(item) if item else None`

---

## Task 7-B: Redis 클라이언트 설정

### 7-B-1: core/redis_client.py (신규)

```python
import redis.asyncio as aioredis
from core.config import get_settings

_client: aioredis.Redis | None = None

async def get_redis() -> aioredis.Redis:
    global _client
    if _client is None:
        settings = get_settings()
        _client = aioredis.from_url(settings.REDIS_URL, decode_responses=True)
    return _client

async def close_redis():
    global _client
    if _client:
        await _client.aclose()
        _client = None
```

### 7-B-2: main.py — lifespan 등록

```python
from contextlib import asynccontextmanager
from core.redis_client import get_redis, close_redis

@asynccontextmanager
async def lifespan(app: FastAPI):
    await get_redis()   # startup
    yield
    await close_redis() # shutdown

app = FastAPI(lifespan=lifespan)
```

기존 `@app.on_event("startup")` 이 있으면 lifespan 내부로 통합.

---

## Task 7-C: ItemMaster Redis 캐싱

### 캐시 전략

| 항목 | 내용 |
|---|---|
| 캐시 키 | `itemmaster:all` |
| 저장 내용 | 활성(is_active=True) 전체 목록 JSON |
| TTL | 300초 (5분) |
| search 처리 | 캐시 데이터를 Python에서 필터링 (DB 미조회) |
| 무효화 시점 | rebuild 완료, create_item, update_item 호출 후 |

### 7-C-1: services/item_master_service.py — list_items() 수정

```
list_items(session, search, is_active):

  # inactive 요청은 캐시 bypass
  if not is_active:
      return await _db_list(session, search=None, is_active=False)

  redis = await get_redis()

  try:
      cached = await redis.get("itemmaster:all")
  except Exception:
      cached = None  # Redis 장애 시 DB fallback

  if cached:
      all_items = [ItemMasterRead(**d) for d in json.loads(cached)]
  else:
      db_items = await _db_list(session, search=None, is_active=True)
      all_items = [_to_read(i) for i in db_items]
      try:
          await redis.setex("itemmaster:all", 300,
                            json.dumps([r.model_dump() for r in all_items]))
      except Exception:
          pass  # Redis 장애 시 무시

  if search:
      q = search.lower()
      all_items = [r for r in all_items
                   if q in r.part_number.lower() or q in r.description.lower()]
  return all_items
```

캐시 무효화 helper:

```python
async def _invalidate_item_cache():
    try:
        redis = await get_redis()
        await redis.delete("itemmaster:all")
    except Exception:
        pass
```

`create_item()`, `update_item()` 마지막에 `await _invalidate_item_cache()` 호출.

---

## Task 7-D: 조건부 Background Rebuild

### 핵심 설계

```
rebuild은 다음 조건 모두 충족 시에만 실행:
  조건 1: import_batches 테이블에서
           SELECT MAX(finished_at) FROM import_batches
           WHERE target_table='bom' AND status='success'
           가 Redis의 'itemmaster:last_rebuild_at' 보다 최신인 경우
  조건 2: 현재 rebuild가 이미 실행 중(running)이 아닌 경우

조건 미충족 시 → {"status": "skipped", "reason": "BOM not updated since last rebuild"}
조건 충족 시 → Background Task로 실행, 즉시 {"status": "started"} 반환
```

### 7-D-1: Redis Progress 키 구조

```
itemmaster:rebuild_status (JSON, TTL 없음)
{
  "status": "idle" | "running" | "done" | "failed",
  "progress": 0~100,          // 완료 백분율
  "total": N,                  // 전체 처리 대상 수
  "processed": N,              // 현재까지 처리 수
  "started_at": "ISO 8601",
  "finished_at": "ISO 8601" | null,
  "error": null | "에러 메시지"
}

itemmaster:last_rebuild_at (ISO 8601 string)  // 마지막 rebuild 완료 시각
```

### 7-D-2: services/item_master_service.py — should_rebuild() 함수

```python
async def should_rebuild(session: AsyncSession) -> tuple[bool, str]:
    """
    Returns (True, "") if rebuild needed, (False, reason) if not.
    """
    redis = await get_redis()

    # 현재 실행 중인지 확인
    status_raw = await redis.get("itemmaster:rebuild_status")
    if status_raw:
        status_data = json.loads(status_raw)
        if status_data.get("status") == "running":
            return False, "Rebuild already running"

    # 마지막 BOM import 시각
    from sqlalchemy import func
    stmt = select(func.max(ImportBatch.finished_at)).where(
        ImportBatch.target_table == "bom",
        ImportBatch.status == "success"
    )
    res = await session.execute(stmt)
    latest_bom_import = res.scalar_one_or_none()

    if not latest_bom_import:
        return False, "No successful BOM import found"

    # 마지막 rebuild 시각
    last_rebuild_str = await redis.get("itemmaster:last_rebuild_at")
    if not last_rebuild_str:
        return True, ""  # 한 번도 rebuild 안 됨

    from datetime import timezone
    last_rebuild = datetime.fromisoformat(last_rebuild_str)
    if latest_bom_import.replace(tzinfo=timezone.utc) > last_rebuild.replace(tzinfo=timezone.utc):
        return True, ""
    return False, "ItemMaster already up-to-date"
```

### 7-D-3: services/item_master_service.py — rebuild_from_bom_background() 수정

기존 `rebuild_from_bom(session)` 은 **request-scoped session 사용으로 Background에서 실행 불가**.  
Background 전용 함수를 신규 작성한다:

```python
async def rebuild_from_bom_background(engine):
    """
    Background Task용. 자체 AsyncSession을 생성하여 실행.
    진행 상황을 Redis에 업데이트.
    """
    from sqlalchemy.ext.asyncio import AsyncSession
    from sqlalchemy.orm import sessionmaker

    redis = await get_redis()
    AsyncSessionLocal = sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)

    # 시작 상태 저장
    await redis.set("itemmaster:rebuild_status", json.dumps({
        "status": "running", "progress": 0, "total": 0,
        "processed": 0, "started_at": datetime.utcnow().isoformat(), "finished_at": None, "error": None
    }))

    try:
        async with AsyncSessionLocal() as session:
            # 전체 BOM item 수집 (기존 rebuild_from_bom 로직 동일)
            stmt = select(BomItem.part_number, BomItem.description, BomItem.vendor_raw, BomItem.level)\
                .distinct(BomItem.part_number).where(BomItem.part_number != None)
            res = await session.execute(stmt)
            bom_items_data = res.all()
            total = len(bom_items_data)

            # 기존 itemmaster 로드
            im_res = await session.execute(select(ItemMaster))
            existing_dict = {i.part_number: i for i in im_res.scalars().all()}
            active_pns = set()
            processed = 0

            for row in bom_items_data:
                pn = row.part_number
                if not pn: continue
                active_pns.add(pn)

                item = existing_dict.get(pn)
                if item:
                    item.description = row.description or item.description
                    item.vendor_raw = row.vendor_raw or item.vendor_raw
                    item.level = row.level or item.level
                    item.is_active = True
                else:
                    session.add(ItemMaster(
                        part_number=pn,
                        description=row.description or "",
                        vendor_raw=row.vendor_raw,
                        level=row.level or 1,
                        is_active=True
                    ))

                processed += 1

                # 50개마다 진행 상황 Redis 업데이트
                if processed % 50 == 0 or processed == total:
                    progress = int(processed / total * 100) if total > 0 else 100
                    await redis.set("itemmaster:rebuild_status", json.dumps({
                        "status": "running", "progress": progress, "total": total,
                        "processed": processed, "started_at": datetime.utcnow().isoformat(),
                        "finished_at": None, "error": None
                    }))

            # 비활성화
            for pn, item in existing_dict.items():
                if pn not in active_pns:
                    item.is_active = False

            await session.commit()

        # 완료 처리
        now = datetime.utcnow().isoformat()
        await redis.set("itemmaster:rebuild_status", json.dumps({
            "status": "done", "progress": 100, "total": total,
            "processed": processed, "started_at": now, "finished_at": now, "error": None
        }))
        await redis.set("itemmaster:last_rebuild_at", now)
        await _invalidate_item_cache()

    except Exception as e:
        await redis.set("itemmaster:rebuild_status", json.dumps({
            "status": "failed", "progress": 0, "total": 0, "processed": 0,
            "started_at": "", "finished_at": datetime.utcnow().isoformat(), "error": str(e)
        }))
```

### 7-D-4: api/routes/items.py — Rebuild 엔드포인트 추가

```python
from core.database import engine  # AsyncEngine을 직접 import

@router.post("/rebuild")
async def trigger_rebuild(
    background_tasks: BackgroundTasks,
    current_user: User = Depends(require_role("manager", "admin")),
    session: AsyncSession = Depends(get_session)
) -> dict:
    ok, reason = await item_master_service.should_rebuild(session)
    if not ok:
        return {"status": "skipped", "reason": reason}

    background_tasks.add_task(item_master_service.rebuild_from_bom_background, engine)
    return {"status": "started"}

@router.get("/rebuild/status")
async def get_rebuild_status() -> dict:
    redis = await get_redis()
    raw = await redis.get("itemmaster:rebuild_status")
    if not raw:
        return {"status": "idle", "progress": 0, "total": 0, "processed": 0,
                "started_at": None, "finished_at": None, "error": None}
    return json.loads(raw)
```

**참고**: `engine` import는 `core/database.py`에서 `AsyncEngine`이 module-level로 정의되어 있어야 함.  
현재 `core/database.py` 확인 후 `engine` 변수가 없으면 추가.

기존 `folder_import_service.py`에서 호출하는 `rebuild_from_bom(session)` (request-scoped)는 그대로 유지.  
단, 신규 `/import/folder/bom` 엔드포인트는 `should_rebuild()` + background 방식으로 전환 가능 (선택사항).

---

## Task 7-E: BOM 역조회 강화

### 7-E-1: schemas/item_master.py — ItemBomUsage 개선

```python
class ItemBomUsage(BaseModel):
    model_code: str
    model_description: Optional[str]  # BomModel에 description이 있으면 표시
    bom_qty: float          # 동일 모델 내 해당 part_number qty 합산
    paths: list[str]        # 이 모델에서 부품이 등장하는 모든 path
    levels: list[int]       # 각 path의 level
```

### 7-E-2: services/item_master_service.py — get_bom_usage() 개선

Polars로 model_code별 집계:

```python
import polars as pl

async def get_bom_usage(session, item_id):
    item = await get_item(session, item_id)
    if not item: return []

    stmt = (
        select(BomModel.model_code, BomItem.description, BomItem.qty, BomItem.level, BomItem.path)
        .join(BomItem, BomModel.id == BomItem.model_id)
        .where(BomItem.part_number == item.part_number)
    )
    res = await session.execute(stmt)
    rows = res.all()
    if not rows: return []

    df = pl.DataFrame(
        [(r.model_code, r.description, float(r.qty), r.level, r.path) for r in rows],
        schema=["model_code", "model_description", "qty", "level", "path"],
        orient="row"
    )
    grouped = (
        df.group_by("model_code")
        .agg([
            pl.col("qty").sum().alias("bom_qty"),
            pl.col("path").alias("paths"),
            pl.col("level").alias("levels"),
            pl.col("model_description").first().alias("model_description"),
        ])
        .sort("bom_qty", descending=True)
    )
    return [ItemBomUsage(**row) for row in grouped.to_dicts()]
```

---

## Task 7-F: 프론트엔드 수정

### 7-F-1: ItemMasterPage.tsx — vendor_name 표시

```tsx
// 변경 전
<TableCell>{item.vendor_raw}</TableCell>
// 변경 후
<TableCell>{item.vendor_name ?? item.vendor_raw}</TableCell>
```

`PartListPage.tsx`의 `vendor_raw` 표시도 동일하게 수정.

### 7-F-2: Rebuild Progress 표시 컴포넌트 (ItemMasterPage 상단 또는 ImportPage)

`RebuildProgress.tsx` (신규, src/components/items/ 또는 src/components/common/):

```
표시 방식:
  - status = "idle" 또는 "done": 
      ["ItemMaster 재구성"] 버튼 (manager 이상만)
  - status = "running":
      진행 중... [██████░░░░] 67% (processed / total)
      숫자: "1234 / 1850 품목 처리 중"
      버튼 비활성화
  - status = "failed":
      ❌ 오류 발생: {error}  재시도 버튼

폴링 로직:
  - useQuery with refetchInterval: 2000 (status="running" 동안)
  - status="done" 또는 "failed" → refetchInterval: false (폴링 중단)
```

API:
- `POST /api/v1/items/rebuild` → 재구성 트리거
- `GET /api/v1/items/rebuild/status` → 상태 폴링

### 7-F-3: BomUsageModal.tsx (신규, src/components/items/)

"사용처 조회" 버튼 클릭 시 모달:

```
헤더: "{part_number} 사용처 조회"

테이블:
  모델코드 | BOM 소요수량 | 사용 위치 (paths)
  FZ474PGV_AS2LLGA | 3.0 | 1.1.2, 1.3.1

빈 결과: "현재 BOM에서 사용되지 않습니다."
```

모달 구현: shadcn Dialog 없이 Tailwind CSS `fixed inset-0 bg-black/50 z-50` + 내부 `bg-white rounded-lg`:

```tsx
{modalOpen && (
  <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center"
       onClick={() => setModalOpen(false)}>
    <div className="bg-white rounded-lg shadow-xl p-6 max-w-2xl w-full mx-4"
         onClick={e => e.stopPropagation()}>
      {/* 헤더 */}
      {/* 테이블 */}
      {/* 닫기 버튼 */}
    </div>
  </div>
)}
```

**ItemMasterPage.tsx 수정:**

```tsx
const [selectedItem, setSelectedItem] = useState<any>(null);
const [modalOpen, setModalOpen] = useState(false);

// 테이블 행의 버튼
<Button variant="ghost" size="sm"
  onClick={() => { setSelectedItem(item); setModalOpen(true); }}>
  사용처 조회
</Button>

// 페이지 하단
<BomUsageModal
  item={selectedItem}
  open={modalOpen}
  onClose={() => { setSelectedItem(null); setModalOpen(false); }}
/>
```

---

## Task 7-G: 통합 검증

```bash
# 1. Python 문법 검증
cd /test/LARS/backend && source venv/bin/activate
python3 -m py_compile core/redis_client.py
python3 -m py_compile services/item_master_service.py
python3 -m py_compile schemas/item_master.py
python3 -m py_compile api/routes/items.py

# 2. 백엔드 재시작
pkill -f "uvicorn main:app" && sleep 2
nohup uvicorn main:app --host 0.0.0.0 --port 8000 > /tmp/lars_backend.log 2>&1 &
sleep 5 && grep -E "startup complete|ERROR" /tmp/lars_backend.log

# 3. API 검증
TOKEN=$(curl -s -X POST http://localhost:8000/api/v1/auth/login \
  -H "Content-Type: application/json" -d '{"email":"admin@lars.local","password":"admin1234"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

# vendor_name 파싱 확인 (EKHQ_ → 서브원 등)
curl -s "http://localhost:8000/api/v1/items" -H "Authorization: Bearer $TOKEN" \
  | python3 -c "
import sys,json
items = json.load(sys.stdin)
for i in items:
    if 'EKHQ' in (i.get('vendor_raw') or ''):
        print(i['vendor_raw'], '→', i.get('vendor_name'))
" | head -5

# Rebuild 상태 확인
curl -s "http://localhost:8000/api/v1/items/rebuild/status" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

# Rebuild 트리거
curl -s -X POST "http://localhost:8000/api/v1/items/rebuild" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

# 진행 중 폴링 (3초 간격으로 3회)
for i in 1 2 3; do
  curl -s "http://localhost:8000/api/v1/items/rebuild/status" \
    -H "Authorization: Bearer $TOKEN" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['status'], d['progress'], '%', d['processed'], '/', d['total'])"
  sleep 3
done

# BOM 역조회 (item_id=1로 테스트)
curl -s "http://localhost:8000/api/v1/items/1/bom-usage" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool | head -20

# 4. TypeScript 검증
cd /test/LARS/.WebUI && npx tsc --noEmit

# 5. 빌드
npm run build
```

---

## 구현 시 주의사항

1. **DB 컬럼 추가 없음** — `vendor_name`, `lower_vendor_name`은 computed 필드, Alembic 불필요
2. **Polars 전용** — Pandas 사용 금지
3. **Redis 장애 대비** — 모든 Redis 호출에 `try/except`, 실패 시 DB fallback
4. **Background session** — `rebuild_from_bom_background(engine)`은 반드시 자체 `AsyncSession` 생성, request-scoped session 사용 금지
5. **조건 확인 race condition** — `should_rebuild()` 에서 status="running" 체크를 원자적으로 처리 (Redis get → check → set이 순차적이므로 단일 프로세스에서는 안전)
6. **기존 rebuild_from_bom(session)** — 기존 import 파이프라인 호환성 유지를 위해 삭제하지 말고 유지. 새 background 함수와 별개로 공존
7. **engine import** — `core/database.py`에서 `engine` (AsyncEngine)을 module-level 변수로 export 확인 필요. 없으면 추가

---

## 완료 보고 형식

작업 완료 후 `LARS_Project/Phase7_Coder_Report.md`를 작성하여 제출한다.  
보고서에는 다음을 포함한다:
- vendor_name 파싱 샘플 결과 (EKHQ_ 형식 → 업체명 변환)
- Redis 캐시 동작 확인 (itemmaster:all 키 생성, 두 번째 요청 속도 향상)
- Rebuild 트리거 → 진행 상황 응답 확인
- BOM 역조회 샘플 결과
- TypeScript 오류 0건
- 빌드 성공
