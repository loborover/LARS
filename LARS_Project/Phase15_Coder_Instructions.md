# Phase 15 Coder Instructions — DP 구조 수정 + 시스템 상태 표시줄

**Role:** Coder (Gemini)  
**Date:** 2026-05-16  
**Priority:** 최고 — 사용자 반복 지적 사항, 반드시 전부 이행할 것

---

## 문제 정의 (감사 결과)

아래 항목들은 **반복 지적에도 미이행된** 사항이다. 변명 없이 전부 구현하라.

| # | 문제 | 원인 |
|---|------|------|
| 1 | DP 테이블에 Line 열 없음 | `/lots-raw` API가 ProductionLine 조인 누락 |
| 2 | DP Model 열이 Model만 표시, Suffix 누락 | `DailyPlanLot`에 suffix 미저장, BomModel 조인 실패 시 suffix = null |
| 3 | BOM 탭 Suffix 표시 누락 | 데이터 확인 필요 |
| 4 | PSI 백그라운드 인디케이터 미표시 | BackgroundMonitor 동작 여부 확인 |
| 5 | 시스템 상태 표시줄(AI/DB/시간) 없음 | 미구현 |

---

## Task 15-A: DailyPlanLot 모델에 suffix 컬럼 추가

### 15-A-1. `backend/models/daily_plan.py` 수정

`DailyPlanLot` 클래스에 `suffix` 필드 추가:

```python
class DailyPlanLot(SQLModel, table=True):
    __tablename__ = "daily_plan_lots"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    plan_id: int = Field(foreign_key="daily_plans.id", index=True)
    wo_number: Optional[str] = None
    model_id: Optional[int] = Field(default=None, foreign_key="bom_models.id")
    model_code: str = Field(index=True)
    suffix: Optional[str] = Field(default=None)          # ← 신규: DP 파일에서 직접 파싱된 suffix
    lot_number: str
    planned_qty: int = Field(default=0)
    input_qty: int = Field(default=0)
    output_qty: int = Field(default=0)
    planned_start: Optional[datetime] = None
    sort_order: int = Field(default=0)
    daily_qty_json: Optional[str] = Field(default=None)
    import_batch_id: Optional[int] = None
    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
```

### 15-A-2. Alembic 마이그레이션 실행

```bash
cd /test/LARS/backend
source venv/bin/activate
alembic revision --autogenerate -m "daily_plan_lot_add_suffix"
alembic upgrade head
```

생성된 마이그레이션 파일 확인: `daily_plan_lots` 테이블에 `suffix VARCHAR` 컬럼 추가.

### 15-A-3. `daily_plan_service.import_from_df` 수정

lot 생성 시 suffix를 직접 저장:

```python
lot = DailyPlanLot(
    plan_id=plan.id,
    wo_number=row.get("wo_number"),
    model_id=bom_model.id if bom_model else None,
    model_code=row["model_code"],
    suffix=row.get("suffix") or "",          # ← 신규: DP 파일의 suffix 직접 저장
    lot_number=row.get("lot_number", "N/A"),
    planned_qty=row["planned_qty"],
    input_qty=row.get("input_qty", 0),
    output_qty=row.get("output_qty", 0),
    planned_start=row.get("planned_start"),
    sort_order=row.get("sort_order", 0),
    daily_qty_json=row.get("daily_qty_json", "{}"),
    import_batch_id=batch_id
)
```

---

## Task 15-B: `/lots-raw` API 수정 — Line 컬럼 + suffix 직접 사용

**파일:** `backend/api/routes/dp.py` → `get_lots_raw` 함수 전면 수정

```python
@router.get("/lots-raw")
async def get_lots_raw(
    batch_id: int = Query(...),
    session: AsyncSession = Depends(get_session)
) -> list[dict]:
    import json
    from models.daily_plan import DailyPlan, ProductionLine

    # DailyPlanLot → DailyPlan → ProductionLine 조인으로 line_code 획득
    stmt = (
        select(DailyPlanLot, ProductionLine.code.label("line_code"))
        .join(DailyPlan, DailyPlanLot.plan_id == DailyPlan.id)
        .join(ProductionLine, DailyPlan.line_id == ProductionLine.id)
        .where(
            DailyPlanLot.import_batch_id == batch_id,
            DailyPlanLot.wo_number.is_not(None),
            DailyPlanLot.wo_number != ""
        )
        .order_by(ProductionLine.code, DailyPlanLot.sort_order)
    )
    res = await session.execute(stmt)
    rows = res.all()

    result = []
    for lot, line_code in rows:
        # suffix: DailyPlanLot에 직접 저장된 값 우선, 없으면 빈 문자열
        suffix = lot.suffix or ""
        model_number = f"{lot.model_code}.{suffix}" if suffix else lot.model_code
        remain_qty = (lot.planned_qty or 0) - (lot.output_qty or 0)

        daily_qty: dict = {}
        if lot.daily_qty_json:
            try:
                daily_qty = json.loads(lot.daily_qty_json)
            except Exception:
                pass

        result.append({
            "line_code": line_code or "",         # ← 신규
            "planned_start": lot.planned_start.isoformat() if lot.planned_start else None,
            "wo_number": lot.wo_number,
            "model_number": model_number,         # 이제 항상 Model.Suffix 형식
            "planned_qty": lot.planned_qty,
            "remain_qty": remain_qty,
            "daily_qty": daily_qty,
        })
    return result
```

---

## Task 15-C: 프론트엔드 DailyPlanPage.tsx 수정

### 15-C-1. `LotRaw` 인터페이스에 `line_code` 추가

```tsx
interface LotRaw {
  line_code: string;           // ← 신규
  planned_start: string | null;
  wo_number: string;
  model_number: string;        // 항상 Model.Suffix 형식
  planned_qty: number;
  remain_qty: number;
  daily_qty: Record<string, number>;
}
```

### 15-C-2. 테이블 헤더 — Line을 최우선 컬럼으로

```tsx
<thead className="sticky top-0 bg-gray-100 z-10 shadow-sm">
  <tr>
    {/* Line이 PST보다 앞에 와야 함 */}
    <FilterableHeader label="Line"        field="line_code"    {...filterProps} className="text-left whitespace-nowrap" />
    <th className="px-3 py-2.5 text-left border-b font-bold text-gray-600 whitespace-nowrap">PST (Start)</th>
    <FilterableHeader label="W/O (제번)"  field="wo_number"    {...filterProps} className="text-left" />
    <FilterableHeader label="Model.Suffix" field="model_number" {...filterProps} className="text-left" />
    <th className="px-3 py-2.5 text-right border-b font-bold text-gray-600 whitespace-nowrap">Lot Qty</th>
    <th className="px-3 py-2.5 text-right border-b font-bold text-gray-600 whitespace-nowrap">Remain</th>
    {dateColumns.map((d) => (
      <th key={d} className="px-3 py-2.5 text-right border-b font-bold text-gray-600 whitespace-nowrap">
        {d.slice(5)}
      </th>
    ))}
  </tr>
</thead>
```

### 15-C-3. 테이블 바디 — line_code 셀 추가

```tsx
<tr key={idx} className="hover:bg-blue-50/50 transition-colors group">
  {/* Line — 가장 첫 번째 열 */}
  <td className="px-3 py-2 font-mono text-xs font-semibold text-purple-700 bg-purple-50/30 whitespace-nowrap">
    {lot.line_code || '-'}
  </td>
  <td className="px-3 py-2 text-gray-400 whitespace-nowrap">
    {lot.planned_start ? lot.planned_start.slice(5, 16).replace('T', ' ') : '-'}
  </td>
  <td className="px-3 py-2 font-mono font-bold text-gray-700 whitespace-nowrap">{lot.wo_number}</td>
  <td className="px-3 py-2 text-blue-600 font-medium whitespace-nowrap">{lot.model_number}</td>
  ...
</tr>
```

### 15-C-4. Grand Total 행 colSpan 수정

Line 열이 추가됐으므로:
```tsx
<td colSpan={4} className="px-3 py-2.5 text-right ...">Grand Total</td>
```
(기존 colSpan={3} → 4로 변경)

---

## Task 15-D: BOM Suffix 표시 검증 및 수정

### 15-D-1. DB 데이터 확인 쿼리 (psql 또는 sqlalchemy)

```bash
cd /test/LARS/backend && source venv/bin/activate && python3 -c "
import asyncio
from core.database import get_session_context
from sqlmodel import select
from models.bom import BomModel

async def check():
    async with get_session_context() as s:
        res = await s.execute(select(BomModel).limit(10))
        for m in res.scalars():
            print(f'id={m.id} model_code={m.model_code!r} suffix={m.suffix!r} model_number={m.model_code}.{m.suffix}')
asyncio.run(check())
"
```

**판정 기준:**
- suffix가 전부 `""` 또는 `None` → BOM 재임포트 필요 또는 파서 수정 필요
- suffix가 올바른 값 → BOMListPage/BOMDetailPage 렌더링 로직 버그

### 15-D-2. BOMListPage 렌더링 확인

`BOMListPage.tsx`의 Variant 행 렌더링에서 suffix가 표시되는지 확인:

```tsx
// Variant 행: suffix가 빈 문자열이면 "(no suffix)", 있으면 .SUFFIX 형태로 표시
{model.suffix ? (
  <span className="font-bold text-blue-700">.{model.suffix}</span>
) : (
  <span className="text-gray-400 italic text-xs">(no suffix)</span>
)}
```

위 코드가 없거나 다르면 수정하라.

### 15-D-3. BOMDetailPage 모델 정보 카드 확인

```tsx
// model_code + suffix 분리 표시
<span className="text-xl font-bold text-gray-800">{data?.model?.model_code}</span>
{data?.model?.suffix && (
  <>
    <span className="text-gray-400">.</span>
    <span className="text-xl font-bold text-blue-600">{data?.model?.suffix}</span>
  </>
)}
```

위 코드가 없거나 다르면 수정하라.

---

## Task 15-E: 시스템 상태 표시줄 (SystemStatusBar)

### 15-E-1. 백엔드 헬스 엔드포인트

**파일 신규 생성:** `backend/api/routes/health.py`

```python
from fastapi import APIRouter
from datetime import datetime

router = APIRouter()

@router.get("/status")
async def system_status() -> dict:
    """DB, AI API, 시스템 시간 상태 반환"""
    db_ok = False
    ai_ok = False

    # DB 체크
    try:
        from core.database import get_session_context
        async with get_session_context() as session:
            await session.execute(__import__('sqlalchemy').text("SELECT 1"))
        db_ok = True
    except Exception:
        db_ok = False

    # AI API 키 유무 체크 (실제 API 호출 없이 키 존재 여부만)
    try:
        import os
        key = os.environ.get("OPENAI_API_KEY") or os.environ.get("ANTHROPIC_API_KEY") or ""
        ai_ok = len(key) > 10
    except Exception:
        ai_ok = False

    return {
        "db": "ok" if db_ok else "error",
        "ai": "ok" if ai_ok else "error",
        "server_time": datetime.now().isoformat(),
    }
```

`backend/api/router.py`에 등록:
```python
from api.routes import health
router.include_router(health.router, prefix="/health", tags=["health"])
```

### 15-E-2. 프론트엔드 SystemStatusBar 컴포넌트

**파일 신규 생성:** `.WebUI/src/components/SystemStatusBar.tsx`

```tsx
import { useEffect, useState } from 'react';
import { apiClient } from '../api/client';

interface StatusData {
  db: 'ok' | 'error';
  ai: 'ok' | 'error';
}

export function SystemStatusBar() {
  const [status, setStatus] = useState<StatusData | null>(null);
  const [now, setNow] = useState(new Date());

  // 시스템 시간 (매 초 갱신)
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  // 헬스 체크 (30초마다)
  useEffect(() => {
    const check = async () => {
      try {
        const res = await apiClient.get('/health/status');
        setStatus(res.data);
      } catch {
        setStatus({ db: 'error', ai: 'error' });
      }
    };
    check();
    const timer = setInterval(check, 30000);
    return () => clearInterval(timer);
  }, []);

  const dot = (ok: boolean) => (
    <span
      className={`inline-block w-1.5 h-1.5 rounded-full ${ok ? 'bg-green-400' : 'bg-red-500'}`}
    />
  );

  const timeStr = now.toLocaleTimeString('ko-KR', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false });

  return (
    <div className="px-3 py-2 border-t border-gray-800 bg-gray-950 flex items-center justify-between gap-2 shrink-0">
      {/* DB / AI 상태 */}
      <div className="flex items-center gap-2">
        <span className="flex items-center gap-1">
          {dot(status?.db === 'ok')}
          <span className="text-[9px] text-gray-500 font-mono">DB</span>
        </span>
        <span className="flex items-center gap-1">
          {dot(status?.ai === 'ok')}
          <span className="text-[9px] text-gray-500 font-mono">AI</span>
        </span>
      </div>
      {/* 시스템 시간 */}
      <span className="text-[9px] text-gray-500 font-mono tabular-nums">{timeStr}</span>
    </div>
  );
}
```

### 15-E-3. AppLayout.tsx에 SystemStatusBar 추가

`BackgroundMonitor` 바로 아래에 추가:

```tsx
import { SystemStatusBar } from '../SystemStatusBar';

// ... aside 내부 최하단
<BackgroundMonitor />
<SystemStatusBar />   {/* ← 이 줄 추가 */}
```

`SystemStatusBar`는 사이드바가 접혀도(`collapsed`) 항상 표시된다.  
단, 접힘 모드에서는 레이아웃이 좁으니 텍스트 레이블만 숨기고 dot만 표시:

```tsx
<div className={`px-3 py-2 border-t border-gray-800 bg-gray-950 flex items-center shrink-0 ${
  collapsed ? 'justify-center gap-1.5' : 'justify-between gap-2'
}`}>
  <div className="flex items-center gap-2">
    <span className="flex items-center gap-1" title={`DB: ${status?.db ?? '...'}`}>
      {dot(status?.db === 'ok')}
      {!collapsed && <span className="text-[9px] text-gray-500 font-mono">DB</span>}
    </span>
    <span className="flex items-center gap-1" title={`AI: ${status?.ai ?? '...'}`}>
      {dot(status?.ai === 'ok')}
      {!collapsed && <span className="text-[9px] text-gray-500 font-mono">AI</span>}
    </span>
  </div>
  {!collapsed && (
    <span className="text-[9px] text-gray-500 font-mono tabular-nums">{timeStr}</span>
  )}
</div>
```

`collapsed` prop을 받으려면 `SystemStatusBar`에 `collapsed?: boolean` prop을 추가하고 AppLayout에서 전달한다.

---

## Task 15-F: BackgroundMonitor 동작 검증

백그라운드 PSI 재계산 인디케이터가 보이지 않는 문제를 확인하라.

### 15-F-1. 엔드포인트 응답 확인

```bash
curl -s http://localhost:8000/api/v1/background/status | python3 -m json.tool
```

반환 JSON 구조에 `id`, `label`, `status`, `progress` 필드가 있어야 한다.

### 15-F-2. BackgroundMonitor 폴링 로직 확인

`BackgroundMonitor.tsx`에서 `/background/status`를 호출하는지 확인:
```tsx
const res = await apiClient.get('/background/status');
```

위 경로가 `/api/v1/background/status` 에 대응하는지 `apiClient` base URL 설정 확인.

### 15-F-3. 강제 idle 태스크 필터링 확인

```tsx
const visibleTasks = tasks.filter(
  (t) => t.status !== 'idle' && !hiddenTasks.has(t.id)
);
if (visibleTasks.length === 0) return null;
```

`idle` 상태가 기본값이면 아무것도 안 보이는 것이 정상.  
Redis에 아무 값도 없으면 background.py가 `status: idle`을 반환하므로 숨겨진다.  
Import를 실행할 때 running → done 흐름이 나타나야 한다.  
**실제 Import를 실행해서 BackgroundMonitor가 나타났다가 5초 후 사라지는지 육안 확인 후 보고서에 기재하라.**

---

## Task 15-G: 최종 빌드 및 서버 재시동

모든 수정 완료 후:

```bash
# 1. Alembic 마이그레이션 확인
cd /test/LARS/backend && source venv/bin/activate
alembic current
# 출력: (head) 가 최신 revision을 가리켜야 함

# 2. 프론트엔드 빌드
cd /test/LARS/.WebUI
npx tsc --noEmit 2>&1 | grep -c "error" || echo "TS errors: 0"
npm run build 2>&1 | tail -5

# 3. 백엔드 재시동
pkill -f "uvicorn main:app" || true
sleep 1
cd /test/LARS/backend && source venv/bin/activate
nohup uvicorn main:app --host 0.0.0.0 --port 8000 --workers 1 > /tmp/uvicorn.log 2>&1 &
sleep 3

# 4. /health/status 엔드포인트 확인
curl -s http://localhost:8000/api/v1/health/status | python3 -m json.tool

# 5. /lots-raw line_code 포함 확인
TOKEN=$(curl -s -X POST http://localhost:8000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@lars.com","password":"admin"}' | python3 -c "import json,sys; print(json.load(sys.stdin).get('access_token',''))")

curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8000/api/v1/dp/lots-raw?batch_id=1" | python3 -c "
import json,sys
data=json.load(sys.stdin)
if data: print('First row keys:', list(data[0].keys()))
print('line_code sample:', [r.get('line_code') for r in data[:3]])
print('model_number sample:', [r.get('model_number') for r in data[:3]])
"
# line_code와 model_number(Model.Suffix 형식)가 포함되어야 함

# 6. vite preview 재시동
pkill -f "vite preview" || true
sleep 1
cd /test/LARS/.WebUI
nohup npx vite preview --port 3000 --host 0.0.0.0 > /tmp/vite.log 2>&1 &
```

---

## 검증 체크리스트

### DP 테이블
- [ ] "Line" 열이 PST 열보다 왼쪽 첫 번째에 위치
- [ ] Line 열에 실제 라인코드 값이 표시됨 (예: "A", "B", "Line1" 등)
- [ ] "Model.Suffix" 열에 Suffix가 포함된 값 표시 (예: `LSGL6335X.ASTLLGA`)
- [ ] BOM 연결이 없는 경우에도 DP 파일에서 파싱된 suffix 사용
- [ ] "W/O (제번)" 컬럼명 명확히 표시
- [ ] Grand Total colSpan 수정으로 레이아웃 깨짐 없음

### BOM 탭
- [ ] BOMListPage에서 suffix가 있는 variant는 `.SUFFIX` 형식으로 표시
- [ ] BOMDetailPage 상단 카드에 model_code + `.suffix` 하이라이트 표시

### 시스템 상태 표시줄
- [ ] 사이드바 최하단에 SystemStatusBar 항상 표시
- [ ] DB 상태 dot (초록/빨강)
- [ ] AI API 상태 dot (초록/빨강)
- [ ] 시스템 시간 1초마다 갱신
- [ ] 사이드바 접힘 모드에서 dot만 표시 (텍스트 숨김)

### BackgroundMonitor
- [ ] `/background/status` 응답 구조 정상 확인
- [ ] Import 실행 시 monitor 나타남 → 완료 후 5초 뒤 숨겨짐 육안 확인

---

## 완료 후

- `Phase15_Coder_Report.md` 작성 (curl 응답 결과 포함)
- `npm run build` TypeScript 에러 0건
- Git commit: `"Phase 15: DP Line+Suffix fix, SystemStatusBar"`
