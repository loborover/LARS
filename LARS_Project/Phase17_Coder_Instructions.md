# Phase 17 Coder Instructions — BOM Substitute Fix + BOM Amount View

**작성자**: Claude (Chief Architect)  
**대상**: Gemini (Coder)  
**날짜**: 2026-05-17  
**우선순위**: HIGH

---

## 개요

이번 Phase는 3개의 연관된 태스크로 구성된다.

| 태스크 | 범위 | 파일 |
|--------|------|------|
| 17-A | BOM 대체품(*S*) 렌더링 버그 수정 | `BOMTree.tsx` |
| 17-B | BOM Amount 산출 API 엔드포인트 신규 추가 | `bom_service.py`, `schemas/bom.py`, `routes/bom.py` |
| 17-C | BOMDetailPage 토글 + Amount View 컴포넌트 | `BOMDetailPage.tsx`, `BOMAmountView.tsx` (신규) |

DB 스키마 변경 없음 — Alembic migration 불필요. `level=-1` sentinel은 이미 DB에 저장되어 있고, `path` 끝의 `.S` suffix도 이미 존재한다.

---

## 배경 지식 (반드시 읽을 것)

### BomItem DB 구조
```
bom_items 테이블:
  id, model_id, level (int), part_number, description,
  qty (float), uom, vendor_raw, supply_type,
  path (materialized path), sort_order
```

### level 값의 의미
- `level = 0`: BOM 루트 (완제품 자체, qty=1). 예: `CBGJ3023D.ABDELNA@CVZ.EKHQ`
- `level = 1, 2, 3, ...`: 실제 부품 계층
- `level = -1`: 대체품 (*S*). 인접한 본부품의 대체 옵션임.

### path 구조
```
level=0  → path="0"
level=1  → path="0.1", "0.2", ...
level=2  → path="0.1.1", "0.1.2", ...
level=-1 → path="0.1.S", "0.2.S", ... (본부품 path + ".S")
```

### 실제 DB 샘플 (model=CBGJ3023D.ABDELNA)
```
sort=0 | lv=0  | path=0         | pn=CBGJ3023D.ABDELNA@CVZ.EKHQ | qty=1.0
sort=1 | lv=1  | path=0.1       | pn=ABQ30180703               | qty=1.0
sort=2 | lv=2  | path=0.1.1     | pn=ABQ30192603               | qty=1.0
sort=3 | lv=3  | path=0.1.1.1   | pn=MAM62585303               | qty=1.0
sort=4 | lv=3  | path=0.1.1.2   | pn=MAZ67707501               | qty=3.0
sort=5 | lv=4  | path=0.1.1.2.1 | pn=RAA31737276               | qty=0.1
sort=6 | lv=4  | path=0.1.1.2.2 | pn=RCL30078501               | qty=0.1
```

만약 `ABQ30180703`의 대체품이 있다면:
```
sort=? | lv=-1 | path=0.1.S     | pn=ALTPART001 | qty=1.0
```

---

## Task 17-A: BOMTree.tsx — 대체품 렌더링 버그 수정

**파일**: `/test/LARS/.WebUI/src/components/bom/BOMTree.tsx`

### 현재 버그 원인 (정밀 분석)

현재 `buildTree()`:
```ts
while (stack.length > 0 && stack[stack.length - 1].level >= item.level) {
  stack.pop();
}
```

`level=-1`인 item이 들어오면:
- `stack[top].level >= -1` 조건이 **모든 정상 레벨(0,1,2,...)**에 대해 true
- → 스택이 전부 비워짐
- → substitute가 `roots`에 push되거나 잘못된 부모의 자식이 됨
- → substitute가 stack에 push됨
- → 다음 정상 아이템(level=2 등)이 substitute의 자식으로 붙어버림

**결과**: 대체품이 트리 노드가 되어 하위 부품들을 자식으로 거느림. 완전히 잘못된 구조.

### 올바른 구조

대체품은 트리 계층의 노드가 아니라, 인접한 **본부품의 형제 수준 대체 옵션**이다.

```
[Root Level 0]
  └─ [Part A, level=1, path="0.1"]          ← 본부품
       ↳ [ALT-PART, level=-1, path="0.1.S"] ← 대체품 (A의 대체 옵션으로 표시)
       └─ [Sub-Part, level=2, path="0.1.1"] ← A의 실제 자식
```

### 구현 지시

#### Step 1: TreeNode 인터페이스 수정

```ts
interface TreeNode extends BOMItem {
  children: TreeNode[];
  substitutes: TreeNode[];  // ← 추가
}
```

#### Step 2: buildTree() 완전 교체

기존 buildTree를 아래 코드로 **완전히 교체**:

```ts
function buildTree(items: BOMItem[]): TreeNode[] {
  const roots: TreeNode[] = [];
  const stack: TreeNode[] = [];
  const pathToNode = new Map<string, TreeNode>();

  for (const item of items) {
    const node: TreeNode = { ...item, children: [], substitutes: [] };

    if (item.level === -1) {
      // 대체품: ".S"를 제거한 path로 본부품 탐색
      const primaryPath = item.path.replace(/\.S$/, '');
      const primaryNode = pathToNode.get(primaryPath);
      if (primaryNode) {
        primaryNode.substitutes.push(node);
      }
      // ← stack에 push하지 않는다. 절대로.
      continue;
    }

    pathToNode.set(item.path, node);

    if (item.level === 0) {
      roots.push(node);
      stack.length = 0;
      stack.push(node);
    } else {
      while (stack.length > 0 && stack[stack.length - 1].level >= item.level) {
        stack.pop();
      }
      if (stack.length > 0) {
        stack[stack.length - 1].children.push(node);
      } else {
        roots.push(node);
      }
      stack.push(node);
    }
  }

  return roots;
}
```

**핵심**: `level === -1` 분기에서 `continue`로 즉시 넘어가 stack 조작을 완전히 건너뜀.

#### Step 3: TreeRow 컴포넌트 수정

대체품 행 렌더링을 `substitutes` 배열에서 처리하도록 수정. `TreeRow` 내부에서 본부품 row 렌더 후, `node.substitutes.map(...)` 으로 대체품 rows를 즉시 이어서 출력:

```tsx
function TreeRow({
  node,
  collapsed,
  onToggle,
  depth,
}: {
  node: TreeNode;
  collapsed: Set<string>;
  onToggle: (path: string) => void;
  depth: number;
}) {
  const hasChildren = node.children.length > 0;
  const isCollapsed = collapsed.has(node.path);
  // isSubstitute는 여기서 항상 false (substitutes는 별도로 렌더됨)

  return (
    <>
      {/* 본부품 행 (기존과 동일) */}
      <tr className={`border-b transition-colors ${depth % 2 === 0 ? 'bg-white' : 'bg-gray-50/30'} hover:bg-blue-50/40`}>
        <td className="px-2 py-1.5 w-8 text-center border-r text-[10px] text-gray-400 font-mono">
          {node.level}
        </td>
        <td className="py-1.5 pr-3">
          <div className="flex items-center gap-1" style={{ paddingLeft: `${Math.max(0, depth) * 1.25}rem` }}>
            {hasChildren ? (
              <button
                onClick={() => onToggle(node.path)}
                className="w-4 h-4 flex items-center justify-center text-gray-400 hover:text-blue-600 shrink-0 transition-transform duration-200"
              >
                <span className="text-[10px] font-bold">{isCollapsed ? '▶' : '▼'}</span>
              </button>
            ) : (
              <span className="w-4 shrink-0" />
            )}
            <span className="font-mono text-xs text-gray-800">{node.part_number}</span>
          </div>
        </td>
        <td className="py-1.5 px-2 text-xs text-gray-600 max-w-xs truncate" title={node.description}>
          {node.description || '-'}
        </td>
        <td className="py-1.5 px-2 text-xs text-right text-gray-700 font-mono whitespace-nowrap">
          {node.qty.toLocaleString()} <span className="text-[10px] text-gray-400">{node.uom}</span>
        </td>
        <td className="py-1.5 px-2 text-xs text-center">
          {node.supply_type && (
            <span className="bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded text-[9px] font-medium uppercase">
              {node.supply_type}
            </span>
          )}
        </td>
        <td className="py-1.5 px-2 text-xs text-gray-500 max-w-[150px] truncate" title={node.vendor_raw}>
          {node.vendor_raw || '-'}
        </td>
      </tr>

      {/* 대체품 행들: 본부품 바로 아래, 동일한 들여쓰기 + 대체 badge */}
      {node.substitutes.map((sub) => (
        <tr key={`${sub.id}-${sub.path}`} className="border-b bg-yellow-50/60 hover:bg-yellow-100/50">
          <td className="px-2 py-1.5 w-8 text-center border-r text-[10px] text-yellow-600 font-mono font-bold">
            S
          </td>
          <td className="py-1.5 pr-3">
            <div className="flex items-center gap-1" style={{ paddingLeft: `${Math.max(0, depth) * 1.25 + 1.25}rem` }}>
              <span className="w-4 shrink-0" />
              <span className="font-mono text-xs text-yellow-800 font-semibold">{sub.part_number}</span>
              <span className="text-[8px] bg-yellow-200 text-yellow-800 px-1.5 py-0.5 rounded font-bold uppercase tracking-tighter ml-1">
                대체
              </span>
            </div>
          </td>
          <td className="py-1.5 px-2 text-xs text-yellow-700 max-w-xs truncate" title={sub.description}>
            {sub.description || '-'}
          </td>
          <td className="py-1.5 px-2 text-xs text-right text-yellow-700 font-mono whitespace-nowrap">
            {sub.qty.toLocaleString()} <span className="text-[10px] text-yellow-500">{sub.uom}</span>
          </td>
          <td className="py-1.5 px-2 text-xs text-center">
            {sub.supply_type && (
              <span className="bg-yellow-100 text-yellow-600 px-1.5 py-0.5 rounded text-[9px] font-medium uppercase">
                {sub.supply_type}
              </span>
            )}
          </td>
          <td className="py-1.5 px-2 text-xs text-yellow-600 max-w-[150px] truncate" title={sub.vendor_raw}>
            {sub.vendor_raw || '-'}
          </td>
        </tr>
      ))}

      {/* 자식 행들 (collapsed 상태일 때 숨김) */}
      {!isCollapsed && node.children.map((child) => (
        <TreeRow
          key={`${child.id}-${child.path}`}
          node={child}
          collapsed={collapsed}
          onToggle={onToggle}
          depth={depth + 1}
        />
      ))}
    </>
  );
}
```

**대체품 들여쓰기 규칙**: 본부품과 동일한 depth에서 시작하되, 추가로 1단계(1.25rem) 더 들여쓴다. 즉 `(depth * 1.25 + 1.25)rem`.

#### Step 4: FlatRow (검색 모드) 대체품 처리

FlatRow는 검색 결과를 flat하게 보여준다. 검색 시 `level=-1`인 항목도 포함될 수 있으므로 isSubstitute 처리 유지:

```tsx
function FlatRow({ item }: { item: BOMItem }) {
  const isSubstitute = item.level < 0;
  return (
    <tr className={`border-b hover:bg-blue-50/40 ${isSubstitute ? 'bg-yellow-50/60' : ''}`}>
      <td className="px-2 py-1.5 w-8 text-center border-r text-[10px] text-gray-400 font-mono">
        {isSubstitute ? <span className="text-yellow-600 font-bold">S</span> : item.level}
      </td>
      <td className="py-1.5 pr-3 pl-4">
        <div className="flex items-center gap-2">
          <span className={`font-mono text-xs ${isSubstitute ? 'text-yellow-800 font-semibold' : 'text-gray-800'}`}>
            {item.part_number}
          </span>
          {isSubstitute && (
            <span className="text-[8px] bg-yellow-200 text-yellow-800 px-1.5 py-0.5 rounded font-bold uppercase tracking-tighter">대체</span>
          )}
        </div>
      </td>
      <td className="py-1.5 px-2 text-xs text-gray-600 max-w-xs truncate">{item.description || '-'}</td>
      <td className="py-1.5 px-2 text-xs text-right text-gray-700 font-mono">{item.qty.toLocaleString()} {item.uom}</td>
      <td className="py-1.5 px-2 text-xs text-center">
        {item.supply_type && <span className="bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded text-[9px] uppercase">{item.supply_type}</span>}
      </td>
      <td className="py-1.5 px-2 text-xs text-gray-500 truncate max-w-[150px]">{item.vendor_raw || '-'}</td>
    </tr>
  );
}
```

#### Step 5: getInitialCollapsed 수정

`level=-1` 항목이 collapsed set에 들어가지 않도록 명시적으로 필터:

```ts
function getInitialCollapsed(items: BOMItem[]): Set<string> {
  const collapsed = new Set<string>();
  for (const item of items) {
    if (item.level >= 2) {  // level=-1(대체품)은 자동으로 제외됨
      collapsed.add(item.path);
    }
  }
  return collapsed;
}
```

---

## Task 17-B: BOM Amount API 신규 추가

### 알고리즘 설명

BOM Amount = 계층 구조를 고려한 실제 소요량 산출.

```
Root (level=0, qty=1) → 제품 1대 기준
  └─ Part A (level=1, qty=2) → 1대당 2개
       └─ Part B (level=2, qty=3) → A 1개당 3개 → 제품 1대당 2×3=6개
            └─ Part C (level=3, qty=4) → B 1개당 4개 → 제품 1대당 2×3×4=24개
```

대체품(level=-1)은 Amount 계산에서 **완전히 제외**한다. (대체품은 쓸 수도 있고 안 쓸 수도 있으므로 선택 사항)

같은 part_number가 BOM의 여러 위치에 나타날 경우 해당 위치들의 accumulated_qty를 **합산**한다.

### Step 1: schemas/bom.py에 신규 스키마 추가

파일 끝에 추가:

```python
class BomAmountItem(BaseModel):
    part_number: str
    description: Optional[str] = None
    uom: str
    total_qty: float
    vendor_raw: Optional[str] = None
    supply_type: Optional[str] = None
    occurrence_count: int  # BOM 내 중복 등장 횟수

class BomAmountResponse(BaseModel):
    model: BomModelRead
    items: List[BomAmountItem]
```

### Step 2: bom_service.py에 get_bom_amount() 추가

```python
async def get_bom_amount(session: AsyncSession, model_number: str) -> Optional[BomAmountResponse]:
    """
    BOM의 계층적 소요량을 산출한다.
    각 item의 accumulated_qty = item.qty × parent.qty × grandparent.qty × ... (루트 제외)
    동일 part_number는 합산하여 반환.
    """
    from schemas.bom import BomAmountItem, BomAmountResponse

    # 1. 모델 조회
    if "." in model_number:
        model_code, suffix = model_number.split(".", 1)
    else:
        model_code, suffix = model_number, ""

    stmt = select(BomModel).where(BomModel.model_code == model_code, BomModel.suffix == suffix)
    res = await session.execute(stmt)
    model = res.scalar_one_or_none()
    if not model:
        return None

    # 2. 전체 BomItem 조회 (sort_order 기준 정렬)
    stmt_items = select(BomItem).where(BomItem.model_id == model.id).order_by(BomItem.sort_order)
    res_items = await session.execute(stmt_items)
    items = res_items.scalars().all()

    # 3. path → qty 딕셔너리 구성 (level >= 0인 항목만, 대체품 level=-1 제외)
    path_to_qty: dict[str, float] = {}
    path_to_item: dict[str, BomItem] = {}
    for item in items:
        if item.level >= 0:
            path_to_qty[item.path] = item.qty
            path_to_item[item.path] = item

    # 4. 각 아이템의 accumulated_qty 계산 (level > 0만, 루트=0 및 대체품=-1 제외)
    # part_number → {total_qty, occurrence_count, metadata}
    aggregated: dict[str, dict] = {}

    for item in items:
        if item.level <= 0:
            continue  # 루트(0) 및 대체품(-1) 건너뜀

        path_parts = item.path.split('.')
        accumulated = item.qty

        # 조상 경로를 따라 올라가며 qty를 곱함
        # path_parts = ["0", "1", "2", "3"] 이면
        # 조상: "0.1" (i=2), "0.1.2" (i=3) → range(2, len(path_parts))
        # "0" (루트, i=1)은 제외 (qty=1이므로 곱해도 무방하나 명시적으로 제외)
        for i in range(2, len(path_parts)):
            ancestor_path = '.'.join(path_parts[:i])
            ancestor_qty = path_to_qty.get(ancestor_path, 1.0)
            accumulated *= ancestor_qty

        pn = item.part_number
        if pn not in aggregated:
            aggregated[pn] = {
                "total_qty": 0.0,
                "occurrence_count": 0,
                "description": item.description,
                "uom": item.uom,
                "vendor_raw": item.vendor_raw,
                "supply_type": item.supply_type,
            }
        aggregated[pn]["total_qty"] += accumulated
        aggregated[pn]["occurrence_count"] += 1

    # 5. 결과 정렬 (total_qty 내림차순)
    result_items = [
        BomAmountItem(
            part_number=pn,
            description=data["description"],
            uom=data["uom"],
            total_qty=round(data["total_qty"], 6),
            vendor_raw=data["vendor_raw"],
            supply_type=data["supply_type"],
            occurrence_count=data["occurrence_count"],
        )
        for pn, data in aggregated.items()
    ]
    result_items.sort(key=lambda x: x.total_qty, reverse=True)

    return BomAmountResponse(
        model=BomModelRead(
            id=model.id,
            model_code=model.model_code,
            suffix=model.suffix,
            description=model.description,
            version=model.version,
        ),
        items=result_items,
    )
```

### Step 3: routes/bom.py에 신규 엔드포인트 추가

**중요**: 기존 `GET /bom/models/{model_number:path}` 라우트와 URL이 충돌하지 않도록 **별도 경로** 사용.

`GET /bom/amount/{model_number:path}` 로 등록한다.

기존 `routes/bom.py`에 추가:

```python
from schemas.bom import BomModelRead, BomTreeResponse, ReverseResult, BomAmountResponse

@router.get("/amount/{model_number:path}", response_model=BomAmountResponse)
async def get_model_amount(
    model_number: str,
    session: AsyncSession = Depends(get_session)
):
    result = await bom_service.get_bom_amount(session, model_number)
    if not result:
        raise HTTPException(status_code=404, detail="Model not found")
    return result
```

**주의**: `BomAmountResponse` import를 반드시 추가할 것.

---

## Task 17-C: BOMDetailPage 토글 + Amount View 컴포넌트

### Step 1: BOMAmountView.tsx 신규 생성

**파일**: `/test/LARS/.WebUI/src/components/bom/BOMAmountView.tsx`

```tsx
import { useState, useMemo } from 'react';
import { Search } from 'lucide-react';

interface BomAmountItem {
  part_number: string;
  description?: string;
  uom: string;
  total_qty: number;
  vendor_raw?: string;
  supply_type?: string;
  occurrence_count: number;
}

interface BomAmountViewProps {
  items: BomAmountItem[];
}

export function BOMAmountView({ items }: BomAmountViewProps) {
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    if (!search.trim()) return items;
    const q = search.toLowerCase();
    return items.filter(
      (i) =>
        i.part_number.toLowerCase().includes(q) ||
        (i.description ?? '').toLowerCase().includes(q) ||
        (i.vendor_raw ?? '').toLowerCase().includes(q)
    );
  }, [items, search]);

  const grandTotal = filtered.reduce((s, i) => s + i.total_qty, 0);

  return (
    <div className="flex flex-col h-full">
      {/* 검색 바 */}
      <div className="flex items-center justify-between gap-4 mb-4 shrink-0">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" size={14} />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="품번, 품명, 협력사로 검색..."
            className="pl-9 pr-8 py-1.5 w-full border rounded-lg text-xs focus:outline-none focus:ring-2 focus:ring-blue-400 bg-white shadow-sm"
          />
          {search && (
            <button
              onClick={() => setSearch('')}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-300 hover:text-gray-600"
            >
              ✕
            </button>
          )}
        </div>
        <span className="text-xs text-gray-400 font-medium">
          {filtered.length}개 고유 품번 (대체품 제외)
        </span>
      </div>

      {/* 테이블 */}
      <div className="flex-1 overflow-auto border rounded-xl bg-white shadow-inner">
        <table className="w-full min-w-[700px] text-xs border-collapse relative">
          <thead className="sticky top-0 bg-gray-100 z-10 shadow-sm">
            <tr className="border-b">
              <th className="px-3 py-2 text-left text-[11px] font-bold text-gray-500 whitespace-nowrap">품번 (Part Number)</th>
              <th className="px-3 py-2 text-left text-[11px] font-bold text-gray-500">품명 (Description)</th>
              <th className="px-3 py-2 text-right text-[11px] font-bold text-gray-500 whitespace-nowrap">소요량</th>
              <th className="px-3 py-2 text-center text-[11px] font-bold text-gray-500">UOM</th>
              <th className="px-3 py-2 text-center text-[11px] font-bold text-gray-500 whitespace-nowrap">등장횟수</th>
              <th className="px-3 py-2 text-center text-[11px] font-bold text-gray-500">공급유형</th>
              <th className="px-3 py-2 text-left text-[11px] font-bold text-gray-500">협력사</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {filtered.map((item, idx) => (
              <tr key={item.part_number} className={`hover:bg-blue-50/40 ${idx % 2 === 0 ? 'bg-white' : 'bg-gray-50/30'}`}>
                <td className="px-3 py-1.5 font-mono font-semibold text-gray-800 whitespace-nowrap">{item.part_number}</td>
                <td className="px-3 py-1.5 text-gray-600 max-w-xs truncate" title={item.description}>
                  {item.description || '-'}
                </td>
                <td className="px-3 py-1.5 text-right font-bold text-gray-900 font-mono">
                  {item.total_qty % 1 === 0
                    ? item.total_qty.toLocaleString()
                    : item.total_qty.toFixed(4)}
                </td>
                <td className="px-3 py-1.5 text-center text-gray-500">{item.uom}</td>
                <td className="px-3 py-1.5 text-center">
                  {item.occurrence_count > 1 ? (
                    <span className="bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full text-[10px] font-bold">
                      ×{item.occurrence_count}
                    </span>
                  ) : (
                    <span className="text-gray-300">1</span>
                  )}
                </td>
                <td className="px-3 py-1.5 text-center">
                  {item.supply_type && (
                    <span className="bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded text-[9px] font-medium uppercase">
                      {item.supply_type}
                    </span>
                  )}
                </td>
                <td className="px-3 py-1.5 text-gray-500 max-w-[150px] truncate" title={item.vendor_raw}>
                  {item.vendor_raw || '-'}
                </td>
              </tr>
            ))}
            {filtered.length === 0 && (
              <tr>
                <td colSpan={7} className="text-center py-20 text-gray-300 text-sm">
                  검색 결과가 없습니다.
                </td>
              </tr>
            )}
          </tbody>
          {filtered.length > 0 && (
            <tfoot className="sticky bottom-0 bg-gray-900 text-white z-10">
              <tr className="font-bold">
                <td colSpan={2} className="px-3 py-2.5 text-right text-[10px] uppercase tracking-wider">
                  Grand Total ({filtered.length} Parts)
                </td>
                <td className="px-3 py-2.5 text-right font-mono text-sm text-blue-300">
                  {grandTotal % 1 === 0 ? grandTotal.toLocaleString() : grandTotal.toFixed(4)}
                </td>
                <td colSpan={4} />
              </tr>
            </tfoot>
          )}
        </table>
      </div>

      <div className="mt-3 text-[10px] text-gray-400 border-t pt-2 shrink-0">
        * 소요량은 제품 1대 기준. 동일 품번이 BOM 여러 위치에 사용될 경우 합산됨. 대체품(S)은 제외.
      </div>
    </div>
  );
}
```

### Step 2: BOMDetailPage.tsx 수정

**파일**: `/test/LARS/.WebUI/src/pages/BOMDetailPage.tsx`

변경 사항:

1. `BOMAmountView` import 추가
2. `viewMode: 'tree' | 'amount'` state 추가
3. Amount 데이터 쿼리 추가
4. 헤더에 토글 버튼 추가
5. 뷰 분기 렌더링

완전 수정된 파일:

```tsx
import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { Button } from '../../components/ui/button';
import { BOMTree } from '../components/bom/BOMTree';
import { BOMAmountView } from '../components/bom/BOMAmountView';
import { TutorialBox } from '../components/TutorialBox';
import { ArrowLeft, Search, GitBranch, List } from 'lucide-react';

export default function BOMDetailPage() {
  const { modelCode } = useParams();
  const navigate = useNavigate();
  const [reverseSearch, setReverseSearch] = useState('');
  const [reverseResult, setReverseResult] = useState<any>(null);
  const [viewMode, setViewMode] = useState<'tree' | 'amount'>('tree');

  // BOM Tree 데이터
  const { data, isLoading } = useQuery({
    queryKey: ['bom-detail', modelCode],
    queryFn: async () => {
      const res = await apiClient.get(`/bom/models/${encodeURIComponent(modelCode || '')}`);
      return res.data;
    },
    enabled: !!modelCode,
  });

  // BOM Amount 데이터 (Amount 뷰 선택 시 로딩)
  const { data: amountData, isLoading: amountLoading } = useQuery({
    queryKey: ['bom-amount', modelCode],
    queryFn: async () => {
      const res = await apiClient.get(`/bom/amount/${encodeURIComponent(modelCode || '')}`);
      return res.data;
    },
    enabled: !!modelCode && viewMode === 'amount',
  });

  const handleReverseLookup = async () => {
    if (!reverseSearch) return;
    try {
      const res = await apiClient.get('/bom/reverse', { params: { part_number: reverseSearch } });
      setReverseResult(res.data.models);
    } catch (e) {
      console.error(e);
      setReverseResult([]);
    }
  };

  return (
    <div className="flex flex-col h-full">
      {/* Sticky 머릿말 */}
      <div className="sticky top-0 z-20 bg-gray-50 pb-3 space-y-3">
        <div className="flex items-center justify-between gap-4">
          <div className="flex items-center gap-4">
            <Button variant="ghost" size="sm" onClick={() => navigate('/bom')} className="flex gap-1 items-center">
              <ArrowLeft size={16} /> 목록
            </Button>
            <h1 className="text-2xl font-bold">BOM 상세: {data?.model?.model_number || modelCode}</h1>
          </div>

          {/* 뷰 전환 토글 */}
          <div className="flex rounded-lg border border-gray-200 overflow-hidden text-[11px] font-bold shadow-sm shrink-0">
            <button
              onClick={() => setViewMode('tree')}
              className={`flex items-center gap-1.5 px-4 py-2 transition-all ${
                viewMode === 'tree'
                  ? 'bg-gray-800 text-white shadow-inner'
                  : 'bg-white text-gray-500 hover:bg-gray-50'
              }`}
            >
              <GitBranch size={13} />
              Tree View
            </button>
            <button
              onClick={() => setViewMode('amount')}
              className={`flex items-center gap-1.5 px-4 py-2 transition-all ${
                viewMode === 'amount'
                  ? 'bg-blue-600 text-white shadow-inner'
                  : 'bg-white text-gray-500 hover:bg-gray-50'
              }`}
            >
              <List size={13} />
              Amount View
            </button>
          </div>
        </div>

        <TutorialBox pageKey="bom-detail">
          <b>Tree View</b>: BOM 계층 구조 탐색. 대체품(S)은 본부품 바로 아래에 표시됩니다.
          <b>Amount View</b>: 계층 소요량 전개 후 품번별 합산 결과. DP와 조인하여 일일 자재소요량 계산 기준입니다.
        </TutorialBox>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="bg-white p-4 rounded-xl shadow-sm border">
            <h2 className="text-sm font-bold mb-3 flex items-center gap-2">
              <Search size={16} className="text-blue-500" /> 역조회 (Where-used)
            </h2>
            <div className="flex space-x-2">
              <input
                type="text"
                placeholder="부품번호 입력..."
                value={reverseSearch}
                onChange={(e) => setReverseSearch(e.target.value)}
                className="flex-1 px-3 py-1.5 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <Button size="sm" onClick={handleReverseLookup}>조회</Button>
            </div>
            {reverseResult && (
              <div className="mt-3 max-h-40 overflow-y-auto">
                <h3 className="text-xs font-semibold text-gray-500 mb-1">사용된 모델 ({reverseResult.length}건):</h3>
                <div className="flex flex-wrap gap-2">
                  {reverseResult.map((m: any) => (
                    <button
                      key={m.id}
                      className="px-2 py-1 bg-blue-50 text-blue-700 text-xs rounded hover:bg-blue-100 font-medium"
                      onClick={() => navigate(`/bom/${encodeURIComponent(m.model_number)}`)}
                    >
                      {m.model_number}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>

          <div className="bg-white p-4 rounded-xl shadow-sm border flex flex-col justify-center">
            <div className="text-xs text-gray-500 mb-1">모델 정보</div>
            <div className="flex items-baseline gap-2">
              <span className="text-xl font-bold text-gray-800">{data?.model?.model_code}</span>
              {data?.model?.suffix && (
                <>
                  <span className="text-gray-400">.</span>
                  <span className="text-xl font-bold text-blue-600">{data?.model?.suffix}</span>
                </>
              )}
            </div>
            <div className="text-sm text-gray-500 mt-1">{data?.model?.description || '-'}</div>
            <div className="flex gap-4 mt-2 text-xs text-gray-400">
              <span>Version: {data?.model?.version}</span>
              <span>총 {data?.items?.length ?? 0}개 부품</span>
              {amountData && (
                <span className="text-blue-500 font-semibold">
                  고유 품번 {amountData.items?.length ?? 0}개
                </span>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* 스크롤 콘텐츠 */}
      <div className="flex-1 min-h-0 mt-4 overflow-auto bg-white rounded-xl shadow-sm border p-6">
        {viewMode === 'tree' ? (
          isLoading ? (
            <div className="flex flex-col items-center justify-center py-20 gap-4">
              <div className="w-10 h-10 border-4 border-blue-600 border-t-transparent rounded-full animate-spin" />
              <p className="text-gray-500">BOM 구조 분석 중...</p>
            </div>
          ) : data ? (
            <BOMTree items={data.items} />
          ) : (
            <div className="py-20 text-center text-gray-400">모델을 찾을 수 없거나 데이터가 없습니다.</div>
          )
        ) : (
          amountLoading ? (
            <div className="flex flex-col items-center justify-center py-20 gap-4">
              <div className="w-10 h-10 border-4 border-blue-600 border-t-transparent rounded-full animate-spin" />
              <p className="text-gray-500">소요량 산출 중...</p>
            </div>
          ) : amountData ? (
            <BOMAmountView items={amountData.items} />
          ) : (
            <div className="py-20 text-center text-gray-400">소요량 데이터를 불러올 수 없습니다.</div>
          )
        )}
      </div>
    </div>
  );
}
```

---

## 실행 순서 (Gemini 준수 필수)

```
1. backend/schemas/bom.py     → BomAmountItem, BomAmountResponse 추가
2. backend/services/bom_service.py → get_bom_amount() 추가
3. backend/api/routes/bom.py  → /amount/{model_number:path} 라우트 추가
4. 백엔드 재시작 (uvicorn kill + restart)
5. curl로 API 동작 확인:
   curl "http://localhost:8000/api/bom/amount/CBGJ3023D.ABDELNA" -H "Authorization: Bearer <token>"
   → items 배열에 accumulated qty가 올바르게 계산되었는지 확인
6. .WebUI/src/components/bom/BOMTree.tsx → buildTree 교체, TreeRow 수정, TreeNode interface 수정
7. .WebUI/src/components/bom/BOMAmountView.tsx → 신규 파일 생성
8. .WebUI/src/pages/BOMDetailPage.tsx → 토글 + Amount 쿼리 + 분기 렌더링
9. npm run build
10. Vite preview 재시작 (port 3000)
11. 브라우저에서 BOM 상세 페이지 확인:
    - Tree View: 대체품이 본부품 바로 아래 대체(S) 배지로 표시되는지
    - Amount View: 품번별 합산 소요량 테이블 표시되는지
    - 토글 전환 정상 동작 확인
```

---

## 검증 기준 (Acceptance Criteria)

### 17-A 검증
- [ ] BOM Tree에서 level=-1 항목이 트리 노드(부모)로 표시되지 않음
- [ ] 대체품 항목이 본부품 바로 아래 들여쓰기로 표시되며 "대체" 배지 있음
- [ ] 대체품 아래에 본부품의 자식들이 정상 표시됨 (대체품이 자식을 가로채지 않음)
- [ ] 검색(FlatRow) 모드에서도 대체품이 황색 배경 + 대체 배지로 표시됨

### 17-B 검증
- [ ] `GET /api/bom/amount/{model_number}` 200 응답
- [ ] level=0 루트 파트 (`@CVZ.EKHQ` 포함 항목) 결과에 미포함
- [ ] level=-1 대체품 결과에 미포함
- [ ] 동일 part_number 여러 위치에 등장 시 total_qty 합산되고 occurrence_count > 1
- [ ] 계층 qty 곱셈 정확성: level-3 item qty=4, parent qty=3, grandparent qty=2 이면 total=24

### 17-C 검증
- [ ] BOM 상세 페이지 우상단에 [Tree View] [Amount View] 토글 버튼 표시
- [ ] Tree View ↔ Amount View 전환 정상 동작
- [ ] Amount View: 품번 | 품명 | 소요량 | UOM | 등장횟수 | 공급유형 | 협력사 열 표시
- [ ] Amount View 검색 필터 동작
- [ ] Amount View Grand Total 행 표시

---

## 주의사항

1. **DB 변경 없음**: Alembic migration 생성 금지. `level=-1` sentinel로 충분함.
2. **기존 `/bom/models/{model_number:path}` 라우트 수정 금지**: 별도 `/bom/amount/{model_number:path}` 경로 사용.
3. **Amount 계산에서 루트(level=0) 제외**: `if item.level <= 0: continue` 로직 확인.
4. **대체품 qty 미포함**: 대체품은 qty 합산에 넣지 않음. `level=-1` 체크로 이미 처리됨.
5. **백엔드 코드 변경 후 반드시 uvicorn 재시작**: `--reload` 없이 실행 중이므로 kill + restart 필수.
6. **프론트엔드 변경 후 반드시 `npm run build` + vite preview 재시작**.
7. **BOMAmountView import path 확인**: 컴포넌트가 `src/components/bom/BOMAmountView.tsx`에 있으므로 `BOMDetailPage.tsx`에서 `'../components/bom/BOMAmountView'` 로 import. (pages 폴더에서 components로 상대경로)
