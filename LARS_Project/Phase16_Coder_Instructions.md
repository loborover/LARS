# Phase 16 Coder Instructions — DP Print Format View

> **Chief → Coder 지시문. 구현 완료 후 Phase16_Coder_Report.md를 작성하라.**
> 이 문서의 모든 Task를 순서대로 완전히 구현하라. 부분 구현, 임시 구현 금지.

---

## 배경 및 목표

`/test/AutoReport/DailyPlan/` 폴더에 있는 xlsx 파일 (`DailyPlan [월]-[일]일_C11.xlsx`)의 Sheet1 레이아웃을 웹에서 렌더링하는 "Print View" 탭을 만든다.

### xlsx 참조 파일 레이아웃 (Sheet1)

```
Row 1:  투입시점 | W/O | 부품번호 | 수량 | -  | -   | (Connecter skip) | 1월 | - | - | - | C11-Line | ...
Row 2:  -       | -   | -       | 계획 | IN | OUT |                  | date1 | date2 | date3 | date4 | ...
Row 3:  -       | -   | -       | Σ계획| ΣIN| ΣOUT|                  | Σd1 | Σd2 | Σd3 | Σd4 | ...  ← Summary
Row 4+: [PST datetime] | [W/O] | [Model.Suffix] | [planned] | [IN] | [OUT] | | [d1] | [d2] | ... ← Data rows
```

웹 렌더링 시 `Connecter`, `Meta_Data`, `TPL`, `UPPH` 컬럼은 생략한다 (LARS에 해당 데이터 없음).

---

## Task 16-A: Backend — `/dp/lots-raw` 응답에 `input_qty`, `output_qty` 추가

**파일**: `/test/LARS/backend/api/routes/dp.py`

`GET /lots-raw` 엔드포인트의 result 딕셔너리에 아래 두 필드를 추가한다:

```python
# 기존 코드에서 result.append({...}) 부분을 수정
result.append({
    "line_code": line_code or "",
    "planned_start": lot.planned_start.isoformat() if lot.planned_start else None,
    "wo_number": lot.wo_number,
    "model_number": model_number,
    "planned_qty": lot.planned_qty or 0,
    "input_qty": lot.input_qty or 0,    # ← 추가
    "output_qty": lot.output_qty or 0,  # ← 추가
    "remain_qty": remain_qty,
    "daily_qty": daily_qty,
})
```

`DailyPlanLot` 모델에 이미 `input_qty: int`, `output_qty: int` 필드가 존재한다. 그대로 사용하면 된다.

---

## Task 16-B: Frontend — `LotRaw` 인터페이스 확장

**파일**: `/test/LARS/.WebUI/src/pages/DailyPlanPage.tsx`

`LotRaw` 인터페이스에 `input_qty`, `output_qty` 추가:

```tsx
interface LotRaw {
  line_code: string;
  planned_start: string | null;
  wo_number: string;
  model_number: string;
  planned_qty: number;
  input_qty: number;    // ← 추가
  output_qty: number;   // ← 추가
  remain_qty: number;
  daily_qty: Record<string, number>;
}
```

---

## Task 16-C: 새 컴포넌트 `DailyPlanPrintView.tsx` 생성

**파일**: `/test/LARS/.WebUI/src/components/dp/DailyPlanPrintView.tsx`

이 컴포넌트를 생성하라. 아래 스펙을 정확히 구현하라.

### Props

```tsx
interface Props {
  lots: LotRaw[];  // DailyPlanPage에서 전달하는 filterProps.filtered 결과
}
```

### 내부 상태

```tsx
const [activeLine, setActiveLine] = useState<string | null>(null);  // null = 전체
const [activeModel, setActiveModel] = useState<string | null>(null); // null = 전체
```

### 데이터 계산 로직

```tsx
// 1. 고유 라인 목록 (정렬)
const lines = Array.from(new Set(lots.map(l => l.line_code))).sort();

// 2. 라인 필터 적용
const lineLots = activeLine ? lots.filter(l => l.line_code === activeLine) : lots;

// 3. 해당 라인의 고유 모델 목록 (등장 순서 유지)
const models = Array.from(new Set(lineLots.map(l => l.model_number)));

// 4. 모델 필터 적용
const viewLots = activeModel ? lineLots.filter(l => l.model_number === activeModel) : lineLots;

// 5. PST 기준 정렬
const sortedLots = [...viewLots].sort((a, b) => {
  if (!a.planned_start) return 1;
  if (!b.planned_start) return -1;
  return a.planned_start.localeCompare(b.planned_start);
});

// 6. 동적 날짜 컬럼 (모든 daily_qty key, 정렬)
const dateColumns = Array.from(
  new Set(sortedLots.flatMap(l => Object.keys(l.daily_qty)))
).sort();
```

### 렌더링 구조

```tsx
return (
  <div className="flex flex-col h-full">
    {/* 컨트롤 영역: print 시 숨김 */}
    <div className="print:hidden flex flex-col gap-2 mb-3">

      {/* 라인 탭 */}
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-xs font-bold text-gray-500 uppercase tracking-wide">Line:</span>
        <button
          onClick={() => { setActiveLine(null); setActiveModel(null); }}
          className={`px-3 py-1 text-xs rounded-full border font-semibold transition-all ${
            activeLine === null
              ? 'bg-gray-800 text-white border-gray-800'
              : 'bg-white text-gray-600 border-gray-300 hover:border-gray-500'
          }`}
        >
          ALL
        </button>
        {lines.map(line => (
          <button
            key={line}
            onClick={() => { setActiveLine(line); setActiveModel(null); }}
            className={`px-3 py-1 text-xs rounded-full border font-semibold transition-all ${
              activeLine === line
                ? 'bg-purple-600 text-white border-purple-600'
                : 'bg-white text-purple-600 border-purple-300 hover:border-purple-500'
            }`}
          >
            {line}
          </button>
        ))}
      </div>

      {/* 모델 pills */}
      <div className="flex items-center gap-1.5 flex-wrap">
        <span className="text-xs font-bold text-gray-500 uppercase tracking-wide">Model:</span>
        <button
          onClick={() => setActiveModel(null)}
          className={`px-2.5 py-0.5 text-[11px] rounded-full border font-mono transition-all ${
            activeModel === null
              ? 'bg-blue-600 text-white border-blue-600'
              : 'bg-white text-gray-500 border-gray-200 hover:border-blue-400'
          }`}
        >
          ALL ({lineLots.length})
        </button>
        {models.map(model => {
          const count = lineLots.filter(l => l.model_number === model).length;
          return (
            <button
              key={model}
              onClick={() => setActiveModel(prev => prev === model ? null : model)}
              className={`px-2.5 py-0.5 text-[11px] rounded-full border font-mono transition-all ${
                activeModel === model
                  ? 'bg-blue-600 text-white border-blue-600'
                  : 'bg-white text-blue-700 border-blue-200 hover:border-blue-500 hover:bg-blue-50'
              }`}
            >
              {model} ({count})
            </button>
          );
        })}
      </div>

      {/* 프린트 버튼 */}
      <div className="flex justify-end">
        <button
          onClick={() => window.print()}
          className="flex items-center gap-1.5 px-4 py-1.5 text-xs bg-gray-800 text-white rounded-lg hover:bg-gray-700 font-semibold transition-colors"
        >
          <Printer size={13} />
          Print (A3 Landscape)
        </button>
      </div>
    </div>

    {/* 테이블 영역 */}
    <div className="flex-1 overflow-auto bg-white rounded-xl border shadow-sm print:overflow-visible print:shadow-none print:border-0">
      {sortedLots.length === 0 ? (
        <div className="flex items-center justify-center h-full text-gray-400 text-sm">
          데이터가 없습니다.
        </div>
      ) : (
        <table className="w-full text-[11px] border-collapse">
          <thead className="sticky top-0 z-10">
            {/* Header Row 1: Section labels */}
            <tr className="bg-gray-700 text-white">
              <th rowSpan={2} className="px-2 py-1.5 text-center border border-gray-600 whitespace-nowrap font-bold">
                투입시점
              </th>
              <th rowSpan={2} className="px-2 py-1.5 text-center border border-gray-600 whitespace-nowrap font-bold">
                W/O (제번)
              </th>
              <th rowSpan={2} className="px-2 py-1.5 text-center border border-gray-600 whitespace-nowrap font-bold">
                부품번호 (Model.Suffix)
              </th>
              <th colSpan={3} className="px-2 py-1.5 text-center border border-gray-600 font-bold">
                수량
              </th>
              {dateColumns.length > 0 && (
                <th colSpan={dateColumns.length} className="px-2 py-1.5 text-center border border-gray-600 font-bold">
                  {/* 날짜 섹션 레이블: 첫 날짜의 월만 표시 */}
                  {dateColumns[0].slice(0, 7).replace('-', '월 ').replace(/^(\d+)월 (\d+)/, (_, y, m) => `${m}월`)}
                </th>
              )}
              {activeLine && (
                <th rowSpan={2} className="px-2 py-1.5 text-center border border-gray-600 whitespace-nowrap font-bold text-yellow-300">
                  {activeLine}-Line
                </th>
              )}
            </tr>
            {/* Header Row 2: Sub-column labels */}
            <tr className="bg-gray-600 text-white">
              <th className="px-2 py-1 text-center border border-gray-500 whitespace-nowrap">계획</th>
              <th className="px-2 py-1 text-center border border-gray-500 whitespace-nowrap">IN</th>
              <th className="px-2 py-1 text-center border border-gray-500 whitespace-nowrap">OUT</th>
              {dateColumns.map(d => (
                <th key={d} className="px-2 py-1 text-center border border-gray-500 whitespace-nowrap">
                  {d.slice(5).replace('-', '/')}  {/* MM/DD */}
                </th>
              ))}
            </tr>
            {/* Summary Row 3: Totals */}
            <tr className="bg-gray-800 text-white font-bold">
              <td colSpan={3} className="px-2 py-1.5 text-right border border-gray-600 text-[10px] uppercase tracking-wider text-gray-300">
                Σ Total ({sortedLots.length} lots)
              </td>
              <td className="px-2 py-1.5 text-right border border-gray-600">
                {sortedLots.reduce((s, l) => s + l.planned_qty, 0).toLocaleString()}
              </td>
              <td className="px-2 py-1.5 text-right border border-gray-600 text-green-300">
                {sortedLots.reduce((s, l) => s + l.input_qty, 0).toLocaleString()}
              </td>
              <td className="px-2 py-1.5 text-right border border-gray-600 text-blue-300">
                {sortedLots.reduce((s, l) => s + l.output_qty, 0).toLocaleString()}
              </td>
              {dateColumns.map(d => (
                <td key={d} className="px-2 py-1.5 text-right border border-gray-600 text-yellow-300">
                  {sortedLots.reduce((s, l) => s + (l.daily_qty[d] || 0), 0).toLocaleString()}
                </td>
              ))}
              {activeLine && <td className="border border-gray-600" />}
            </tr>
          </thead>
          <tbody className="divide-y">
            {sortedLots.map((lot, idx) => (
              <tr
                key={idx}
                className={`transition-colors ${idx % 2 === 0 ? 'bg-white' : 'bg-gray-50'} hover:bg-blue-50/60`}
              >
                <td className="px-2 py-1.5 text-gray-500 whitespace-nowrap font-mono text-[10px]">
                  {lot.planned_start ? lot.planned_start.slice(0, 16).replace('T', ' ') : '-'}
                </td>
                <td className="px-2 py-1.5 font-mono font-bold text-gray-800 whitespace-nowrap">
                  {lot.wo_number}
                </td>
                <td className="px-2 py-1.5 text-blue-700 font-semibold whitespace-nowrap">
                  {lot.model_number}
                </td>
                <td className="px-2 py-1.5 text-right font-semibold text-gray-700">
                  {lot.planned_qty.toLocaleString()}
                </td>
                <td className={`px-2 py-1.5 text-right font-medium ${lot.input_qty > 0 ? 'text-green-700' : 'text-gray-300'}`}>
                  {lot.input_qty > 0 ? lot.input_qty.toLocaleString() : '-'}
                </td>
                <td className={`px-2 py-1.5 text-right font-medium ${lot.output_qty > 0 ? 'text-blue-700 font-semibold' : 'text-gray-300'}`}>
                  {lot.output_qty > 0 ? lot.output_qty.toLocaleString() : '-'}
                </td>
                {dateColumns.map(d => (
                  <td key={d} className={`px-2 py-1.5 text-right ${lot.daily_qty[d] ? 'text-gray-900 font-medium bg-blue-50/30' : 'text-gray-200'}`}>
                    {lot.daily_qty[d] ? lot.daily_qty[d].toLocaleString() : '-'}
                  </td>
                ))}
                {activeLine && <td className="px-2 py-1.5 text-center text-[10px] text-gray-400">{lot.line_code}</td>}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  </div>
);
```

### 필수 임포트

```tsx
import { useState } from 'react';
import { Printer } from 'lucide-react';

// LotRaw 인터페이스는 DailyPlanPage.tsx에서 export하거나 공통 types 파일로 분리하여 import
// 가장 간단한 방법: DailyPlanPrintView.tsx 내부에도 동일 인터페이스를 선언
interface LotRaw {
  line_code: string;
  planned_start: string | null;
  wo_number: string;
  model_number: string;
  planned_qty: number;
  input_qty: number;
  output_qty: number;
  remain_qty: number;
  daily_qty: Record<string, number>;
}
```

---

## Task 16-D: `DailyPlanPage.tsx`에 뷰 전환 탭 추가

**파일**: `/test/LARS/.WebUI/src/pages/DailyPlanPage.tsx`

### 변경 사항

1. **import 추가**:
```tsx
import DailyPlanPrintView from '../components/dp/DailyPlanPrintView';
```

2. **state 추가** (컴포넌트 상단):
```tsx
const [viewMode, setViewMode] = useState<'raw' | 'print'>('raw');
```

3. **헤더 영역에 뷰 전환 버튼 추가** (sticky div 안, `hasAnyFilter` 버튼 옆):
```tsx
{/* 뷰 전환 탭 */}
<div className="flex rounded-lg border border-gray-200 overflow-hidden text-xs font-semibold">
  <button
    onClick={() => setViewMode('raw')}
    className={`px-3 py-1.5 transition-colors ${viewMode === 'raw' ? 'bg-gray-800 text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
  >
    Raw Table
  </button>
  <button
    onClick={() => setViewMode('print')}
    className={`px-3 py-1.5 transition-colors ${viewMode === 'print' ? 'bg-gray-800 text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
  >
    Print View
  </button>
</div>
```

4. **우측 테이블 영역을 조건부 렌더링으로 교체**:

현재 우측 `{/* 우측: Flat 테이블 */}` div 전체를 아래로 교체:

```tsx
{/* 우측: 테이블 영역 */}
<div className="flex-1 overflow-hidden flex flex-col">
  {viewMode === 'raw' ? (
    /* 기존 Raw 테이블 div 전체 그대로 유지 */
    <div className="flex-1 overflow-hidden bg-white rounded-xl border shadow-sm flex flex-col">
      {/* ... 기존 isLoading / filteredLots 렌더링 코드 그대로 ... */}
    </div>
  ) : (
    /* Print View */
    isLoading ? (
      <div className="flex-1 flex flex-col items-center justify-center space-y-4">
        <div className="w-10 h-10 border-4 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
        <p className="text-gray-400 text-sm font-medium">데이터 로드 중...</p>
      </div>
    ) : !selectedBatchId ? (
      <div className="flex-1 flex items-center justify-center text-gray-400 font-medium">
        좌측에서 Batch를 선택하세요.
      </div>
    ) : (
      <DailyPlanPrintView lots={filteredLots} />
    )
  )}
</div>
```

**중요**: `filteredLots`를 Print View에 전달한다. Raw Table의 필터 상태와 Print View의 필터 상태는 독립적이다 — Raw Table의 FilterableHeader 필터는 `filteredLots`를 만들고, Print View 내부에서 Line/Model 필터를 별도로 적용한다.

---

## Task 16-E: Print CSS 설정

**파일**: `/test/LARS/.WebUI/src/index.css` (또는 `global.css`)

파일 끝에 아래 `@media print` 블록을 추가하라:

```css
@media print {
  @page {
    size: A3 landscape;
    margin: 8mm 10mm;
  }

  /* 사이드바, 헤더, 컨트롤 숨김 */
  nav,
  aside,
  .print\:hidden {
    display: none !important;
  }

  /* 테이블이 페이지를 꽉 채우도록 */
  body {
    font-size: 9pt;
  }

  table {
    font-size: 8pt;
    border-collapse: collapse;
  }

  th, td {
    border: 0.5pt solid #ccc !important;
  }

  thead {
    display: table-header-group;
  }

  tr {
    page-break-inside: avoid;
  }
}
```

---

## Task 16-F: 디렉토리 생성 및 빌드 검증

### 1. 디렉토리 생성 확인
```bash
mkdir -p /test/LARS/.WebUI/src/components/dp
```

### 2. TypeScript 빌드 검증
```bash
cd /test/LARS/.WebUI && npx tsc --noEmit 2>&1 | head -30
```
TypeScript 오류가 0건이어야 한다.

### 3. Vite 빌드 및 프리뷰 재시작
```bash
cd /test/LARS/.WebUI && npm run build 2>&1 | tail -10
```
빌드 성공 후, 기존 `vite preview` 프로세스를 종료하고 재시작:
```bash
pkill -f "vite preview" 2>/dev/null; sleep 1
cd /test/LARS/.WebUI && nohup npx vite preview --port 3000 --host 0.0.0.0 > /tmp/vite_preview.log 2>&1 &
```

### 4. 백엔드 재시작 (16-A 변경 적용)
```bash
pkill -f "uvicorn main:app" 2>/dev/null; sleep 2
cd /test/LARS/backend && nohup /test/LARS/backend/venv/bin/uvicorn main:app --host 0.0.0.0 --port 8000 --reload > /tmp/backend.log 2>&1 &
sleep 3 && curl -s http://localhost:8000/api/v1/health/status
```

### 5. 동작 검증
```bash
# lots-raw 응답에 input_qty, output_qty 포함 확인
curl -s "http://localhost:8000/api/v1/dp/lots-raw?batch_id=1" | python3 -c "import sys,json; d=json.load(sys.stdin); print(list(d[0].keys()) if d else 'empty')"
```
응답 키 목록에 `input_qty`, `output_qty`가 포함되어야 한다.

---

## 완료 기준 (Done Criteria)

1. `GET /dp/lots-raw` 응답에 `input_qty`, `output_qty` 포함 ✓
2. `DailyPlanPrintView.tsx` 생성 완료 ✓
3. Raw Table / Print View 탭 전환 동작 ✓
4. Print View: Line 탭 (ALL / C11 / C5 / C7 등) 동작 ✓
5. Print View: Model pill 클릭 시 해당 모델 lot만 표시 ✓
6. Print View: 3-row header (Section labels / Sub-labels / Totals) 정확히 렌더링 ✓
7. Print View: 데이터 rows가 PST 오름차순 정렬 ✓
8. `window.print()` 호출 시 A3 landscape 출력 ✓
9. TypeScript 오류 0건, Vite 빌드 성공 ✓
10. `Phase16_Coder_Report.md` 작성 완료 ✓

---

## 주의사항

- **`components/dp/` 디렉토리**가 없으면 먼저 생성하라.
- **`LotRaw` 인터페이스 중복 선언** 문제를 피하려면 `DailyPlanPage.tsx`에서 `export interface LotRaw {...}`로 수정하고 `DailyPlanPrintView.tsx`에서 `import { LotRaw } from '../../pages/DailyPlanPage'`로 가져와도 된다. 어느 방법이든 TypeScript 오류 없이 빌드되면 된다.
- **`filterProps.filtered`** (즉 `filteredLots`)를 Print View에 전달한다. Print View는 그 위에 Line/Model 필터를 독립적으로 적용한다.
- `viewMode`가 `'print'`로 바뀔 때 `activeLine`과 `activeModel`은 Print View 내부 state이므로 자동으로 `null` (전체 표시)로 시작된다.
- 라인 탭에서 다른 라인 선택 시 `activeModel`을 `null`로 리셋해야 한다 (위 코드에 이미 포함됨).
