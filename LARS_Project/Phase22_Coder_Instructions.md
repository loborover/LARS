# Phase 22 Coder Instructions — PartList UX 개선 & Vendor 파싱 수정

## 개요

이 Phase에서는 세 가지 영역을 다룬다.

1. **Vendor Raw 파싱 버그 수정** — `EKHQ_서브원_KR123414` 형태가 파싱되지 않는 문제
2. **PartList Lot View 개선** — 열 헤더 2행(Description/PartNumber+UOM), BOM 더블클릭 이동
3. **PartList PSI Matrix 개선** — UOM 고정 열 추가, Vendor 파싱 적용

---

## 사전 확인 — 이미 완료된 변경 사항

> 다음 항목은 이미 코드에 반영되어 있으므로 **중복 작업 금지**.

- `PartListPage.tsx` PSI Matrix 고정 열 순서: `Description → 1차협력사(vendor_raw) → 2차협력사(lower_vendor_raw) → 품번`
- `PartListPage.tsx` PSI Matrix 날짜 헤더 2행: Week번호(월요일 시작) + MM-DD/요일
- `part_list_service.py` `get_psi_matrix()` SQL: `LEFT JOIN item_master im ON im.part_number = pls.part_number` 추가 → `lower_vendor_raw` 포함
- `part_list_service.py` 필터 파라미터: `line_code`, `supply_type`, `expeditor_user_id` 추가 완료
- `PartListPage.tsx` 필터 패널 UI (Expeditor/SupplyType/Line 셀렉트박스) 완료

---

## Task 22-A — Vendor Raw 파싱 버그 수정

### 파일: `backend/services/item_master_service.py`

**문제 원인:**
```python
# 현재 (버그 있음)
_VENDOR_PATTERN = re.compile(r'^[A-Z]+_(.+)_KR\d+$')
```
- `_KR\d+$` 패턴이 `KR` 코드만 매칭 → `CN004859`, `CN003742` 등 중국 협력사 코드는 매칭 실패
- 실제 데이터 예시:
  - `EKHQ_ZOPPAS INDUSTRIES HANG ZHOU LTD._CN004859` → ❌ 파싱 실패 (CN 코드)
  - `EKHQ_주식회사제임스텍_KR011021` → ✅ 파싱 성공 (KR 코드)
  - `PANASONIC CORPORATION` → ✅ 정상 (언더스코어 없음, raw 그대로 반환)

**수정 방법 — 정규식 제거, split 기반 로직으로 교체:**

```python
# 수정 후
def parse_vendor_name(vendor_raw: str | None) -> str | None:
    """
    PLANT_VendorName_VendorCode 형식에서 VendorName 부분만 추출.
    언더스코어가 2개 미만이면 원본 그대로 반환.
    VendorName 자체에 언더스코어가 포함된 경우도 처리 (첫 부분=Plant, 마지막 부분=VendorCode).
    """
    if not vendor_raw:
        return None
    parts = vendor_raw.split('_')
    if len(parts) >= 3:
        return '_'.join(parts[1:-1])   # Plant 제외, VendorCode 제외
    return vendor_raw
```

`_VENDOR_PATTERN` 변수 및 `import re` 제거 (re 모듈이 다른 곳에서도 사용되지 않는 경우만 제거).

---

## Task 22-B — Vendor 이름 파싱 유틸리티 공유

### 파일: `backend/core/utils.py` (신규 생성)

part_list_service, psi_service 등에서도 동일 로직 필요. 공유 유틸 모듈에 정의한다.

```python
# backend/core/utils.py

def parse_vendor_name(vendor_raw: str | None) -> str | None:
    """PLANT_VendorName_VendorCode 형식에서 VendorName 추출. 그 외 형식은 원본 반환."""
    if not vendor_raw:
        return None
    parts = vendor_raw.split('_')
    if len(parts) >= 3:
        return '_'.join(parts[1:-1])
    return vendor_raw
```

그런 다음 `item_master_service.py`에서:
```python
from core.utils import parse_vendor_name
# 기존 로컬 정의 제거
```

---

## Task 22-C — `LotViewResponse` 스키마 확장

### 파일: `backend/schemas/part_list.py`

`LotViewResponse`에 `part_meta` 필드 추가. 각 품번의 description과 uom을 프론트엔드에 전달하기 위함.

```python
# 수정 전
class LotViewResponse(BaseModel):
    batch_id: int
    part_columns: List[str]
    rows: List[LotViewRow]

# 수정 후
class PartMeta(BaseModel):
    description: Optional[str] = None
    uom: str = "EA"

class LotViewResponse(BaseModel):
    batch_id: int
    part_columns: List[str]
    part_meta: Dict[str, PartMeta]   # key=part_number, value=PartMeta
    rows: List[LotViewRow]
```

---

## Task 22-D — `get_lot_view()` 서비스 수정

### 파일: `backend/services/part_list_service.py`

`get_lot_view()` SQL에 `description`과 `uom` 컬럼 추가 후, Polars DataFrame에서 part_meta 딕셔너리를 추출해 반환값에 포함한다.

**SQL 수정:**

```sql
-- 수정 후: description, uom 추가
SELECT
    pls.lot_id,
    dpl.wo_number,
    dpl.model_code,
    dpl.suffix,
    dpl.planned_qty,
    dp.plan_date::date AS plan_date,
    pls.part_number,
    pls.required_qty,
    pls.description,          -- 추가
    pls.uom                   -- 추가
FROM ...
```

**part_meta 추출 (Polars 처리부):**

```python
# top_parts 선정 후 기존 위치에 추가
meta_df = (
    df_filtered
    .group_by("part_number")
    .agg([
        pl.first("description").alias("description"),
        pl.first("uom").alias("uom"),
    ])
)
part_meta = {
    r["part_number"]: {"description": r["description"], "uom": r["uom"] or "EA"}
    for r in meta_df.to_dicts()
}
```

**반환값 수정:**

```python
return {
    "batch_id": batch_id,
    "part_columns": existing_part_cols,
    "part_meta": part_meta,    # 추가
    "rows": rows,
}
```

---

## Task 22-E — `get_psi_matrix()` 응답에 Vendor 파싱 적용

### 파일: `backend/services/part_list_service.py`

```python
from core.utils import parse_vendor_name   # 상단 import에 추가

# rows 빌드 루프 내부 수정
rows.append({
    "part_number": r["part_number"],
    "description": r.get("description"),
    "vendor_raw": parse_vendor_name(r.get("vendor_raw")),
    "lower_vendor_raw": parse_vendor_name(r.get("lower_vendor_raw")),
    "uom": r.get("uom") or "EA",
    "total_qty": r["total_qty"],
    "by_date": by_date,
})
```

`get_pl_summary()` 응답에도 동일 적용:
```python
return [
    {
        "part_number": r.part_number,
        "description": r.description,
        "total_required_qty": float(r.total_required_qty),
        "uom": r.uom,
        "vendor_raw": parse_vendor_name(r.vendor_raw),
    }
    for r in rows
]
```

---

## Task 22-F — Frontend: LotViewTable 열 헤더 2행 구조

### 파일: `WebUI/src/pages/PartListPage.tsx` — `LotViewTable` 컴포넌트

**타입 확장:**

```typescript
interface PartMeta {
  description: string | null;
  uom: string;
}

interface LotViewData {
  batch_id: number;
  part_columns: string[];
  part_meta: Record<string, PartMeta>;   // 추가
  rows: any[];
}
```

**2행 헤더 구현 (thead):**

```tsx
<thead className="sticky top-0 z-10 bg-gray-50 shadow-sm">
  {/* Row 1: 메타 레이블 + Description */}
  <tr>
    {META_LABELS.map((label, i) => (
      <th key={i} rowSpan={2}
        className="sticky px-2 py-2 text-[10px] font-black text-gray-500 uppercase border-b border-r whitespace-nowrap bg-gray-100"
        style={{ left: `${i * 100}px`, zIndex: 20 }}>
        {label}
      </th>
    ))}
    {part_columns.map(pn => {
      const meta = part_meta[pn];
      return (
        <th key={pn}
          className="px-2 py-1.5 text-[10px] font-medium text-gray-500 border-b border-r whitespace-nowrap max-w-[100px] truncate"
          title={meta?.description ?? pn}>
          {meta?.description
            ? (meta.description.length > 12 ? meta.description.slice(0, 12) + '…' : meta.description)
            : '-'}
        </th>
      );
    })}
  </tr>
  {/* Row 2: 품번 + UOM */}
  <tr>
    {part_columns.map(pn => {
      const meta = part_meta[pn];
      return (
        <th key={pn} className="px-2 py-1.5 border-b border-r whitespace-nowrap" title={pn}>
          <div className="text-[10px] font-bold text-blue-600 font-mono">
            {pn.length > 10 ? pn.slice(-8) : pn}
          </div>
          <div className="text-[9px] text-gray-400 font-semibold">
            {meta?.uom ?? 'EA'}
          </div>
        </th>
      );
    })}
  </tr>
</thead>
```

---

## Task 22-G — Frontend: LotViewTable BOM 더블클릭 이동

### 파일: `WebUI/src/pages/PartListPage.tsx`

DP 모듈(`DailyPlanPage.tsx`)에서 이미 구현된 BOM 더블클릭 이동 기능을 재활용한다.

```typescript
import { useNavigate } from 'react-router-dom';

const navigate = useNavigate();

const { data: bomModels = [] } = useQuery<{ model_number: string }[]>({
  queryKey: ['bom-models-all'],
  queryFn: async () => (await apiClient.get('/bom/models', { params: { is_active: true } })).data,
  staleTime: 60000,
});
const bomModelSet = useMemo(() => new Set(bomModels.map(m => m.model_number)), [bomModels]);
```

**모델 셀 수정:**

```tsx
<td
  className={`sticky bg-white px-2 py-1 border-r font-semibold whitespace-nowrap select-none ${
    bomModelSet.has(row.model_number)
      ? 'text-blue-600 cursor-pointer hover:text-blue-800 hover:underline'
      : 'text-red-400 cursor-not-allowed'
  }`}
  style={{ left: '200px', zIndex: 10 }}
  title={bomModelSet.has(row.model_number) ? `더블클릭: ${row.model_number} BOM 보기` : `BOM 미등록: ${row.model_number}`}
  onDoubleClick={() => {
    if (bomModelSet.has(row.model_number)) {
      navigate(`/bom/${encodeURIComponent(row.model_number)}`);
    }
  }}
>
  {row.model_number}
</td>
```

---

## Task 22-H — Frontend: PSI Matrix UOM 고정 열 추가

### 파일: `WebUI/src/pages/PartListPage.tsx` — `PsiMatrixTable` 컴포넌트

```typescript
const COL_DESC = 160;
const COL_VENDOR = 100;
const COL_LOWER = 90;
const COL_PN = 130;
const COL_UOM = 50;    // 추가
const FIXED_WIDTH = COL_DESC + COL_VENDOR + COL_LOWER + COL_PN + COL_UOM;
```

헤더, 데이터 행, Foot 행에 UOM 열 추가:
```tsx
// 헤더
<th style={{ left: `${COL_DESC + COL_VENDOR + COL_LOWER + COL_PN}px`, zIndex: 20, minWidth: `${COL_UOM}px` }}>UOM</th>

// 데이터 행
<td style={{ left: `${COL_DESC + COL_VENDOR + COL_LOWER + COL_PN}px`, zIndex: 10 }}>
  {row.uom || 'EA'}
</td>
```

---

## 검증 체크리스트 (Task A~H)

```
[ ] 22-A: EKHQ_ZOPPAS INDUSTRIES HANG ZHOU LTD._CN004859 → ZOPPAS INDUSTRIES HANG ZHOU LTD.
[ ] 22-A: EKHQ_주식회사제임스텍_KR011021 → 주식회사제임스텍
[ ] 22-A: PANASONIC CORPORATION → 그대로 반환
[ ] 22-C: LotViewResponse에 part_meta 필드 포함 확인
[ ] 22-F: Lot View 열 헤더 2행 렌더링 (Row1=Description, Row2=품번+UOM)
[ ] 22-G: BOM 등록 모델 더블클릭 시 /bom/{model_number}로 이동
[ ] 22-G: BOM 미등록 모델은 빨간 텍스트 + cursor-not-allowed
[ ] 22-H: PSI Matrix UOM 고정 열 표시
[ ] 22-E: PSI Matrix vendor 열에서 파싱된 이름 표시
[ ] npx tsc --noEmit 오류 0건
```

---

---

# Phase 22 추가 — PSI 모듈 다중 필터 & 열 순서 변경

## 개요

이 섹션(Task 22-I ~ 22-N)은 PSI 소요량 매트릭스에 다음을 추가한다.

1. **다중 선택 필터** — Line, Model.Suffix, WorkOrder를 검색+다중선택 가능한 콤보박스로 필터링
2. **고정 열 순서 변경** — `협력사 → 2차협력사 → 품번 → 품명 → 재고`
3. **Vendor 파싱 적용** — `build_psi_matrix_v2()` 응답에도 `parse_vendor_name()` 적용

---

## 사전 확인 — 이미 완료된 변경 사항

> 다음 항목은 **이미 코드에 반영**되어 있으므로 중복 작업 금지.

- `PSIPage.tsx`: Expeditor(단일 select), SupplyType(단일 select) 필터 존재
- `PSIMatrixV2.tsx`: `MatrixRow` interface에 `vendor_secondary` 필드 이미 선언됨
- `psi_service.py` `build_psi_matrix_v2()`: `expeditor_user_id`, `supply_type`, `vendor_code` 파라미터 이미 존재
- `psi.py` `/psi/matrix-v2` endpoint: `expeditor_user_id`, `supply_type`, `vendor_code` 파라미터 이미 존재

---

## Task 22-I — Backend: `GET /psi/filter-options` 엔드포인트

### 파일: `backend/services/psi_service.py`

```python
async def get_psi_filter_options(session: AsyncSession) -> dict:
    """PSI 필터 선택지 반환: lines, models, work_orders"""
    from services.part_list_service import get_target_dp_batch_id
    from models.daily_plan import DailyPlanLot

    batch_id = await get_target_dp_batch_id()
    if not batch_id:
        return {"lines": [], "models": [], "work_orders": []}

    stmt = select(
        DailyPlanLot.line_code,
        DailyPlanLot.model_code,
        DailyPlanLot.suffix,
        DailyPlanLot.wo_number,
    ).where(DailyPlanLot.batch_id == batch_id).distinct()

    res = await session.execute(stmt)
    rows = res.all()

    lines = sorted({r.line_code for r in rows if r.line_code})
    models = sorted({
        f"{r.model_code}.{r.suffix}" if r.suffix else r.model_code
        for r in rows if r.model_code
    })
    work_orders = sorted({r.wo_number for r in rows if r.wo_number})

    return {"lines": lines, "models": models, "work_orders": work_orders}
```

### 파일: `backend/api/routes/psi.py`

`/psi/matrix-v2` 엔드포인트보다 **위**에 선언:

```python
@router.get("/filter-options")
async def get_psi_filter_options(
    session: AsyncSession = Depends(get_session),
):
    """PSI 필터 선택지 (현재 Target Batch 기준): lines, models, work_orders"""
    return await psi_service.get_psi_filter_options(session)
```

---

## Task 22-J — Backend: `build_psi_matrix_v2()` 다중 필터 확장

### 파일: `backend/services/psi_service.py`

**함수 시그니처 수정:**

```python
async def build_psi_matrix_v2(
    session: AsyncSession,
    date_from: date,
    days: int = 7,
    expeditor_user_id: Optional[int] = None,
    supply_type: Optional[str] = None,
    vendor_code: Optional[str] = None,
    line_codes: list[str] | None = None,
    model_numbers: list[str] | None = None,
    wo_numbers: list[str] | None = None,
) -> dict:
```

**supply_type 필터 직후에 삽입 (약 590라인):**

```python
    if line_codes or model_numbers or wo_numbers:
        from models.daily_plan import DailyPlanLot
        from models.part_list import PartListSnapshot
        from sqlalchemy import or_, and_

        lot_stmt = select(DailyPlanLot.id)
        if line_codes:
            lot_stmt = lot_stmt.where(DailyPlanLot.line_code.in_(line_codes))
        if model_numbers:
            model_conditions = []
            for mn in model_numbers:
                if '.' in mn:
                    mc, sf = mn.split('.', 1)
                    model_conditions.append(
                        and_(DailyPlanLot.model_code == mc, DailyPlanLot.suffix == sf)
                    )
                else:
                    model_conditions.append(DailyPlanLot.model_code == mn)
            lot_stmt = lot_stmt.where(or_(*model_conditions))
        if wo_numbers:
            lot_stmt = lot_stmt.where(DailyPlanLot.wo_number.in_(wo_numbers))

        lot_res = await session.execute(lot_stmt)
        lot_ids = [r[0] for r in lot_res.all()]
        if not lot_ids:
            return {"date_columns": date_strs, "rows": []}

        snap_pn_stmt = (
            select(PartListSnapshot.part_number)
            .where(PartListSnapshot.lot_id.in_(lot_ids))
            .distinct()
        )
        snap_pn_res = await session.execute(snap_pn_stmt)
        filtered_pns = {r[0] for r in snap_pn_res.all()}

        part_numbers = [pn for pn in part_numbers if pn in filtered_pns]
        items = [it for it in items if it.part_number in filtered_pns]
        if not items:
            return {"date_columns": date_strs, "rows": []}
```

**rows.append() 내부 — Vendor 파싱 적용:**

```python
from core.utils import parse_vendor_name   # 파일 상단 import

rows.append({
    "item_id": item.id,
    "part_number": pn,
    "description": item.description or binfo.get("bom_desc"),
    "level": binfo.get("level"),
    "supply_type": binfo.get("supply_type"),
    "uom": binfo.get("uom", "EA"),
    "vendor_primary": parse_vendor_name(item.vendor_raw),
    "vendor_secondary": parse_vendor_name(item.lower_vendor_raw),
    "plan_qty": plan_qty_map.get(pn, 0.0),
    "inventory_qty": inventory,
    "by_date": by_date,
})
```

---

## Task 22-K — Backend: `/psi/matrix-v2` 라우트 파라미터 확장

### 파일: `backend/api/routes/psi.py`

```python
@router.get("/matrix-v2", response_model=PsiMatrixV2Response)
async def get_psi_matrix_v2(
    date_from: Optional[date] = None,
    days: int = Query(7, ge=1, le=60),
    expeditor_user_id: Optional[int] = None,
    supply_type: Optional[str] = None,
    vendor_code: Optional[str] = None,
    line_codes: List[str] = Query(default=[]),
    model_numbers: List[str] = Query(default=[]),
    wo_numbers: List[str] = Query(default=[]),
    session: AsyncSession = Depends(get_session),
):
    from datetime import date as date_type
    if date_from is None:
        date_from = date_type.today()

    data = await psi_service.build_psi_matrix_v2(
        session,
        date_from=date_from,
        days=days,
        expeditor_user_id=expeditor_user_id,
        supply_type=supply_type,
        vendor_code=vendor_code,
        line_codes=line_codes or None,
        model_numbers=model_numbers or None,
        wo_numbers=wo_numbers or None,
    )
    return data
```

> FastAPI `Query(default=[])` 패턴: `?line_codes=A&line_codes=B` 형식으로 다중 값 수신.

---

## Task 22-L — Frontend: `MultiSelectCombobox` 컴포넌트

### 파일: `WebUI/src/components/MultiSelectCombobox.tsx` (신규 생성)

```tsx
import React, { useState, useEffect, useRef, useMemo } from 'react';
import { ChevronDown } from 'lucide-react';

interface MultiSelectComboboxProps {
  label: string;
  options: string[];
  selected: string[];
  onChange: (values: string[]) => void;
  placeholder?: string;
  maxDisplayed?: number;
}

export function MultiSelectCombobox({
  label, options, selected, onChange,
  placeholder = '전체', maxDisplayed = 2,
}: MultiSelectComboboxProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    if (open) document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [open]);

  const filtered = useMemo(
    () => options.filter(o => o.toLowerCase().includes(search.toLowerCase())),
    [options, search]
  );

  const toggle = (val: string) =>
    onChange(selected.includes(val) ? selected.filter(v => v !== val) : [...selected, val]);

  const clearAll = (e: React.MouseEvent) => { e.stopPropagation(); onChange([]); };

  const displayText = selected.length === 0
    ? placeholder
    : selected.length <= maxDisplayed
      ? selected.join(', ')
      : `${selected.slice(0, maxDisplayed).join(', ')} +${selected.length - maxDisplayed}`;

  return (
    <div className="space-y-1 relative" ref={ref}>
      <label className="text-[10px] font-black text-gray-400 uppercase tracking-wider block">{label}</label>
      <button
        type="button"
        onClick={() => setOpen(o => !o)}
        className={`flex items-center gap-1.5 h-8 min-w-[120px] max-w-[200px] rounded-md border px-2 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors ${
          selected.length > 0 ? 'border-blue-400 bg-blue-50 text-blue-700' : 'border-gray-200 bg-white text-gray-600'
        }`}
      >
        <span className="flex-1 text-left truncate">{displayText}</span>
        {selected.length > 0 && (
          <span className="flex-shrink-0 w-4 h-4 rounded-full bg-blue-500 text-white text-[9px] flex items-center justify-center font-black cursor-pointer hover:bg-blue-700"
            onClick={clearAll} title="선택 해제">×</span>
        )}
        <ChevronDown size={12} className={`flex-shrink-0 transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div className="absolute top-full left-0 mt-1 z-50 bg-white border border-gray-200 rounded-lg shadow-lg w-56">
          <div className="p-2 border-b">
            <input
              type="text" value={search}
              onChange={e => setSearch(e.target.value)}
              onKeyDown={e => { if (e.key === 'Escape') setOpen(false); }}
              placeholder="검색..." autoFocus
              className="w-full h-7 rounded border px-2 text-xs focus:outline-none focus:ring-1 focus:ring-blue-400"
            />
          </div>
          {selected.length > 0 && (
            <div className="px-3 py-1.5 border-b">
              <button type="button" onClick={() => onChange([])}
                className="text-[10px] text-red-500 hover:text-red-700 font-bold">
                전체 해제 ({selected.length}개 선택됨)
              </button>
            </div>
          )}
          <ul className="max-h-48 overflow-y-auto py-1">
            {filtered.length === 0 ? (
              <li className="px-3 py-2 text-xs text-gray-400">검색 결과 없음</li>
            ) : filtered.map(opt => (
              <li key={opt}>
                <label className="flex items-center gap-2 px-3 py-1.5 hover:bg-gray-50 cursor-pointer text-xs">
                  <input type="checkbox" checked={selected.includes(opt)} onChange={() => toggle(opt)}
                    className="rounded border-gray-300 text-blue-600 focus:ring-blue-500" />
                  <span className="truncate" title={opt}>{opt}</span>
                </label>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
```

---

## Task 22-M — Frontend: `PSIPage.tsx` 필터 확장

### 파일: `WebUI/src/pages/PSIPage.tsx`

**추가 import:**
```typescript
import { MultiSelectCombobox } from '../components/MultiSelectCombobox';
```

**추가 상태:**
```typescript
const [lineFilter, setLineFilter] = useState<string[]>([]);
const [modelFilter, setModelFilter] = useState<string[]>([]);
const [woFilter, setWoFilter] = useState<string[]>([]);
```

**filter-options 쿼리 추가:**
```typescript
const { data: filterOptions = { lines: [], models: [], work_orders: [] } } = useQuery({
  queryKey: ['psi-filter-options'],
  queryFn: async () => (await apiClient.get('/psi/filter-options')).data,
  staleTime: 30_000,
});
```

**matrix-v2 queryKey + params 수정:**
```typescript
queryKey: ['psi-matrix-v2', dateFrom, days, expeditorId, supplyType, lineFilter, modelFilter, woFilter],
queryFn: async () => {
  const params: Record<string, any> = { date_from: dateFrom, days };
  if (expeditorId !== 'all') params.expeditor_user_id = expeditorId;
  if (supplyType !== 'all') params.supply_type = supplyType;
  if (lineFilter.length > 0) params.line_codes = lineFilter;
  if (modelFilter.length > 0) params.model_numbers = modelFilter;
  if (woFilter.length > 0) params.wo_numbers = woFilter;
  const res = await apiClient.get('/psi/matrix-v2', {
    params,
    paramsSerializer: (p) => qs.stringify(p, { arrayFormat: 'repeat' }),
  });
  return res.data;
},
```

> `qs` 라이브러리 필요: `npm install qs && npm install -D @types/qs`

**필터 패널에 콤보박스 3개 추가 (Supply Type 필터 뒤에):**
```tsx
<MultiSelectCombobox label="Line" options={filterOptions.lines} selected={lineFilter} onChange={setLineFilter} placeholder="전체 라인" />
<MultiSelectCombobox label="Model.Suffix" options={filterOptions.models} selected={modelFilter} onChange={setModelFilter} placeholder="전체 모델" />
<MultiSelectCombobox label="Work Order" options={filterOptions.work_orders} selected={woFilter} onChange={setWoFilter} placeholder="전체 W/O" />
```

---

## Task 22-N — Frontend: `PSIMatrixV2.tsx` 고정 열 순서 변경

### 파일: `WebUI/src/components/psi/PSIMatrixV2.tsx`

**목표**: `품번, 품명, 재고, Level, 협력사, S/Type` → `협력사, 2차협력사, 품번, 품명, 재고`

**1. 너비 상수:**
```typescript
const FIXED_COLS_WIDTH = 512;
// 협력사(96) + 2차협력사(80) + 품번(128) + 품명(144) + 재고(64) = 512
```

**2. 헤더 Row 1:** `colSpan={6}` → `colSpan={5}`

**3. 헤더 Row 2 — 컬럼 배열 교체:**
```tsx
{([
  ['협력사',    'w-24',  0   ],
  ['2차협력사', 'w-20',  96  ],
  ['품번',      'w-32',  176 ],
  ['품명',      'w-36',  304 ],
  ['재고',      'w-16',  448 ],
] as [string, string, number][]).map(([label, w, leftPx]) => (
  <th key={label}
    className={`sticky bg-gray-100 px-2 py-1 text-[10px] font-black text-gray-500 uppercase border-b border-r ${w}`}
    style={{ left: leftPx, zIndex: 10 }}>
    {label}
  </th>
))}
```

**4. 헤더 Row 3:** `colSpan={6}` → `colSpan={5}`

**5. 바디 고정 컬럼 td 교체 (rowSpan=4, 소요량 tr 내부):**
```tsx
{/* 협력사 */}
<td className={`sticky ${evenBg} px-1 py-0 border-r border-b text-gray-500 truncate max-w-[96px]`}
    style={{ left: 0, zIndex: 5 }} rowSpan={4} title={row.vendor_primary ?? ''}>
  {row.vendor_primary}
</td>
{/* 2차협력사 */}
<td className={`sticky ${evenBg} px-1 py-0 border-r border-b text-gray-400 text-[10px] truncate max-w-[80px]`}
    style={{ left: 96, zIndex: 5 }} rowSpan={4} title={row.vendor_secondary ?? ''}>
  {row.vendor_secondary}
</td>
{/* 품번 */}
<td className={`sticky ${evenBg} px-2 py-0 border-r border-b font-mono font-bold text-blue-700 whitespace-nowrap`}
    style={{ left: 176, zIndex: 5 }} rowSpan={4}>
  {row.part_number}
</td>
{/* 품명 */}
<td className={`sticky ${evenBg} px-2 py-0 border-r border-b text-gray-600 truncate max-w-[144px]`}
    style={{ left: 304, zIndex: 5 }} rowSpan={4} title={row.description ?? ''}>
  {row.description}
</td>
{/* 재고 (클릭 편집) */}
<td className={`sticky ${evenBg} px-1 py-0 border-r border-b text-right font-black text-gray-700 cursor-pointer hover:bg-blue-50`}
    style={{ left: 448, zIndex: 5 }} rowSpan={4}
    onClick={() => setEditingInventory({ item_id: row.item_id, value: String(row.inventory_qty) })}>
  {isEdInventory ? (
    <input autoFocus type="number" value={editingInventory!.value}
      onChange={e => setEditingInventory({ ...editingInventory!, value: e.target.value })}
      onBlur={() => inventoryMutation.mutate({ item_id: row.item_id, inventory_qty: parseFloat(editingInventory!.value) || 0 })}
      onKeyDown={e => {
        if (e.key === 'Enter') inventoryMutation.mutate({ item_id: row.item_id, inventory_qty: parseFloat(editingInventory!.value) || 0 });
        if (e.key === 'Escape') setEditingInventory(null);
      }}
      className="w-14 text-right text-xs border-b border-blue-500 bg-blue-50 outline-none"
      onClick={e => e.stopPropagation()} />
  ) : (
    <span className="text-blue-600">{row.inventory_qty.toLocaleString()}</span>
  )}
</td>
```

> **삭제 대상**: 기존 `Level` td, `협력사` td, `S/Type` td (rowSpan=4 3개 셀) 완전 제거.

---

## 검증 체크리스트 (Task I~N)

```
[ ] 22-I: GET /psi/filter-options 응답에 lines, models, work_orders 배열 포함
[ ] 22-I: Target Batch 없을 때 빈 배열 반환
[ ] 22-J: line_codes 필터 적용 시 해당 라인의 품번만 표시
[ ] 22-J: model_numbers — "MODEL.SUFFIX" 및 "MODEL" 형식 모두 동작
[ ] 22-J: vendor_primary / vendor_secondary에 parse_vendor_name() 적용
[ ] 22-K: GET /psi/matrix-v2?line_codes=A&line_codes=B 정상 수신
[ ] 22-L: MultiSelectCombobox — 검색, 체크박스 선택, 외부 클릭 닫힘, Escape 닫힘
[ ] 22-M: PSIPage 필터 패널에 Line/Model.Suffix/WorkOrder 콤보박스 렌더링
[ ] 22-M: 필터 변경 시 매트릭스 재조회
[ ] 22-N: 고정 열 순서: 협력사 → 2차협력사 → 품번 → 품명 → 재고
[ ] 22-N: Level, S/Type 열 완전히 제거
[ ] 22-N: 스크롤 시 모든 고정 열 올바른 위치에서 freeze
[ ] npx tsc --noEmit 오류 0건
```

---

## 참고 — 현재 파일 상태

| 파일 | Task A~H 후 상태 | Task I~N 필요 작업 |
|---|---|---|
| `backend/services/psi_service.py` | build_psi_matrix_v2() 존재 | line/model/wo 필터 추가 |
| `backend/api/routes/psi.py` | /psi/matrix-v2 존재 | filter-options 엔드포인트 + 파라미터 확장 |
| `WebUI/src/components/MultiSelectCombobox.tsx` | 없음 | 신규 생성 |
| `WebUI/src/pages/PSIPage.tsx` | Expeditor/SupplyType 필터 존재 | 3개 콤보박스 추가 |
| `WebUI/src/components/psi/PSIMatrixV2.tsx` | 품번/품명/재고/Level/협력사/S-Type | 열 재배치 (FIXED_COLS_WIDTH=512) |
