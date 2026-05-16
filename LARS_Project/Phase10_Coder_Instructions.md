# Phase 10 Coder Instructions — DP Viewer 전면 재설계

**Role:** Coder (Gemini)  
**Date:** 2026-05-16  
**Priority:** High  
**이전 Phase 8 DP Viewer 구현을 대체한다. DailyPlanViewer.tsx, DailyPlanPrint.tsx는 이 지시서 완료 후 삭제해도 된다.**

---

## 목표

사용자 요구: *"거의 Raw data로 Excel Sheet 펼쳐보듯 볼 수 있으면 돼"*

### 핵심 변경 사항
1. **Batch 선택 목록**: 어떤 DP 파일(import batch)을 볼지 목록에서 선택
2. **Flat 테이블 뷰**: W/O가 있는 행만 표시, 열 순서 = PST → W/O → Model.Suffix → Lot Qty → Remain Qty → 날짜열들
3. **Target DP 지정**: 목록에서 하나를 "Target"으로 설정 → PSI, PartList 등 다른 모듈의 계산 기준이 됨

---

## Task 10-A: 백엔드 신규 엔드포인트

### 10-A-1. DP Batch 목록 API

`backend/api/routes/dp.py`에 추가:

```python
@router.get("/batches")
async def get_dp_batches(session: AsyncSession = Depends(get_session)) -> list[dict]:
    """DP import batch 목록과 날짜 범위, Target 여부 반환"""
    from sqlalchemy import func
    from models.import_batch import ImportBatch
    from core.redis_client import get_redis
    import json

    # import_batches 테이블에서 DP 배치 목록 조회
    stmt = select(ImportBatch).where(
        ImportBatch.target_table == "daily_plan",
        ImportBatch.status == "success"
    ).order_by(ImportBatch.finished_at.desc())
    res = await session.execute(stmt)
    batches = res.scalars().all()

    # Redis에서 현재 target batch_id 조회
    redis = await get_redis()
    target_raw = await redis.get("dp:target_batch_id")
    target_batch_id = int(target_raw) if target_raw else None

    result = []
    for b in batches:
        # 해당 batch에 속한 DailyPlan의 날짜 범위와 lot 수 조회
        date_stmt = select(
            func.min(DailyPlan.plan_date),
            func.max(DailyPlan.plan_date),
        ).where(DailyPlan.import_batch_id == b.id)
        date_res = await session.execute(date_stmt)
        date_row = date_res.one_or_none()

        from sqlalchemy import func as sqlfunc
        lot_stmt = select(func.count(DailyPlanLot.id)).where(DailyPlanLot.import_batch_id == b.id)
        lot_res = await session.execute(lot_stmt)
        lot_count = lot_res.scalar_one()

        result.append({
            "batch_id": b.id,
            "date_min": date_row[0].date().isoformat() if date_row and date_row[0] else None,
            "date_max": date_row[1].date().isoformat() if date_row and date_row[1] else None,
            "lot_count": lot_count,
            "finished_at": b.finished_at.isoformat() if b.finished_at else None,
            "is_target": b.id == target_batch_id,
        })
    return result
```

### 10-A-2. Batch Raw Lots API

```python
@router.get("/lots-raw")
async def get_lots_raw(
    batch_id: int = Query(...),
    session: AsyncSession = Depends(get_session)
) -> list[dict]:
    """
    특정 batch의 모든 lot을 flat하게 반환.
    - W/O 없는 행 제외
    - suffix는 BomModel join으로 조회
    - daily_qty_json을 날짜별 dict로 파싱해서 포함
    """
    import json
    from models.bom import BomModel

    stmt = (
        select(DailyPlanLot, BomModel.suffix)
        .outerjoin(BomModel, DailyPlanLot.model_id == BomModel.id)
        .where(
            DailyPlanLot.import_batch_id == batch_id,
            DailyPlanLot.wo_number.is_not(None),
            DailyPlanLot.wo_number != ""
        )
        .order_by(DailyPlanLot.sort_order)
    )
    res = await session.execute(stmt)
    rows = res.all()

    result = []
    for lot, suffix in rows:
        suffix = suffix or ""
        model_number = f"{lot.model_code}.{suffix}" if suffix else lot.model_code
        remain_qty = (lot.planned_qty or 0) - (lot.output_qty or 0)

        daily_qty: dict = {}
        if lot.daily_qty_json:
            try:
                daily_qty = json.loads(lot.daily_qty_json)
            except Exception:
                pass

        result.append({
            "planned_start": lot.planned_start.isoformat() if lot.planned_start else None,
            "wo_number": lot.wo_number,
            "model_number": model_number,
            "planned_qty": lot.planned_qty,
            "remain_qty": remain_qty,
            "daily_qty": daily_qty,  # {"2026-05-14": 9.0, ...}
        })
    return result
```

### 10-A-3. Target DP 설정/조회 API

```python
@router.post("/set-target")
async def set_target_batch(batch_id: int) -> dict:
    """Target DP batch_id를 Redis에 저장"""
    from core.redis_client import get_redis
    redis = await get_redis()
    await redis.set("dp:target_batch_id", str(batch_id))
    return {"status": "ok", "target_batch_id": batch_id}

@router.get("/target-batch")
async def get_target_batch(session: AsyncSession = Depends(get_session)) -> dict:
    """현재 Target DP batch 정보 반환"""
    from core.redis_client import get_redis
    from models.import_batch import ImportBatch
    redis = await get_redis()
    target_raw = await redis.get("dp:target_batch_id")
    if not target_raw:
        return {"target_batch_id": None}
    batch_id = int(target_raw)
    stmt = select(ImportBatch).where(ImportBatch.id == batch_id)
    res = await session.execute(stmt)
    b = res.scalar_one_or_none()
    return {
        "target_batch_id": batch_id,
        "finished_at": b.finished_at.isoformat() if b and b.finished_at else None,
    }
```

### 10-A-4. 라우트 순서 주의

`dp.py` 라우터에서 경로 충돌 방지를 위해 등록 순서:
1. `GET /batches`
2. `GET /lots-raw`
3. `GET /daily`
4. `GET /dates`
5. `POST /set-target`
6. `GET /target-batch`
7. `GET /` (list plans)
8. `GET /{plan_id}/lots`

---

## Task 10-B: 프론트엔드 — DailyPlanPage 전면 교체

**파일**: `.WebUI/src/pages/DailyPlanPage.tsx`

### 레이아웃 구조

```
┌─────────────────────────────────────────────────────┐
│ [sticky 머릿말] Daily Plan  [Target: 2026-05-14 ~ 16]│
├─────────────────────────────────────────────────────┤
│ ← Batch 목록 패널 (좌, 고정폭 280px)  │ 테이블 뷰  │
│ ┌─────────────────────────┐            │ (우, flex-1)│
│ │ ● 2026-05-12 ~ 05-16   │ ← selected  │            │
│ │   234 lots | [TARGET]  │             │   PST │ W/O│
│ │   [Set as Target]       │             │   ... │ ...│
│ │─────────────────────────│             │            │
│ │ ○ 2026-05-08 ~ 05-10   │             │            │
│ │   198 lots              │             │            │
│ │   [Set as Target]       │             │            │
│ └─────────────────────────┘             │            │
└─────────────────────────────────────────────────────┘
```

### 구현 코드

```tsx
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';

interface DpBatch {
  batch_id: number;
  date_min: string | null;
  date_max: string | null;
  lot_count: number;
  finished_at: string | null;
  is_target: boolean;
}

interface LotRaw {
  planned_start: string | null;
  wo_number: string;
  model_number: string;
  planned_qty: number;
  remain_qty: number;
  daily_qty: Record<string, number>;
}

export default function DailyPlanPage() {
  const queryClient = useQueryClient();
  const [selectedBatchId, setSelectedBatchId] = useState<number | null>(null);

  // Batch 목록
  const { data: batches = [] } = useQuery<DpBatch[]>({
    queryKey: ['dp-batches'],
    queryFn: async () => (await apiClient.get('/dp/batches')).data,
  });

  // 자동으로 첫 번째 batch 선택
  useState(() => {
    if (batches.length > 0 && selectedBatchId === null) {
      setSelectedBatchId(batches[0].batch_id);
    }
  });

  // Raw lots
  const { data: lots = [], isLoading } = useQuery<LotRaw[]>({
    queryKey: ['dp-lots-raw', selectedBatchId],
    queryFn: async () => (await apiClient.get('/dp/lots-raw', { params: { batch_id: selectedBatchId } })).data,
    enabled: !!selectedBatchId,
  });

  // Set Target mutation
  const setTargetMutation = useMutation({
    mutationFn: async (batch_id: number) =>
      apiClient.post('/dp/set-target', null, { params: { batch_id } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dp-batches'] }),
  });

  // 동적 날짜 컬럼 계산
  const dateColumns = Array.from(
    new Set(lots.flatMap((l) => Object.keys(l.daily_qty)))
  ).sort();

  return (
    <div className="flex flex-col h-full">
      {/* Sticky 머릿말 */}
      <div className="sticky top-0 z-20 bg-gray-50 pb-3">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-bold">Daily Plan (일일생산계획)</h1>
          {batches.find((b) => b.is_target) && (
            <span className="text-xs bg-green-100 text-green-700 border border-green-300 px-2 py-1 rounded-full font-semibold">
              Target: {batches.find((b) => b.is_target)?.date_min} ~ {batches.find((b) => b.is_target)?.date_max}
            </span>
          )}
        </div>
      </div>

      {/* 본문: 좌측 batch 목록 + 우측 테이블 */}
      <div className="flex flex-1 min-h-0 gap-4 mt-2">

        {/* 좌측: Batch 목록 */}
        <div className="w-72 shrink-0 overflow-y-auto space-y-2">
          {batches.map((b) => (
            <div
              key={b.batch_id}
              onClick={() => setSelectedBatchId(b.batch_id)}
              className={`p-3 rounded-lg border cursor-pointer transition-all ${
                selectedBatchId === b.batch_id
                  ? 'border-blue-400 bg-blue-50 shadow-sm'
                  : 'border-gray-200 bg-white hover:bg-gray-50'
              }`}
            >
              <div className="flex items-center justify-between mb-1">
                <span className="font-medium text-sm">
                  {b.date_min} ~ {b.date_max}
                </span>
                {b.is_target && (
                  <span className="text-[10px] bg-green-500 text-white px-1.5 py-0.5 rounded font-bold">
                    TARGET
                  </span>
                )}
              </div>
              <div className="text-xs text-gray-500 mb-2">{b.lot_count} lots</div>
              {!b.is_target && (
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    setTargetMutation.mutate(b.batch_id);
                  }}
                  className="text-xs text-blue-600 hover:underline border border-blue-200 px-2 py-0.5 rounded"
                >
                  Set as Target
                </button>
              )}
            </div>
          ))}
          {batches.length === 0 && (
            <div className="text-sm text-gray-400 p-4">DP 파일이 없습니다. Import 탭에서 업로드하세요.</div>
          )}
        </div>

        {/* 우측: Flat 테이블 */}
        <div className="flex-1 overflow-auto bg-white rounded-lg border shadow-sm">
          {isLoading ? (
            <div className="p-8 text-center text-gray-400">로딩 중...</div>
          ) : lots.length === 0 ? (
            <div className="p-8 text-center text-gray-400">
              {selectedBatchId ? 'W/O가 있는 데이터가 없습니다.' : '좌측에서 Batch를 선택하세요.'}
            </div>
          ) : (
            <table className="w-full text-sm border-collapse">
              <thead className="sticky top-0 bg-gray-100 z-10">
                <tr>
                  <th className="px-3 py-2 text-left border-b font-semibold text-gray-600 whitespace-nowrap">PST</th>
                  <th className="px-3 py-2 text-left border-b font-semibold text-gray-600 whitespace-nowrap">W/O</th>
                  <th className="px-3 py-2 text-left border-b font-semibold text-gray-600 whitespace-nowrap">Model.Suffix</th>
                  <th className="px-3 py-2 text-right border-b font-semibold text-gray-600 whitespace-nowrap">Lot Qty</th>
                  <th className="px-3 py-2 text-right border-b font-semibold text-gray-600 whitespace-nowrap">Remain</th>
                  {dateColumns.map((d) => (
                    <th key={d} className="px-3 py-2 text-right border-b font-semibold text-gray-600 whitespace-nowrap">
                      {d.slice(5)} {/* MM-DD 표시 */}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {lots.map((lot, idx) => (
                  <tr key={idx} className={idx % 2 === 0 ? 'bg-white' : 'bg-gray-50'}>
                    <td className="px-3 py-1.5 border-b text-gray-500 text-xs whitespace-nowrap">
                      {lot.planned_start ? lot.planned_start.slice(0, 16).replace('T', ' ') : '-'}
                    </td>
                    <td className="px-3 py-1.5 border-b font-mono text-xs whitespace-nowrap">{lot.wo_number}</td>
                    <td className="px-3 py-1.5 border-b whitespace-nowrap">{lot.model_number}</td>
                    <td className="px-3 py-1.5 border-b text-right">{lot.planned_qty.toLocaleString()}</td>
                    <td className={`px-3 py-1.5 border-b text-right font-medium ${lot.remain_qty > 0 ? 'text-orange-600' : 'text-green-600'}`}>
                      {lot.remain_qty.toLocaleString()}
                    </td>
                    {dateColumns.map((d) => (
                      <td key={d} className="px-3 py-1.5 border-b text-right text-gray-700">
                        {lot.daily_qty[d] ? lot.daily_qty[d].toLocaleString() : '-'}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
              <tfoot className="sticky bottom-0 bg-gray-100">
                <tr className="font-semibold text-gray-700">
                  <td colSpan={3} className="px-3 py-2 border-t">합계</td>
                  <td className="px-3 py-2 border-t text-right">
                    {lots.reduce((s, l) => s + l.planned_qty, 0).toLocaleString()}
                  </td>
                  <td className="px-3 py-2 border-t text-right">
                    {lots.reduce((s, l) => s + l.remain_qty, 0).toLocaleString()}
                  </td>
                  {dateColumns.map((d) => (
                    <td key={d} className="px-3 py-2 border-t text-right">
                      {lots.reduce((s, l) => s + (l.daily_qty[d] || 0), 0).toLocaleString()}
                    </td>
                  ))}
                </tr>
              </tfoot>
            </table>
          )}
        </div>
      </div>
    </div>
  );
}
```

---

## Task 10-C: PSI/PartList — Target DP 연동

PSI와 PartList 서비스가 DP 데이터를 조회할 때 `dp:target_batch_id` Redis 키를 참조하도록 변경한다.

### `backend/services/psi_service.py` 수정

DP 데이터를 조회하는 쿼리에 batch_id 필터 추가:

```python
async def get_target_dp_batch_id() -> int | None:
    from core.redis_client import get_redis
    redis = await get_redis()
    raw = await redis.get("dp:target_batch_id")
    return int(raw) if raw else None
```

DP 관련 `select(DailyPlanLot)` 쿼리에:
```python
batch_id = await get_target_dp_batch_id()
if batch_id:
    stmt = stmt.where(DailyPlanLot.import_batch_id == batch_id)
```

### `backend/services/part_list_service.py` 수정

동일 패턴으로 `import_batch_id` 필터 적용.

> **주의**: target batch_id가 설정되지 않은 경우(None) — 기존 동작(전체 데이터) 유지.

---

## Task 10-D: 정리 (Phase 8 잔재 제거)

Phase 8에서 만들었지만 더 이상 필요 없는 파일:
- `.WebUI/src/components/dp/DailyPlanViewer.tsx` — 삭제
- `.WebUI/src/components/dp/DailyPlanPrint.tsx` — 삭제

`DailyPlanPage.tsx`에서 이 컴포넌트들 import 제거됨 (새 코드로 대체).

Phase 8에서 추가된 백엔드 엔드포인트(`GET /dp/daily`, `GET /dp/dates`)는 **유지**. 다른 곳에서 참조될 수 있으므로 삭제하지 않는다.

---

## 검증 체크리스트

- [ ] `GET /api/v1/dp/batches` — batch 목록 반환, `is_target` 포함
- [ ] `GET /api/v1/dp/lots-raw?batch_id=1` — lot 목록, W/O 없는 행 제외, `daily_qty` dict 포함
- [ ] `POST /api/v1/dp/set-target?batch_id=1` — Redis `dp:target_batch_id` 설정됨
- [ ] 프론트엔드: batch 목록 좌측 패널, 클릭 시 우측 테이블 로드
- [ ] "Set as Target" 클릭 후 TARGET 배지 표시
- [ ] 날짜 열 동적 생성 (daily_qty_json 기반)
- [ ] 합계 행(tfoot) sticky 하단 고정
- [ ] Remain 양수 → 주황, 0 이하 → 초록
- [ ] `npm run build` TypeScript 오류 0건

---

## 완료 후

- `Phase10_Coder_Report.md` 작성
- `npm run build` + 프리뷰 재시작
- Git commit: `"Phase 10: DP viewer redesign (batch-based flat table + target DP)"`
