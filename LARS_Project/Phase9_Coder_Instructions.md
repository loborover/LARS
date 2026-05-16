# Phase 9 Coder Instructions

**Role:** Coder (Gemini)  
**Date:** 2026-05-16  
**Priority:** High  
**Dependencies:** Phase 7 (ItemMaster + Redis), Phase 8 (DP Viewer) 완료 전제

---

## 개요

세 가지 작업을 처리한다:

1. **BOM 모델번호 = Model + Suffix** — DB 스키마 변경 + 전파
2. **사이드바 하단 Background Process Monitor UI**
3. **전 페이지 머릿말(헤더) sticky 고정 + 테이블 헤더 sticky**

---

## Task 9-A: BOM Model + Suffix 통합

### 배경

파서(`bom_parser.py`)는 이미 파일명에서 `model_code`와 `suffix`를 분리 추출한다:  
`LSGL6335X.ARSELGA@CVZ.EKHQ 1.0.xlsx` → `model_code='LSGL6335X'`, `suffix='ARSELGA'`

그러나 현재 `bom_models` 테이블은 `model_code`만 unique로 저장해 suffix가 다른 같은 모델 두 개를 구분 못한다. **모델번호 = Model + Suffix** 이므로 `(model_code, suffix)` 복합키로 변경해야 한다.

### 9-A-1. DB 모델 수정 (`backend/models/bom.py`)

```python
class BomModel(SQLModel, table=True):
    __tablename__ = "bom_models"

    id: Optional[int] = Field(default=None, primary_key=True)
    model_code: str = Field(index=True)          # unique 제거
    suffix: str = Field(default="", index=True)   # 추가
    description: Optional[str] = None
    version: str = Field(default="1.0")
    is_active: bool = Field(default=True)
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)

    __table_args__ = (
        UniqueConstraint("model_code", "suffix", name="uq_bom_models_code_suffix"),
    )
```

`from sqlmodel import UniqueConstraint` 또는 `from sqlalchemy import UniqueConstraint` import 추가.

### 9-A-2. Alembic 마이그레이션 생성

```bash
cd /test/LARS/backend
source venv/bin/activate
alembic revision --autogenerate -m "bom_model_add_suffix"
alembic upgrade head
```

자동 생성된 migration 파일을 확인하고 아래 내용이 포함되었는지 검증:
- `bom_models` 테이블에 `suffix VARCHAR` 컬럼 추가 (nullable, default `''`)
- 기존 `unique constraint on model_code` 삭제
- 새 `unique constraint on (model_code, suffix)` 추가

### 9-A-3. BOM 스키마 수정 (`backend/schemas/bom.py`)

`BomModelRead`에 `suffix`와 계산 프로퍼티 `model_number` 추가:

```python
from pydantic import BaseModel, computed_field

class BomModelRead(BaseModel):
    id: int
    model_code: str
    suffix: str = ""
    description: Optional[str] = None
    version: str

    @computed_field
    @property
    def model_number(self) -> str:
        return f"{self.model_code}.{self.suffix}" if self.suffix else self.model_code
```

### 9-A-4. BOM 서비스 수정 (`backend/services/bom_service.py`)

**`import_from_df()` 수정:**
```python
async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    from sqlalchemy import delete
    # suffix 컬럼이 없으면 빈 문자열로 채움
    if "suffix" not in df.columns:
        df = df.with_columns(pl.lit("").alias("suffix"))

    model_keys = df.select(["model_code", "suffix"]).unique().to_dicts()
    total_upserted = 0

    for key in model_keys:
        mc = key["model_code"]
        sf = key["suffix"] or ""

        stmt = select(BomModel).where(BomModel.model_code == mc, BomModel.suffix == sf)
        res = await session.execute(stmt)
        bom_model = res.scalar_one_or_none()

        if not bom_model:
            bom_model = BomModel(model_code=mc, suffix=sf, import_batch_id=batch_id)
            session.add(bom_model)
            await session.flush()
            await session.refresh(bom_model)
        else:
            bom_model.import_batch_id = batch_id
            await session.flush()

        model_df = df.filter((pl.col("model_code") == mc) & (pl.col("suffix") == sf))
        # 이하 기존 upsert 로직 동일 (sort_order 기반 update/insert/delete)
        ...
```

**`get_bom_tree()` 수정:**  
`model_code` 파라미터를 `model_number`로 변경. `model_number`가 `.`을 포함하면 split하고, 없으면 suffix=""로 처리:

```python
async def get_bom_tree(session: AsyncSession, model_number: str) -> Optional[BomTreeResponse]:
    if "." in model_number:
        model_code, suffix = model_number.split(".", 1)
    else:
        model_code, suffix = model_number, ""
    
    stmt = select(BomModel).where(BomModel.model_code == model_code, BomModel.suffix == suffix)
    ...
```

**`list_models()` 수정:**  
search가 `model_number` 기반으로도 동작하도록:
```python
async def list_models(session: AsyncSession, search: Optional[str] = None, is_active: bool = True) -> List[BomModelRead]:
    stmt = select(BomModel).where(BomModel.is_active == is_active)
    if search:
        from sqlalchemy import or_
        stmt = stmt.where(or_(
            BomModel.model_code.contains(search),
            BomModel.suffix.contains(search)
        ))
    result = await session.execute(stmt)
    models = result.scalars().all()
    return [BomModelRead(id=m.id, model_code=m.model_code, suffix=m.suffix,
                         description=m.description, version=m.version) for m in models]
```

**`bom_reverse_lookup()` 수정:**  
반환 시 `suffix` 포함:
```python
model_reads = [BomModelRead(id=m.id, model_code=m.model_code, suffix=m.suffix,
                             description=m.description, version=m.version) for m in models]
```

### 9-A-5. BOM API 라우트 수정 (`backend/api/routes/bom.py`)

`/{model_code}` 경로를 `/{model_number}`로 변경 (path에 `.`이 포함되므로 FastAPI path 파라미터 처리 주의):

```python
@router.get("/models/{model_number:path}", response_model=BomTreeResponse)
async def get_model_tree(
    model_number: str,
    session: AsyncSession = Depends(get_session)
):
    result = await bom_service.get_bom_tree(session, model_number)
    if not result:
        raise HTTPException(status_code=404, detail="Model not found")
    return result
```

### 9-A-6. ItemMaster 서비스 수정 (`backend/services/item_master_service.py`)

`get_bom_usage()` — `model_code` 대신 `model_number` 반환:

```python
# df 생성 시 model_number 컬럼 추가
df = pl.DataFrame(
    [(f"{r.model_code}.{r.suffix}" if r.suffix else r.model_code,
      r.description, float(r.qty), r.level, r.path)
     for r in rows],
    schema=["model_number", "model_description", "qty", "level", "path"],
    orient="row"
)
grouped = (
    df.group_by("model_number")
    .agg([
        pl.col("qty").sum().alias("bom_qty"),
        pl.col("path").alias("paths"),
        pl.col("level").alias("levels"),
        pl.col("model_description").first().alias("model_description"),
    ])
    .sort("bom_qty", descending=True)
)
```

`get_bom_usage()`의 SQL query에 `BomModel.suffix` 컬럼도 select에 추가:
```python
stmt = (
    select(BomModel.model_code, BomModel.suffix, BomItem.description, BomItem.qty, BomItem.level, BomItem.path)
    .join(BomItem, BomModel.id == BomItem.model_id)
    .where(BomItem.part_number == item.part_number)
)
```

`ItemBomUsage` 스키마(`schemas/item_master.py`)에서 `model_code` → `model_number`로 필드명 변경.

### 9-A-7. Daily Plan 서비스 수정 (`backend/services/daily_plan_service.py`)

DP row에 `suffix` 컬럼이 있을 경우 BOM 조회에 suffix 포함:

```python
# Resolve model_id if BOM exists
sf = row.get("suffix") or ""
if sf:
    stmt = select(BomModel).where(BomModel.model_code == row["model_code"], BomModel.suffix == sf)
else:
    stmt = select(BomModel).where(BomModel.model_code == row["model_code"])
res = await session.execute(stmt)
bom_model = res.scalar_one_or_none()
```

### 9-A-8. 프론트엔드 수정 (`.WebUI/src/`)

BOM 관련 페이지(`BomPage.tsx` 또는 동등 파일)에서:
- 모델 목록 표시 시 `p.model_number` 사용 (API가 반환하는 computed field)
- BOM 트리 조회 시 API URL: `/bom/models/${encodeURIComponent(modelNumber)}` 사용
- ItemMaster의 BOM 사용처 모달에서 `model_number` 표시

---

## Task 9-B: Background Process Monitor UI

### 배경

`GET /api/v1/items/rebuild/status` 엔드포인트가 이미 존재하며 다음을 반환한다:
```json
{
  "status": "running" | "done" | "failed" | "idle",
  "progress": 0~100,
  "total": 1000,
  "processed": 500,
  "started_at": "ISO string",
  "finished_at": "ISO string | null",
  "error": "string | null"
}
```

이 데이터를 사이드바 하단에서 표시하는 컴포넌트를 만든다.

### 9-B-1. BackgroundMonitor 컴포넌트 생성

파일: `.WebUI/src/components/BackgroundMonitor.tsx`

**동작 규칙:**
- 기본 상태: hidden (idle 상태)
- 폴링: 2초 간격 (`setInterval`). `done`/`failed` 확인 즉시 5초 후 자동 숨김
- `running`: 파란색 progress bar + `"{processed} / {total} ({progress}%)"` 텍스트
- `done`: 초록색 체크 + `"Complete"` + 5초 뒤 자동 숨김
- `failed`: 빨간색 X + `"Failed: {error}"` + 5초 뒤 자동 숨김

**구현 스펙:**

```tsx
import { useEffect, useState, useRef } from 'react';
import { apiClient } from '../api/client';

interface RebuildStatus {
  status: 'idle' | 'running' | 'done' | 'failed';
  progress: number;
  total: number;
  processed: number;
  error: string | null;
}

export function BackgroundMonitor() {
  const [status, setStatus] = useState<RebuildStatus | null>(null);
  const [visible, setVisible] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const hideTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const poll = async () => {
    try {
      const res = await apiClient.get('/items/rebuild/status');
      const data: RebuildStatus = res.data;
      
      if (data.status === 'idle') {
        setVisible(false);
        return;
      }
      
      setStatus(data);
      setVisible(true);

      if (data.status === 'done' || data.status === 'failed') {
        // 완료/실패 시 5초 후 숨김
        if (hideTimerRef.current) clearTimeout(hideTimerRef.current);
        hideTimerRef.current = setTimeout(() => setVisible(false), 5000);
        if (intervalRef.current) {
          clearInterval(intervalRef.current);
          intervalRef.current = null;
        }
      }
    } catch { /* ignore */ }
  };

  useEffect(() => {
    poll();
    intervalRef.current = setInterval(poll, 2000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
      if (hideTimerRef.current) clearTimeout(hideTimerRef.current);
    };
  }, []);

  if (!visible || !status) return null;

  return (
    <div className="mx-2 mb-2 p-3 bg-gray-800 rounded-lg text-xs text-white">
      <div className="font-semibold mb-1 text-gray-300">Item Rebuild</div>
      
      {status.status === 'running' && (
        <>
          <div className="w-full bg-gray-600 rounded-full h-1.5 mb-1">
            <div
              className="bg-blue-400 h-1.5 rounded-full transition-all duration-300"
              style={{ width: `${status.progress}%` }}
            />
          </div>
          <div className="text-gray-400">
            {status.processed} / {status.total} ({status.progress}%)
          </div>
        </>
      )}
      
      {status.status === 'done' && (
        <div className="flex items-center space-x-1 text-green-400">
          <span>✓</span><span>Complete</span>
        </div>
      )}
      
      {status.status === 'failed' && (
        <div className="text-red-400">
          ✕ Failed: {status.error ?? 'unknown error'}
        </div>
      )}
    </div>
  );
}
```

### 9-B-2. AppLayout 수정 (`.WebUI/src/components/layout/AppLayout.tsx`)

`BackgroundMonitor` import 후 사이드바 `<aside>` 내 `<nav>` 아래에 추가:

```tsx
import { BackgroundMonitor } from '../BackgroundMonitor';

// <aside> 구조 변경:
<aside className="hidden md:flex flex-col w-64 bg-gray-900 text-white">
  <div className="p-4 text-xl font-bold border-b border-gray-800">LARS Platform</div>
  <nav className="flex-1 p-4 space-y-2 overflow-y-auto">
    {/* 기존 nav 내용 유지 */}
  </nav>
  <BackgroundMonitor />   {/* 사이드바 최하단 */}
</aside>
```

---

---

## Task 9-C: 전 페이지 머릿말 Sticky 고정

### 배경

`AppLayout.tsx`의 `<main>` 요소가 `overflow-auto`를 가지므로, 페이지 내부에서 sticky를 쓰면 `<main>` 기준으로 고정된다. 현재 모든 페이지에서 스크롤 시 제목·필터가 사라지는 문제가 있다.

### 적용 대상 파일

`src/pages/` 아래의 모든 데이터 페이지:
- `BOMListPage.tsx`
- `BOMDetailPage.tsx`
- `DailyPlanPage.tsx`
- `ItemMasterPage.tsx`
- `PSIPage.tsx`
- `PartListPage.tsx`

### 공통 패턴

각 페이지를 아래 구조로 변환한다:

```tsx
// 변경 전 (현재)
<div className="space-y-6">
  <div className="flex justify-between items-center">
    <h1 className="text-2xl font-bold">페이지 제목</h1>
  </div>
  {/* 필터 영역 */}
  <div>...</div>
  {/* 테이블 */}
  <div className="bg-white rounded shadow overflow-x-auto">
    <Table>
      <TableHeader>...</TableHeader>
      <TableBody>...</TableBody>
    </Table>
  </div>
</div>

// 변경 후
<div className="flex flex-col h-full">
  {/* Sticky 머릿말 */}
  <div className="sticky top-0 z-20 bg-gray-50 pb-3 space-y-3">
    <div className="flex justify-between items-center">
      <h1 className="text-2xl font-bold">페이지 제목</h1>
      {/* 우상단 버튼 유지 */}
    </div>
    {/* 필터 영역 (있으면 여기 포함) */}
  </div>

  {/* 스크롤 콘텐츠 */}
  <div className="flex-1 overflow-auto mt-2">
    <div className="bg-white rounded shadow">
      <Table>
        <TableHeader className="sticky top-0 z-10 bg-white">
          <TableRow>...</TableRow>
        </TableHeader>
        <TableBody>...</TableBody>
      </Table>
    </div>
  </div>
</div>
```

### 세부 지침

1. **머릿말 배경색**: 해당 페이지의 실제 배경색과 일치시킬 것. 대부분 `bg-gray-50`. PSIPage는 `bg-gray-50/30`.
2. **테이블이 없는 페이지** (AIChatPage, DashboardPage, LoginPage 등): 변경하지 않음.
3. **PSIPage**: 이미 `h-full flex flex-col`이 있고 구조가 복잡하므로, 상단 `div.shrink-0` 섹션에 `sticky top-0 z-20 bg-gray-50/30`만 추가하는 방식으로 최소 변경.
4. **DailyPlanPage**: 탭(Viewer/Print) 구조이므로, 탭 선택 영역까지 sticky에 포함시키고, 각 탭 내부 테이블만 스크롤.
5. **2단 그리드 레이아웃** (DailyPlanPage의 계획목록+로트상세): 각 패널의 `<TableHeader>`에 `sticky top-0 bg-white z-10` 추가.

### 주의사항

- `overflow-auto`를 내부 div에 넣으면 바깥 `<main>`의 스크롤과 충돌 가능. 페이지 전체가 `<main>` 안에서 scroll되는 것이 기본이므로, 내부에 추가 `overflow-auto`를 넣을 때는 명시적 `max-h`를 함께 설정할 것.  
  권장: `max-h-[calc(100vh-200px)] overflow-auto`
- `z-index` 레이어: 머릿말 `z-20` > 테이블 헤더 `z-10`. 머릿말이 테이블 헤더를 덮어야 한다.

---

## 검증 체크리스트

### 9-A (BOM Suffix)
- [ ] `alembic upgrade head` 오류 없음
- [ ] BOM 파일 re-import 후 `bom_models` 테이블에 `suffix` 컬럼 값 확인
- [ ] `GET /api/v1/bom/models` 응답에 `suffix`, `model_number` 포함
- [ ] `GET /api/v1/bom/models/LSGL6335X.ARSELGA` 정상 응답
- [ ] ItemMaster BOM 사용처 모달에서 `model_number` 표시
- [ ] DP import 시 suffix 있는 lot의 `model_id` 정상 매핑

### 9-B (Monitor UI)
- [ ] ItemMaster Rebuild 트리거 후 사이드바 하단 progress bar 표시
- [ ] 완료 시 "Complete" 표시 → 5초 후 자동 숨김
- [ ] idle 상태에서 모니터 컴포넌트 미표시 확인
- [ ] `npm run build` TypeScript 오류 0건

### 9-C (Sticky Header)
- [ ] BOMListPage — 제목+검색바 sticky 확인
- [ ] DailyPlanPage — 제목+탭 sticky, 내부 테이블 헤더 sticky
- [ ] ItemMasterPage — 제목+필터 sticky
- [ ] PSIPage — 제목+필터 sticky
- [ ] PartListPage — 제목+필터 sticky
- [ ] 스크롤 시 머릿말이 최상단에 고정되는지 브라우저에서 확인

### 9-D (Tutorial 토글)
- [ ] 각 페이지 튜토리얼 안내 문구가 토글 가능
- [ ] 사용자 설정이 localStorage에 저장되어 새로고침 후에도 유지
- [ ] 숨김 상태에서 재열람 가능한 버튼 표시 확인

---

---

## Task 9-D: 페이지별 튜토리얼 안내 문구 숨김/열림 토글

### 배경

각 페이지 상단에 기능 설명 안내 문구가 있거나 추가 예정이다. 사용자마다 익숙도가 다르므로 개인 기호에 따라 숨기거나 열 수 있어야 한다. 설정은 새로고침 후에도 유지되어야 한다 (`localStorage` 사용).

### 9-D-1. useTutorial 훅 생성

파일: `.WebUI/src/hooks/useTutorial.ts`

```ts
import { useState, useEffect } from 'react';

export function useTutorial(pageKey: string) {
  const storageKey = `tutorial_hidden_${pageKey}`;
  const [hidden, setHidden] = useState<boolean>(() => {
    return localStorage.getItem(storageKey) === 'true';
  });

  const toggle = () => {
    const next = !hidden;
    setHidden(next);
    localStorage.setItem(storageKey, String(next));
  };

  return { hidden, toggle };
}
```

### 9-D-2. TutorialBox 컴포넌트 생성

파일: `.WebUI/src/components/TutorialBox.tsx`

```tsx
import { useTutorial } from '../hooks/useTutorial';
import { HelpCircle, X, ChevronDown } from 'lucide-react';

interface Props {
  pageKey: string;
  children: React.ReactNode;
}

export function TutorialBox({ pageKey, children }: Props) {
  const { hidden, toggle } = useTutorial(pageKey);

  if (hidden) {
    return (
      <button
        onClick={toggle}
        className="flex items-center gap-1 text-xs text-gray-400 hover:text-blue-500 mb-2"
      >
        <HelpCircle size={14} />
        <span>도움말 보기</span>
        <ChevronDown size={12} />
      </button>
    );
  }

  return (
    <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 mb-3 text-sm text-blue-800 relative">
      <button
        onClick={toggle}
        className="absolute top-2 right-2 text-blue-400 hover:text-blue-700"
        title="닫기"
      >
        <X size={14} />
      </button>
      {children}
    </div>
  );
}
```

### 9-D-3. 각 페이지에 TutorialBox 적용

대상 페이지 6개에 `TutorialBox`를 import하고 sticky 머릿말 안 (필터 위)에 삽입.  
튜토리얼 내용은 아래 기준으로 작성:

| 페이지 | pageKey | 튜토리얼 내용 |
|--------|---------|-------------|
| BOMListPage | `bom-list` | "BOM 파일을 Import하면 모델 목록이 표시됩니다. 모델을 클릭하면 자재 트리를 볼 수 있습니다." |
| BOMDetailPage | `bom-detail` | "자재 트리는 레벨별로 표시됩니다. 부품번호를 클릭하면 ItemMaster에서 상세 정보를 확인할 수 있습니다." |
| DailyPlanPage | `daily-plan` | "날짜와 라인을 선택하면 일일 생산계획을 조회합니다. [인쇄 뷰어] 탭에서 프린트 최적화 화면을 사용하세요." |
| ItemMasterPage | `item-master` | "BOM Import 후 자동으로 품목이 등록됩니다. '사용처 조회'로 어떤 모델에 사용되는지 확인하세요." |
| PSIPage | `psi` | "수급현황 매트릭스입니다. 셀을 클릭하면 인라인 편집이 가능합니다. 날짜 범위와 필터를 설정하세요." |
| PartListPage | `part-list` | "DP Import 후 BOM과 매칭되어 소요자재가 자동 계산됩니다. 모델과 날짜로 필터링하세요." |

---

## 완료 후 처리

1. Alembic 마이그레이션 파일 커밋 포함
2. `npm run build` 후 결과 확인
3. `Phase9_Coder_Report.md` 작성 (완료/스킵 항목, 이슈 기록)
4. Git commit: `"Phase 9: BOM model+suffix unification, background monitor UI"`
