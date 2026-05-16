# Phase 12 Coder Instructions — BOM List 그룹핑 + 트리 뷰어 개선

**Role:** Coder (Gemini)  
**Date:** 2026-05-16  
**Priority:** High  
**전제**: Phase 9-A (BOM model + suffix DB 반영) 완료 상태

---

## 배경 및 목표

- **ModelNumber = Model + Suffix** (예: `LSGL6335X.ASTLLGA`)
- 같은 Model이라도 Suffix가 다르면 다른 Buyer를 위한 별도 BOM이다
- **BOM 목록**: `model_code` 기준으로 그룹핑, 각 suffix를 접을 수 있는 하위 트리로 표시
- **BOM 뷰어**: level 필드 기반 접고 펼 수 있는 인터랙티브 트리로 전면 개선

---

## Task 12-A: BOM 목록 — Model 그룹 + Suffix 접기/펼치기

### 현재 상태
`BOMListPage.tsx`가 모델을 flat 리스트로 표시. `model_number` 컬럼 값은 올바르나 구조가 없다.

### 목표 UI

```
▼ LSGL6335X                           (2 variants) ← 클릭하면 접기
     LSGL6335X.ASTLLGA   설명   v1.0  [상세보기]
     LSGL6335X.ARSELGA   설명   v1.0  [상세보기]

▶ LSGL4850X                           (3 variants) ← 클릭하면 펼치기
   (접혀 있음)

  LG6700                              (suffix 없음) ← 단일이면 바로 행 표시
     설명   v1.0  [상세보기]
```

### 구현: `BOMListPage.tsx` 수정

#### 1. API 데이터 그룹핑 (프론트엔드에서)

```tsx
interface BomModel {
  id: number;
  model_code: string;
  suffix: string;
  model_number: string;   // computed: model_code.suffix or model_code
  description: string | null;
  version: string;
}

interface ModelGroup {
  model_code: string;
  variants: BomModel[];
}

// 그룹핑 함수
function groupModels(models: BomModel[]): ModelGroup[] {
  const map = new Map<string, BomModel[]>();
  for (const m of models) {
    const arr = map.get(m.model_code) ?? [];
    arr.push(m);
    map.set(m.model_code, arr);
  }
  return Array.from(map.entries())
    .map(([model_code, variants]) => ({ model_code, variants }))
    .sort((a, b) => a.model_code.localeCompare(b.model_code));
}
```

#### 2. 접기/펼치기 상태

```tsx
const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(new Set());

const toggleGroup = (model_code: string) => {
  setCollapsedGroups(prev => {
    const next = new Set(prev);
    if (next.has(model_code)) next.delete(model_code);
    else next.add(model_code);
    return next;
  });
};
```

#### 3. 렌더링

검색 초기화 시: variants가 1개이고 suffix가 없는 그룹은 펼침 상태 기본값 (접지 않음).  
variants가 2개 이상인 그룹은 기본 펼침 상태로 시작 (전부 다 보임 — 사용자가 원하면 접음).

```tsx
const groups = groupModels(models ?? []);

return (
  <div className="bg-white rounded-xl shadow-sm border overflow-hidden">
    <div className="max-h-[calc(100vh-250px)] overflow-auto">
      <table className="w-full text-sm">
        <thead className="sticky top-0 bg-gray-50 z-10 border-b">
          <tr>
            <th className="px-4 py-2.5 text-left font-semibold text-gray-600">모델 번호</th>
            <th className="px-4 py-2.5 text-left font-semibold text-gray-600">설명</th>
            <th className="px-4 py-2.5 text-left font-semibold text-gray-600">버전</th>
            <th className="px-4 py-2.5 text-center font-semibold text-gray-600">작업</th>
          </tr>
        </thead>
        <tbody>
          {groups.map((group) => {
            const isMulti = group.variants.length > 1 || group.variants.some(v => v.suffix);
            const isCollapsed = collapsedGroups.has(group.model_code);

            return (
              <>
                {/* 그룹 헤더 행 (variants가 1개 + suffix 없으면 생략, 바로 variant 표시) */}
                {isMulti && (
                  <tr
                    key={`group-${group.model_code}`}
                    onClick={() => toggleGroup(group.model_code)}
                    className="bg-gray-50 cursor-pointer hover:bg-blue-50 select-none border-b"
                  >
                    <td className="px-4 py-2 font-bold text-gray-800 flex items-center gap-2">
                      <span className="text-gray-400 text-xs">
                        {isCollapsed ? '▶' : '▼'}
                      </span>
                      <span>{group.model_code}</span>
                      <span className="text-xs text-gray-400 font-normal">
                        ({group.variants.length} variants)
                      </span>
                    </td>
                    <td colSpan={3} />
                  </tr>
                )}

                {/* Variant 행들 */}
                {!isCollapsed && group.variants.map((model) => (
                  <tr
                    key={model.id}
                    className="hover:bg-blue-50/50 transition-colors border-b"
                  >
                    <td className={`px-4 py-2 font-medium text-blue-600 ${isMulti ? 'pl-10' : ''}`}>
                      {isMulti ? (
                        // 그룹 내에서는 suffix만 강조 표시
                        <span>
                          <span className="text-gray-400">{model.model_code}</span>
                          {model.suffix && (
                            <span className="font-bold text-blue-700">.{model.suffix}</span>
                          )}
                        </span>
                      ) : (
                        model.model_number
                      )}
                    </td>
                    <td className="px-4 py-2 text-gray-600">{model.description || '-'}</td>
                    <td className="px-4 py-2 text-gray-500 text-xs">{model.version}</td>
                    <td className="px-4 py-2 text-center">
                      <button
                        onClick={() => navigate(`/bom/${encodeURIComponent(model.model_number)}`)}
                        className="text-xs text-blue-600 hover:underline px-2 py-1 rounded hover:bg-blue-50"
                      >
                        상세보기
                      </button>
                    </td>
                  </tr>
                ))}
              </>
            );
          })}

          {groups.length === 0 && (
            <tr>
              <td colSpan={4} className="text-center py-20 text-gray-400">
                검색 결과가 없습니다. BOM을 Import 하세요.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  </div>
);
```

---

## Task 12-B: BOM 트리 뷰어 — 접기/펼치기 인터랙티브 트리

### 현재 상태
`BOMTree.tsx`는 flat 테이블에 `paddingLeft`로 시각적 들여쓰기만 할 뿐 접기/펼치기 없음.

### 목표 UI

```
▼ [0] LSGL6335X         Complete Set    1 EA
  ▼ [1] PCB-MAIN        메인 기판       1 EA   서브원
      ▶ [2] IC-001      제어 IC         2 EA   (접혀있음 → 클릭하면 펼침)
      ▶ [2] R-100       저항 100Ω      10 EA
  ▼ [1] CABLE-01        전원 케이블     1 EA
      [2] CONNECTOR-A   커넥터          2 EA   대원
      [2] WIRE-RED      빨간 전선       0.5 M
  ▶ [1] HOUSING         하우징          1 EA   (접혀있음)
```

- 자식이 있는 노드: ▶/▼ 토글 버튼
- 자식이 없는 노드: 들여쓰기만, 버튼 없음
- 기본 상태: level 0, 1 펼침 / level 2 이상 접힘

### 12-B-1. 트리 구조 빌드 함수

flat item 배열 → 트리 노드 구조:

```tsx
interface BOMItem {
  id: number;
  level: number;
  part_number: string;
  description?: string;
  qty: number;
  uom: string;
  vendor_raw?: string;
  supply_type?: string;
  path: string;
}

interface TreeNode extends BOMItem {
  children: TreeNode[];
}

function buildTree(items: BOMItem[]): TreeNode[] {
  // items는 sort_order 기준 pre-order traversal 순서로 정렬되어 있다
  // stack 기반으로 부모-자식 관계 구성
  const roots: TreeNode[] = [];
  const stack: TreeNode[] = [];  // 현재 ancestor chain

  for (const item of items) {
    const node: TreeNode = { ...item, children: [] };

    if (item.level === 0) {
      roots.push(node);
      stack.length = 0;
      stack.push(node);
    } else {
      // 현재 레벨보다 깊은 스택 항목 제거
      while (stack.length > 0 && stack[stack.length - 1].level >= item.level) {
        stack.pop();
      }
      if (stack.length > 0) {
        stack[stack.length - 1].children.push(node);
      } else {
        roots.push(node);  // 부모 못 찾으면 root로
      }
      stack.push(node);
    }
  }

  return roots;
}
```

### 12-B-2. 초기 접힘 상태 계산

```tsx
function getInitialCollapsed(items: BOMItem[]): Set<string> {
  // level >= 2 인 노드의 path를 기본 접힘 상태로
  const collapsed = new Set<string>();
  for (const item of items) {
    if (item.level >= 2) {
      collapsed.add(item.path);
    }
  }
  return collapsed;
}
```

### 12-B-3. BOMTree 컴포넌트 전면 교체

파일: `.WebUI/src/components/bom/BOMTree.tsx`

```tsx
import { useState } from 'react';

// BOMItem, TreeNode interface 정의 (위 참조)
// buildTree, getInitialCollapsed 함수 포함

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
  const isSubstitute = node.level < 0;

  return (
    <>
      <tr
        className={`border-b transition-colors ${
          isSubstitute ? 'bg-yellow-50/50' : depth % 2 === 0 ? 'bg-white' : 'bg-gray-50/30'
        } hover:bg-blue-50/40`}
      >
        {/* 레벨/토글 */}
        <td className="px-2 py-1.5 w-8 text-center">
          <span className="text-xs text-gray-400 font-mono">
            {isSubstitute ? 'S' : node.level}
          </span>
        </td>

        {/* 품번 (들여쓰기 + 토글) */}
        <td className="py-1.5 pr-3">
          <div
            className="flex items-center gap-1"
            style={{ paddingLeft: `${Math.max(0, depth) * 1.25}rem` }}
          >
            {hasChildren ? (
              <button
                onClick={() => onToggle(node.path)}
                className="w-4 h-4 flex items-center justify-center text-gray-500 hover:text-blue-600 shrink-0"
              >
                <span className="text-[10px]">{isCollapsed ? '▶' : '▼'}</span>
              </button>
            ) : (
              <span className="w-4 shrink-0" /> /* 정렬용 spacer */
            )}
            <span className={`font-mono text-xs ${isSubstitute ? 'text-yellow-700' : 'text-gray-800'}`}>
              {node.part_number}
            </span>
            {isSubstitute && (
              <span className="text-[9px] bg-yellow-200 text-yellow-800 px-1 rounded">대체</span>
            )}
          </div>
        </td>

        {/* 품명 */}
        <td className="py-1.5 px-2 text-xs text-gray-600 max-w-xs truncate">
          {node.description || '-'}
        </td>

        {/* 수량 */}
        <td className="py-1.5 px-2 text-xs text-right text-gray-700 whitespace-nowrap">
          {node.qty} {node.uom}
        </td>

        {/* 공급유형 */}
        <td className="py-1.5 px-2 text-xs text-center">
          {node.supply_type && (
            <span className="bg-gray-100 text-gray-600 px-1.5 py-0.5 rounded text-[10px]">
              {node.supply_type}
            </span>
          )}
        </td>

        {/* 업체 */}
        <td className="py-1.5 px-2 text-xs text-gray-500 max-w-[120px] truncate">
          {node.vendor_raw || '-'}
        </td>
      </tr>

      {/* 자식 노드들 (접힘 상태가 아닐 때만 렌더링) */}
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

export function BOMTree({ items }: { items: BOMItem[] }) {
  const tree = buildTree(items);
  const [collapsed, setCollapsed] = useState<Set<string>>(() => getInitialCollapsed(items));

  const onToggle = (path: string) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(path)) next.delete(path);
      else next.add(path);
      return next;
    });
  };

  const expandAll = () => setCollapsed(new Set());
  const collapseAll = () => {
    const allPaths = new Set(items.filter(i => i.level > 0).map(i => i.path));
    setCollapsed(allPaths);
  };

  return (
    <div>
      {/* 전체 펼치기/접기 컨트롤 */}
      <div className="flex justify-end gap-2 mb-3">
        <button
          onClick={expandAll}
          className="text-xs text-blue-600 hover:underline px-2 py-1 border border-blue-200 rounded hover:bg-blue-50"
        >
          전체 펼치기
        </button>
        <button
          onClick={collapseAll}
          className="text-xs text-gray-600 hover:underline px-2 py-1 border border-gray-200 rounded hover:bg-gray-50"
        >
          전체 접기
        </button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[700px] text-sm">
          <thead className="sticky top-0 bg-gray-100 z-10">
            <tr className="border-b-2">
              <th className="px-2 py-2 text-center text-xs font-bold text-gray-500 w-8">Lv</th>
              <th className="py-2 pr-3 text-left text-xs font-bold text-gray-500">품번 (Part No.)</th>
              <th className="px-2 py-2 text-left text-xs font-bold text-gray-500">품명 (Description)</th>
              <th className="px-2 py-2 text-right text-xs font-bold text-gray-500">수량</th>
              <th className="px-2 py-2 text-center text-xs font-bold text-gray-500">공급</th>
              <th className="px-2 py-2 text-left text-xs font-bold text-gray-500">협력사</th>
            </tr>
          </thead>
          <tbody>
            {tree.map((root) => (
              <TreeRow
                key={`${root.id}-${root.path}`}
                node={root}
                collapsed={collapsed}
                onToggle={onToggle}
                depth={0}
              />
            ))}
            {tree.length === 0 && (
              <tr>
                <td colSpan={6} className="text-center py-20 text-gray-400">
                  BOM 데이터가 없습니다.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* 범례 */}
      <div className="mt-3 flex gap-4 text-[10px] text-gray-400">
        <span className="flex items-center gap-1"><span>▶/▼</span> 접기/펼치기 가능한 노드</span>
        <span className="flex items-center gap-1"><span className="bg-yellow-200 px-1 rounded text-yellow-800">대체</span> 대체 부품 (Substitute)</span>
      </div>
    </div>
  );
}
```

---

## Task 12-C: BOMDetailPage 상단 정보 보강

`BOMDetailPage.tsx` 상단 모델 정보 카드에 `model_code`와 `suffix`를 분리해서 표시:

```tsx
<div className="bg-white p-4 rounded-xl shadow-sm border">
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
  </div>
</div>
```

---

## 검증 체크리스트

### 12-A (BOM 목록 그룹핑)
- [ ] 같은 model_code의 여러 suffix가 그룹으로 표시됨
- [ ] 그룹 헤더 클릭 시 접기/펼치기 동작
- [ ] suffix 없는 단일 모델은 그룹 헤더 없이 바로 표시
- [ ] 검색 시 해당 결과를 포함한 그룹만 표시
- [ ] `상세보기` 클릭 → `/bom/LSGL6335X.ASTLLGA` 라우팅 정상

### 12-B (BOM 트리 뷰어)
- [ ] BOM items가 계층 트리로 렌더링됨
- [ ] ▶/▼ 클릭으로 접기/펼치기 동작
- [ ] 기본 상태: level 0, 1 펼침 / level 2+ 접힘
- [ ] "전체 펼치기" / "전체 접기" 버튼 동작
- [ ] 대체 부품(S) 항목이 노란 배경으로 표시
- [ ] 수백 개 items에서 렌더링 성능 이상 없음 (가상 스크롤 없이 OK)
- [ ] `npm run build` TypeScript 오류 0건

---

## 완료 후

- `Phase12_Coder_Report.md` 작성
- `npm run build` 확인
- Git commit: `"Phase 12: BOM list model grouping + interactive tree viewer"`
