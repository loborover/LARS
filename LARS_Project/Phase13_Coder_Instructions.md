# Phase 13 Coder Instructions — ItemMaster 구매품/사내품 분리 + 전 문서 컬럼 필터

**Role:** Coder (Gemini)  
**Date:** 2026-05-16  
**Priority:** High

---

## 목표

1. **ItemMaster**: Vendor 유무로 구매품 / 사내생산품 탭 분리, 기본 = 구매품
2. **재사용 컬럼 필터 시스템**: 모든 테이블에 컬럼별 필터 기능 추가

---

## Task 13-A: 재사용 컬럼 필터 시스템 구축

### 13-A-1. `useColumnFilter` 훅 생성

파일: `.WebUI/src/hooks/useColumnFilter.ts`

```ts
import { useState, useMemo } from 'react';

export function useColumnFilter<T extends Record<string, any>>(data: T[]) {
  const [filters, setFilters] = useState<Record<string, string>>({});
  const [openInputs, setOpenInputs] = useState<Set<string>>(new Set());

  const filtered = useMemo(() => {
    const activeEntries = Object.entries(filters).filter(([, v]) => v.trim() !== '');
    if (activeEntries.length === 0) return data;
    return data.filter(row =>
      activeEntries.every(([key, value]) => {
        const cellVal = String(row[key] ?? '').toLowerCase();
        return cellVal.includes(value.toLowerCase());
      })
    );
  }, [data, filters]);

  const setFilter = (field: string, value: string) =>
    setFilters(prev => ({ ...prev, [field]: value }));

  const toggleInput = (field: string) => {
    setOpenInputs(prev => {
      const next = new Set(prev);
      if (next.has(field)) {
        next.delete(field);
        setFilters(prev2 => { const n = { ...prev2 }; delete n[field]; return n; });
      } else {
        next.add(field);
      }
      return next;
    });
  };

  const clearAll = () => { setFilters({}); setOpenInputs(new Set()); };

  const isOpen = (field: string) => openInputs.has(field);
  const isActive = (field: string) => !!filters[field]?.trim();
  const hasAnyFilter = Object.values(filters).some(v => v.trim() !== '');

  return { filtered, filters, setFilter, toggleInput, clearAll, isOpen, isActive, hasAnyFilter };
}
```

### 13-A-2. `FilterableHeader` 컴포넌트 생성

파일: `.WebUI/src/components/FilterableHeader.tsx`

```tsx
import { Filter } from 'lucide-react';

interface Props {
  label: string;
  field: string;
  filters: Record<string, string>;
  isOpen: (field: string) => boolean;
  isActive: (field: string) => boolean;
  onToggle: (field: string) => void;
  onFilter: (field: string, value: string) => void;
  align?: 'left' | 'right' | 'center';
  className?: string;
}

export function FilterableHeader({
  label, field, filters, isOpen, isActive, onToggle, onFilter, align = 'left', className = ''
}: Props) {
  return (
    <th className={`px-3 py-2 font-semibold text-gray-600 text-xs bg-gray-50 ${className}`}>
      <div className={`flex items-center gap-1 ${align === 'right' ? 'justify-end' : align === 'center' ? 'justify-center' : ''}`}>
        <span>{label}</span>
        <button
          onClick={() => onToggle(field)}
          className={`p-0.5 rounded transition-colors ${
            isActive(field)
              ? 'text-blue-600 bg-blue-100'
              : 'text-gray-300 hover:text-gray-500'
          }`}
          title={isActive(field) ? `필터 활성: "${filters[field]}"` : '필터'}
        >
          <Filter size={11} />
        </button>
      </div>
      {isOpen(field) && (
        <div className="mt-1">
          <input
            autoFocus
            type="text"
            value={filters[field] ?? ''}
            onChange={e => onFilter(field, e.target.value)}
            placeholder="필터..."
            className="w-full px-1.5 py-0.5 text-xs border border-blue-300 rounded focus:outline-none focus:ring-1 focus:ring-blue-400 font-normal text-gray-700 bg-white"
            onClick={e => e.stopPropagation()}
          />
        </div>
      )}
    </th>
  );
}
```

**사용 패턴:**
```tsx
import { useColumnFilter } from '../hooks/useColumnFilter';
import { FilterableHeader } from '../components/FilterableHeader';

// 컴포넌트 내부
const { filtered, filters, setFilter, toggleInput, clearAll, isOpen, isActive, hasAnyFilter } =
  useColumnFilter(items ?? []);

// 테이블 헤더
<FilterableHeader
  label="품번"
  field="part_number"
  filters={filters}
  isOpen={isOpen}
  isActive={isActive}
  onToggle={toggleInput}
  onFilter={setFilter}
/>

// 필터 초기화 버튼 (hasAnyFilter 시 표시)
{hasAnyFilter && (
  <button onClick={clearAll} className="text-xs text-red-500 hover:underline">
    필터 초기화
  </button>
)}
```

---

## Task 13-B: ItemMaster 페이지 개선

### 변경 내용: `ItemMasterPage.tsx`

#### 1. Vendor 탭 추가

```tsx
type VendorTab = 'purchased' | 'inhouse';
const [vendorTab, setVendorTab] = useState<VendorTab>('purchased');
```

API는 `is_active=true` 전체를 가져오고, 프론트에서 탭 기준으로 분기:
```tsx
// vendor_raw가 있으면 구매품, 없으면 사내생산품
const vendorFiltered = (items ?? []).filter(item =>
  vendorTab === 'purchased' ? !!item.vendor_raw : !item.vendor_raw
);
```

탭 UI (sticky 머릿말 내 검색바 위에 위치):
```tsx
<div className="flex bg-white rounded-lg shadow-sm border p-1 w-fit">
  <button
    onClick={() => setVendorTab('purchased')}
    className={`px-4 py-1.5 rounded-md text-sm font-semibold transition-all ${
      vendorTab === 'purchased'
        ? 'bg-blue-600 text-white shadow'
        : 'text-gray-500 hover:bg-gray-100'
    }`}
  >
    구매품 <span className="text-xs opacity-70">(Vendor 있음)</span>
  </button>
  <button
    onClick={() => setVendorTab('inhouse')}
    className={`px-4 py-1.5 rounded-md text-sm font-semibold transition-all ${
      vendorTab === 'inhouse'
        ? 'bg-gray-700 text-white shadow'
        : 'text-gray-500 hover:bg-gray-100'
    }`}
  >
    사내생산품 <span className="text-xs opacity-70">(Vendor 없음)</span>
  </button>
</div>
```

#### 2. 컬럼 필터 적용

`useColumnFilter`를 `vendorFiltered` 배열에 적용:
```tsx
const { filtered: displayItems, filters, setFilter, toggleInput, clearAll, isOpen, isActive, hasAnyFilter } =
  useColumnFilter(vendorFiltered);
```

필터 대상 컬럼: `part_number`, `description`, `vendor_name`, `level`

테이블 헤더를 `FilterableHeader`로 교체:
```tsx
<thead>
  <tr>
    <FilterableHeader label="Level"  field="level"       {...filterProps} />
    <FilterableHeader label="품번"   field="part_number" {...filterProps} />
    <FilterableHeader label="품명"   field="description" {...filterProps} />
    <FilterableHeader label="협력사" field="vendor_name" {...filterProps} />
    <th>상태</th>
    <th>조회</th>
  </tr>
</thead>
```

> `filterProps = { filters, isOpen, isActive, onToggle: toggleInput, onFilter: setFilter }`

#### 3. 사내생산품 탭 안내 문구

```tsx
{vendorTab === 'inhouse' && (
  <div className="bg-yellow-50 border border-yellow-200 rounded p-2 text-xs text-yellow-800 mb-2">
    사내생산품은 Vendor 정보가 없는 품목입니다. 일반적으로 추적 관리가 필요하지 않습니다.
  </div>
)}
```

#### 4. 헤더 카운트 표시

탭 옆에 현재 탭의 아이템 수 표시:
```tsx
<span className="text-xs text-gray-400 ml-2">
  {vendorFiltered.length}건 / 표시 {displayItems.length}건
</span>
```

---

## Task 13-C: PartListPage 컬럼 필터 적용

`PartListPage.tsx`의 테이블에 컬럼 필터 추가.

PartList 데이터 구조를 확인한 후 (`/pl` API 응답의 `items` 배열 기준) 다음 컬럼에 적용:
- `part_number` (품번)
- `description` (품명)
- `vendor_name` 또는 `vendor_raw` (협력사)
- `supply_type` (공급유형)

```tsx
const { filtered, ...filterProps } = useColumnFilter(data?.items ?? []);
```

필터 초기화 버튼을 헤더 우측에 표시.

---

## Task 13-D: DailyPlanPage 컬럼 필터 적용

`DailyPlanPage.tsx`의 flat 테이블에 컬럼 필터 추가.

`lots` 배열에 `useColumnFilter` 적용. 필터 대상:
- `wo_number`
- `model_number`

날짜 컬럼(동적 생성)은 필터 제외 (데이터 타입이 number라 텍스트 검색 불필요).

```tsx
const { filtered: filteredLots, ...filterProps } = useColumnFilter(lots);
const dateColumns = Array.from(
  new Set(filteredLots.flatMap(l => Object.keys(l.daily_qty)))
).sort();
```

---

## Task 13-E: BOMTree 컬럼 필터 적용

`BOMTree.tsx`에 flat 검색 필터 추가 (트리 필터는 UX 복잡도가 높으므로 flat 검색으로 구현).

트리 필터 동작 방식:
- 필터 입력 시 → 트리를 무시하고 flat items 배열에서 매칭 행만 표시 (들여쓰기 유지)
- 필터 해제 시 → 다시 트리 구조 표시

```tsx
const [flatSearch, setFlatSearch] = useState('');

// flatSearch가 있으면 flat 표시, 없으면 tree 표시
const showFlat = flatSearch.trim() !== '';
const flatFiltered = showFlat
  ? items.filter(i =>
      i.part_number.toLowerCase().includes(flatSearch.toLowerCase()) ||
      (i.description ?? '').toLowerCase().includes(flatSearch.toLowerCase()) ||
      (i.vendor_raw ?? '').toLowerCase().includes(flatSearch.toLowerCase())
    )
  : [];
```

BOMTree 컴포넌트 상단에 검색 바 추가:
```tsx
<div className="flex items-center gap-2 mb-3">
  <div className="relative flex-1 max-w-sm">
    <Search className="absolute left-2 top-1/2 -translate-y-1/2 text-gray-400" size={14} />
    <input
      type="text"
      value={flatSearch}
      onChange={e => setFlatSearch(e.target.value)}
      placeholder="품번 / 품명 / 협력사 검색..."
      className="pl-8 pr-3 py-1.5 w-full border rounded text-xs focus:outline-none focus:ring-2 focus:ring-blue-400"
    />
    {flatSearch && (
      <button onClick={() => setFlatSearch('')} className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600">✕</button>
    )}
  </div>
  {showFlat && (
    <span className="text-xs text-gray-400">{flatFiltered.length}건 매칭</span>
  )}
  {/* 기존 전체 펼치기/접기 버튼 유지 (showFlat=false 일 때만 표시) */}
  {!showFlat && (
    <div className="flex gap-2 ml-auto">
      <button onClick={expandAll} className="text-xs text-blue-600 hover:underline px-2 py-1 border border-blue-200 rounded">전체 펼치기</button>
      <button onClick={collapseAll} className="text-xs text-gray-600 hover:underline px-2 py-1 border border-gray-200 rounded">전체 접기</button>
    </div>
  )}
</div>

{/* 테이블 */}
<table ...>
  <tbody>
    {showFlat
      ? flatFiltered.map(item => <FlatRow key={item.id} item={item} />)
      : tree.map(root => <TreeRow ... />)
    }
  </tbody>
</table>
```

`FlatRow`는 기존 `TreeRow`와 동일 구조지만 들여쓰기를 `item.level`로 직접 계산.

---

## Task 13-F: BOMListPage 컬럼 필터

BOMListPage는 이미 상단 검색이 있으므로, 테이블 내 컬럼 필터 대신 **그룹 헤더 아래 컬럼 필터 행**을 추가:

- `모델 번호` 열: 이미 상단 검색으로 커버됨 → 생략
- `설명(description)` 열: 컬럼 필터 추가
- `버전(version)` 열: 컬럼 필터 추가

`useColumnFilter`를 `groups` 배열이 아닌 `models` 배열에 적용하고 결과로 `groupModels()`:
```tsx
const { filtered: filteredModels, ...filterProps } = useColumnFilter(models ?? []);
const groups = groupModels(filteredModels);
```

---

## 검증 체크리스트

### 13-B (ItemMaster 탭)
- [ ] "구매품 (Vendor 있음)" 탭 기본 활성 상태
- [ ] "사내생산품 (Vendor 없음)" 탭으로 전환 시 vendor 없는 아이템만 표시
- [ ] 탭 전환 시 컬럼 필터 초기화
- [ ] 각 탭 아이템 수 표시

### 13-A + C~F (컬럼 필터)
- [ ] Filter 아이콘 클릭 시 컬럼 헤더 아래 인풋 표시
- [ ] 인풋 값 변경 시 해당 컬럼 기준으로 즉시 필터링 (useMemo)
- [ ] 여러 컬럼 동시 필터 가능
- [ ] 필터 활성 시 Filter 아이콘 파란색으로 강조
- [ ] "필터 초기화" 버튼으로 전체 해제
- [ ] BOMTree: 검색 입력 시 flat 모드로 전환, 해제 시 트리 복구
- [ ] PartListPage, DailyPlanPage 컬럼 필터 동작
- [ ] `npm run build` TypeScript 오류 0건

---

## 완료 후

- `Phase13_Coder_Report.md` 작성
- `npm run build` 확인
- Git commit: `"Phase 13: ItemMaster vendor tab + universal column filter"`
