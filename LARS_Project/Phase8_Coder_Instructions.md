# Phase 8 Coder Instructions — Daily Plan 뷰어 재설계 (웹 뷰어 + 인쇄 뷰어)

> 작성일: 2026-05-16
> 작성자: Chief
> 대상: Coder (Gemini)
> 기준 문서: `LARS_Project/New_LARS_Project.md`

---

## 배경 및 목적

현재 Daily Plan(`/dp`) 페이지는 "계획 목록 클릭 → 로트 상세" 2-panel 구조로,
실제 생산 현장에서 쓸 수 없는 UI이다. 또한 `list_plans()` API의 `pl.col()` 버그가
제거되어 이제 211건의 plan이 정상 반환된다(Chief가 직접 수정 완료).

이 Phase에서는 Daily Plan 페이지를 **날짜 기준 뷰** 중심으로 전면 재설계한다.

---

## 데이터 구조 파악 (Coder 필독)

### DB 현황
| 테이블 | 건수 | 비고 |
|---|---|---|
| `daily_plans` | 211건 | (plan_date, line_id) 고유 |
| `daily_plan_lots` | 1,439건 | plan_id → daily_plan 참조 |
| `production_lines` | 5개 | C11, C5, C7, GP1, DUMMY |

### daily_qty_json 형식
```json
{"2026-05-14": 9.0, "2026-05-16": 2.0}
```
하나의 W/O(lot)는 여러 날짜에 걸쳐 수량이 나뉠 수 있다.  
날짜별 뷰에서는 해당 날짜의 수량만 추출하여 표시한다.

### 데이터 범위
- 날짜: 2026-04-21 ~ 2026-08-31 (77개 날짜)
- 라인: C11, C5, C7, GP1, DUMMY

---

## Task 8-A: 백엔드 — 날짜 기준 조회 API 추가

### 8-A-1: services/daily_plan_service.py에 get_daily_view() 추가

```
GET /api/v1/dp/daily?date=2026-05-16&line_code=C11(선택)

알고리즘:
1. date 파라미터로 daily_plans 조회 → plan_id 목록
2. 해당 plan_id들의 daily_plan_lots 전체 로드
3. ProductionLine JOIN → line_code 첨부
4. 각 lot의 daily_qty_json 파싱:
     import json
     qty = json.loads(lot.daily_qty_json or '{}').get(str(target_date), 0.0)
5. daily_qty == 0인 lot 제외
6. line_code별로 그룹화하여 반환
7. 각 그룹 내 lot은 sort_order 기준 정렬
```

**반환 스키마:**

```python
class DailyLotView(BaseModel):
    wo_number: Optional[str]
    model_code: str
    lot_number: str
    daily_qty: float      # 해당 날짜의 수량
    planned_qty: int      # 전체 계획수량
    output_qty: int       # 실적
    sort_order: int

class DailyLineView(BaseModel):
    line_code: str
    line_name: str
    lots: List[DailyLotView]
    total_daily_qty: float    # 해당 라인 당일 수량 합계

class DailyPlanViewResponse(BaseModel):
    date: str
    lines: List[DailyLineView]
    total_qty: float          # 전체 라인 합계
```

**schemas/daily_plan.py에 위 스키마 추가.**

### 8-A-2: api/routes/dp.py에 엔드포인트 추가

```python
@router.get("/daily", response_model=DailyPlanViewResponse)
async def get_daily_view(
    date: date = Query(..., description="조회 날짜 (YYYY-MM-DD)"),
    line_code: Optional[str] = Query(None),
    session: AsyncSession = Depends(get_session)
):
    return await daily_plan_service.get_daily_view(session, date, line_code)
```

**주의**: 이 엔드포인트는 `GET /{plan_id}/lots` 보다 먼저 라우터에 등록되어야 한다.
FastAPI는 경로 순서대로 매칭하므로 `/daily`가 `/{plan_id}`에 흡수되지 않도록 순서 확인.

### 8-A-3: 날짜 목록 API 추가 (달력/드롭다운용)

```python
@router.get("/dates", response_model=List[str])
async def get_available_dates(session: AsyncSession = Depends(get_session)):
    """DP 데이터가 존재하는 날짜 목록 반환 (ISO 8601 형식)"""
    from sqlalchemy import func
    stmt = select(func.distinct(DailyPlan.plan_date)).order_by(DailyPlan.plan_date)
    res = await session.execute(stmt)
    dates = res.scalars().all()
    return [d.date().isoformat() if hasattr(d, 'date') else str(d)[:10] for d in dates]
```

---

## Task 8-B: 프론트엔드 — DailyPlanPage 전면 재설계

### 8-B-1: 페이지 구조

```
DailyPlanPage (/dp)
├── 상단 필터 바
│   ├── 날짜 선택 (드롭다운 또는 date input)  ← /dp/dates에서 로드
│   └── 라인 필터 (전체 / C11 / C5 / C7 / GP1)
└── 탭 네비게이션
    ├── [웹 뷰어]  ← 기본 탭
    └── [인쇄 뷰어]
```

두 탭 모두 동일한 데이터(`/dp/daily?date=...&line_code=...`)를 사용.  
탭 전환은 URL hash나 state로 관리 (React Router sub-route 불필요, useState로 충분).

### 8-B-2: 탭 컴포넌트 구조

```
src/pages/DailyPlanPage.tsx     ← 메인 (필터 + 탭 상태 관리)
src/components/dp/
  DailyPlanViewer.tsx           ← 웹 뷰어 탭
  DailyPlanPrint.tsx            ← 인쇄 뷰어 탭
```

### 8-B-3: DailyPlanPage.tsx (메인)

```tsx
export default function DailyPlanPage() {
  const today = new Date().toISOString().split('T')[0];
  const [selectedDate, setSelectedDate] = useState(today);
  const [selectedLine, setSelectedLine] = useState('');  // '' = 전체
  const [activeTab, setActiveTab] = useState<'viewer' | 'print'>('viewer');

  // 날짜 목록 (드롭다운)
  const { data: availableDates } = useQuery({
    queryKey: ['dp-dates'],
    queryFn: () => apiClient.get('/dp/dates').then(r => r.data),
  });

  // 일일 생산계획 데이터
  const { data: dailyPlan, isLoading } = useQuery({
    queryKey: ['dp-daily', selectedDate, selectedLine],
    queryFn: () => apiClient.get('/dp/daily', {
      params: { date: selectedDate, ...(selectedLine && { line_code: selectedLine }) }
    }).then(r => r.data),
    enabled: !!selectedDate,
  });

  const lines = ['', 'C11', 'C5', 'C7', 'GP1'];

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">Daily Plan (일일생산계획)</h1>

      {/* 필터 바 */}
      <div className="flex gap-4 items-center bg-white p-4 rounded shadow">
        <div>
          <label className="block text-xs text-gray-500 mb-1">날짜</label>
          <select value={selectedDate} onChange={e => setSelectedDate(e.target.value)}
                  className="border rounded px-3 py-1.5 text-sm">
            {availableDates?.map((d: string) => (
              <option key={d} value={d}>{d}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-xs text-gray-500 mb-1">라인</label>
          <select value={selectedLine} onChange={e => setSelectedLine(e.target.value)}
                  className="border rounded px-3 py-1.5 text-sm">
            <option value="">전체</option>
            {lines.filter(l => l).map(l => <option key={l} value={l}>{l}</option>)}
          </select>
        </div>
        {/* 탭 버튼 */}
        <div className="ml-auto flex border rounded overflow-hidden">
          <button onClick={() => setActiveTab('viewer')}
                  className={`px-4 py-1.5 text-sm ${activeTab === 'viewer' ? 'bg-blue-600 text-white' : 'bg-white text-gray-600'}`}>
            웹 뷰어
          </button>
          <button onClick={() => setActiveTab('print')}
                  className={`px-4 py-1.5 text-sm ${activeTab === 'print' ? 'bg-blue-600 text-white' : 'bg-white text-gray-600'}`}>
            인쇄 뷰어
          </button>
        </div>
      </div>

      {/* 탭 컨텐츠 */}
      {isLoading ? (
        <div className="p-8 text-center text-gray-500">Loading...</div>
      ) : !dailyPlan ? null : activeTab === 'viewer' ? (
        <DailyPlanViewer data={dailyPlan} />
      ) : (
        <DailyPlanPrint data={dailyPlan} />
      )}
    </div>
  );
}
```

### 8-B-4: DailyPlanViewer.tsx (웹 뷰어)

라인별 섹션으로 나뉜 테이블. 라인 헤더 + 로트 목록 + 소계.

```
[C11 라인]  ──────────────────────────────────────── 당일 계획: 687
  순번 │ W/O           │ 모델코드            │ 당일수량 │ 총계획 │ 실적
  ─────┼───────────────┼─────────────────────┼──────────┼────────┼──────
    1  │ FO2623ST-...  │ LSEL6335F           │   100    │  500  │   0
    2  │ FO2623ST-...  │ LSGL6335F           │    50    │  200  │   0
  소계 │               │                     │   150    │       │

[C5 라인]   ──────────────────────────────────────── 당일 계획: 240
  ...
  
전체 합계: 927
```

**구현 포인트:**
- 라인 헤더는 배경색 강조 (bg-gray-100)
- 소계 행은 font-semibold
- 전체 합계는 페이지 하단에 별도 카드로 표시
- 모델코드는 고정폭 폰트(font-mono)

### 8-B-5: DailyPlanPrint.tsx (인쇄 뷰어)

같은 데이터를 인쇄에 최적화된 레이아웃으로 표시.

```
┌─────────────────────────────────────────────────────┐
│  LARS - 일일생산계획                                  │
│  날짜: 2026-05-16                    [🖨️ 인쇄] 버튼  │
├─────────────────────────────────────────────────────┤
│ C11 라인                                             │
│ No. │ W/O  │ 모델 │ 당일 수량 │ 총 계획 │ 실적       │
│ ... │ ...  │ ...  │    ...    │   ...   │  ...       │
│ 소계                        150      ...            │
├─────────────────────────────────────────────────────┤
│ C5 라인                                              │
│ ...                                                  │
├─────────────────────────────────────────────────────┤
│ 전체 합계: 927                                       │
└─────────────────────────────────────────────────────┘
```

**CSS 처리:**
- `[🖨️ 인쇄]` 버튼: `onClick={() => window.print()}`
- `@media print` CSS: 버튼 숨김, 사이드바 숨김, 여백 최소화

```css
/* src/index.css 또는 컴포넌트 내 <style> 태그에 추가 */
@media print {
  nav, aside, .no-print, button { display: none !important; }
  body { font-size: 10pt; }
  .print-table { border-collapse: collapse; width: 100%; }
  .print-table th, .print-table td { border: 1px solid #000; padding: 2px 4px; }
}
```

`DailyPlanPrint.tsx`에서는 일반 HTML `<table>` 사용 (shadcn Table 불필요, print CSS 적용 용이).

---

## Task 8-C: 타입 정의 추가

`src/types/` 또는 적절한 타입 파일에 추가:

```typescript
export interface DailyLotView {
  wo_number: string | null;
  model_code: string;
  lot_number: string;
  daily_qty: number;
  planned_qty: number;
  output_qty: number;
  sort_order: number;
}

export interface DailyLineView {
  line_code: string;
  line_name: string;
  lots: DailyLotView[];
  total_daily_qty: number;
}

export interface DailyPlanViewResponse {
  date: string;
  lines: DailyLineView[];
  total_qty: number;
}
```

---

## Task 8-D: 통합 검증

```bash
# 1. 백엔드 Python 문법 검증
cd /test/LARS/backend && source venv/bin/activate
python3 -m py_compile services/daily_plan_service.py
python3 -m py_compile api/routes/dp.py

# 2. 백엔드 재시작
pkill -f "uvicorn main:app" && sleep 2
nohup uvicorn main:app --host 0.0.0.0 --port 8000 > /tmp/lars_backend.log 2>&1 &
sleep 5 && tail -3 /tmp/lars_backend.log

# 3. API 검증
TOKEN=$(curl -s -X POST http://localhost:8000/api/v1/auth/login \
  -H "Content-Type: application/json" -d '{"email":"admin@lars.local","password":"admin1234"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

# 날짜 목록
curl -s "http://localhost:8000/api/v1/dp/dates" -H "Authorization: Bearer $TOKEN" \
  | python3 -c "import sys,json; d=json.load(sys.stdin); print(f'날짜 수: {len(d)}, 범위: {d[0]}~{d[-1]}')"

# 당일 뷰 (2026-05-14)
curl -s "http://localhost:8000/api/v1/dp/daily?date=2026-05-14" -H "Authorization: Bearer $TOKEN" \
  | python3 -c "
import sys,json
d = json.load(sys.stdin)
print(f'전체합계: {d[\"total_qty\"]}')
for line in d['lines']:
    print(f'  {line[\"line_code\"]}: {line[\"total_daily_qty\"]} ({len(line[\"lots\"])}개 W/O)')
"

# 4. TypeScript 검증
cd /test/LARS/.WebUI && npx tsc --noEmit

# 5. 빌드
npm run build
```

---

## 구현 시 주의사항

1. **라우터 순서**: `GET /dp/daily`와 `GET /dp/dates`는 `GET /dp/{plan_id}/lots` 보다 먼저 등록
2. **날짜 비교**: `daily_qty_json` 키가 `"2026-05-14"` 형식이므로 `str(target_date)` 그대로 매칭
3. **daily_qty_json = None 처리**: `json.loads(lot.daily_qty_json or '{}')`으로 None safe 처리
4. **DUMMY 라인**: 결과에서 line_code='DUMMY' 는 필터링하여 미표시 권장
5. **인쇄 시 사이드바 숨김**: 기존 레이아웃 컴포넌트에 `@media print { aside { display: none } }` 필요
6. **Polars 전용 원칙**: 서비스 레이어의 집계/정렬에서 Pandas 사용 금지

---

## 완료 보고 형식

작업 완료 후 `LARS_Project/Phase8_Coder_Report.md`를 작성하여 제출한다.  
보고서에는: `/dp/daily?date=2026-05-14` 응답 샘플, 웹 뷰어 렌더링 확인, 인쇄 뷰어 `window.print()` 동작 확인, TypeScript 오류 0건, 빌드 성공을 포함한다.
