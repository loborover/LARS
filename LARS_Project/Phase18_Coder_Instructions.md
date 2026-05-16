# Phase 18 Coder Instructions — DP Batch 삭제 + 출처 태그(Local/ERP)

**작성자**: Claude (Chief Architect)  
**대상**: Gemini (Coder)  
**날짜**: 2026-05-17  
**우선순위**: HIGH

---

## 개요

| 태스크 | 범위 | 파일 |
|--------|------|------|
| 18-A | `import_batches` 테이블에 `data_source` 컬럼 추가 (Alembic migration) | `alembic/versions/` |
| 18-B | `ImportBatch` 모델 필드 추가 | `models/import_batch.py` |
| 18-C | DP batch 삭제 endpoint | `api/routes/dp.py` |
| 18-D | ImportBatch 생성 시 `data_source` 설정 | `services/folder_import_service.py`, `api/routes/import_pipeline.py` |
| 18-E | `/dp/batches` API 응답에 `data_source` 포함 | `api/routes/dp.py` |
| 18-F | 프론트엔드: 삭제 버튼 + 출처 태그 표시 | `.WebUI/src/pages/DailyPlanPage.tsx` |

---

## 배경 지식

### 현재 ImportBatch 모델
```python
class ImportBatch(SQLModel, table=True):
    __tablename__ = "import_batches"
    id: Optional[int]
    source_type: str      # "folder_scan" | "excel_upload" — 어떻게 올렸는지 (방법)
    source_name: str      # 파일명
    target_table: str     # "bom" | "daily_plan"
    records_inserted: int
    records_updated: int
    records_failed: int
    status: str           # "pending" | "processing" | "success" | "failed"
    error_log: JSONB
    started_by: int
    started_at: datetime
    finished_at: datetime
```

`source_type`은 이미 있으나 "어떤 방법으로 올렸는가(폴더스캔/업로드)"를 의미한다.  
새로 추가할 `data_source`는 "데이터의 출처(Local 수동 vs ERP 시스템)"를 의미한다. **다른 개념이므로 별도 컬럼 추가.**

### FK 관계 (삭제 cascade 시 중요)
```
import_batches.id ← (참조, FK 없음) ── DailyPlan.import_batch_id
import_batches.id ← (참조, FK 없음) ── DailyPlanLot.import_batch_id
DailyPlanLot.id ← (FK) ── part_list_snapshots.lot_id  ← 반드시 먼저 삭제
DailyPlanLot.plan_id → DailyPlan.id (FK)
```

`import_batches`에는 SQLModel FK declaration이 없으므로 DB 레벨 cascade가 없다.  
**삭제 순서를 코드로 직접 제어해야 한다.**

---

## Task 18-A: Alembic Migration

Alembic autogenerate를 사용하지 말고 아래 내용으로 **직접** migration 파일을 생성한다.

파일명 패턴: `backend/alembic/versions/<timestamp>_add_data_source_to_import_batches.py`  
(기존 파일들의 네이밍 패턴을 맞출 것)

```python
"""add data_source to import_batches

Revision ID: <생성된 hex ID>
Revises: <직전 revision ID>
Create Date: 2026-05-17
"""
from alembic import op
import sqlalchemy as sa

revision = '<생성된 hex ID>'
down_revision = '<직전 revision ID>'
branch_labels = None
depends_on = None

def upgrade() -> None:
    op.add_column(
        'import_batches',
        sa.Column('data_source', sa.String(50), nullable=False, server_default='local')
    )

def downgrade() -> None:
    op.drop_column('import_batches', 'data_source')
```

**Revision ID**: `alembic revision` 명령으로 생성하거나 직접 랜덤 hex 8자리 사용.  
**down_revision**: `alembic heads` 명령으로 현재 최신 revision ID 확인 후 기입.

migration 파일 작성 후 반드시 실행:
```bash
cd /test/LARS/backend
alembic upgrade head
```

성공 메시지 확인 후 다음 태스크 진행.

---

## Task 18-B: ImportBatch 모델 필드 추가

**파일**: `backend/models/import_batch.py`

`data_source` 필드 추가:

```python
class ImportBatch(SQLModel, table=True):
    __tablename__ = "import_batches"
    
    id: Optional[int] = Field(default=None, primary_key=True)
    source_type: str
    source_name: str
    target_table: str
    records_inserted: int = Field(default=0)
    records_updated: int = Field(default=0)
    records_failed: int = Field(default=0)
    status: str = Field(default="pending")
    error_log: Optional[Dict[str, Any]] = Field(default=None, sa_column=Column(JSONB))
    started_by: Optional[int] = Field(default=None, foreign_key="users.id")
    started_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    finished_at: Optional[datetime] = None
    data_source: str = Field(default="local")   # ← 추가: "local" | "erp"
```

---

## Task 18-C: DP Batch 삭제 endpoint

**파일**: `backend/api/routes/dp.py`

`router` 상단의 의존성은 이미 `require_role("internal", "manager", "admin")`이므로 별도 권한 추가 불필요.

아래 endpoint를 `dp.py`에 추가:

```python
@router.delete("/batches/{batch_id}", status_code=200)
async def delete_dp_batch(
    batch_id: int,
    session: AsyncSession = Depends(get_session)
) -> dict:
    """
    DP import batch 삭제.
    삭제 cascade 순서:
      1. PartListSnapshot (lot_id FK)
      2. DailyPlanLot (import_batch_id)
      3. DailyPlan (import_batch_id 기준, lot 없는 것만)
      4. ImportBatch
      5. Redis target 초기화 (삭제된 batch가 target이었을 경우)
    """
    from sqlalchemy import delete as sa_delete, exists
    from models.part_list import PartListSnapshot
    from core.redis_client import get_redis

    # 1. 배치 존재 확인
    batch = await session.get(ImportBatch, batch_id)
    if not batch or batch.target_table != "daily_plan":
        raise HTTPException(status_code=404, detail="DP batch not found")

    # 2. 해당 배치의 lot id 목록 수집
    lot_ids_stmt = select(DailyPlanLot.id).where(DailyPlanLot.import_batch_id == batch_id)
    lot_ids_res = await session.execute(lot_ids_stmt)
    lot_ids = [row[0] for row in lot_ids_res.all()]

    # 3. PartListSnapshot 삭제 (FK 우선)
    if lot_ids:
        await session.execute(
            sa_delete(PartListSnapshot).where(PartListSnapshot.lot_id.in_(lot_ids))
        )

    # 4. DailyPlanLot 삭제
    await session.execute(
        sa_delete(DailyPlanLot).where(DailyPlanLot.import_batch_id == batch_id)
    )

    # 5. lot이 없어진 DailyPlan 삭제 (import_batch_id가 이 배치인 것 중 lots가 없는 것)
    plan_ids_stmt = select(DailyPlan.id).where(DailyPlan.import_batch_id == batch_id)
    plan_ids_res = await session.execute(plan_ids_stmt)
    plan_ids = [row[0] for row in plan_ids_res.all()]

    if plan_ids:
        # lot이 남아 있는 plan_id는 제외 (다른 배치가 덮어쓴 경우)
        has_lots_stmt = select(DailyPlanLot.plan_id).where(
            DailyPlanLot.plan_id.in_(plan_ids)
        ).distinct()
        has_lots_res = await session.execute(has_lots_stmt)
        plans_with_lots = set(row[0] for row in has_lots_res.all())
        empty_plan_ids = [pid for pid in plan_ids if pid not in plans_with_lots]

        if empty_plan_ids:
            await session.execute(
                sa_delete(DailyPlan).where(DailyPlan.id.in_(empty_plan_ids))
            )

    # 6. ImportBatch 삭제
    await session.delete(batch)
    await session.commit()

    # 7. Redis target 초기화
    redis = await get_redis()
    target_raw = await redis.get("dp:target_batch_id")
    if target_raw and int(target_raw) == batch_id:
        await redis.delete("dp:target_batch_id")

    return {"status": "deleted", "batch_id": batch_id}
```

**주의**: `PartListSnapshot` 모델이 `models/part_list.py`에 있는지 확인. import 경로가 다르면 수정.

---

## Task 18-D: ImportBatch 생성 시 data_source 설정

모든 수동 import 경로에서 `data_source="local"` 을 명시적으로 설정한다.

### folder_import_service.py

`scan_and_import_folder()` 함수의 `ImportBatch(...)` 생성 부분:

```python
batch = ImportBatch(
    source_type="folder_scan",
    source_name=filename,
    target_table="bom" if file_type == "bom" else "daily_plan",
    status="processing",
    started_by=user_id,
    data_source="local",   # ← 추가
)
```

### import_pipeline.py — upload endpoint

`/upload` 경로의 `ImportBatch(...)` 생성:

```python
batch = ImportBatch(
    source_type="excel_upload",
    source_name=filename,
    target_table=target_table,
    status="pending",
    started_by=current_user.id,
    data_source="local",   # ← 추가
)
```

**import_pipeline.py** 내에 `ImportBatch` 생성이 여러 곳일 수 있다. **파일 전체를 grep하여 `ImportBatch(`가 있는 모든 위치**에 `data_source="local"` 추가. 누락 없이.

---

## Task 18-E: /dp/batches 응답에 data_source 포함

**파일**: `backend/api/routes/dp.py` — `get_dp_batches()` 함수

각 배치 응답 dict에 `data_source` 추가:

```python
result.append({
    "batch_id": b.id,
    "date_min": date_row[0].date().isoformat() if date_row and date_row[0] else None,
    "date_max": date_row[1].date().isoformat() if date_row and date_row[1] else None,
    "lot_count": lot_count,
    "finished_at": b.finished_at.isoformat() if b.finished_at else None,
    "is_target": b.id == target_batch_id,
    "data_source": b.data_source,   # ← 추가
})
```

---

## Task 18-F: 프론트엔드 수정

**파일**: `/test/LARS/.WebUI/src/pages/DailyPlanPage.tsx`

### 변경 사항 목록

1. `DpBatch` 인터페이스에 `data_source` 필드 추가
2. 삭제 mutation 추가
3. 배치 카드에 출처 태그(Local/ERP) 표시
4. 배치 카드에 삭제 버튼 추가 (확인 dialog 포함)
5. 삭제된 배치가 현재 선택된 배치였으면 선택 해제

### 완전 수정된 DailyPlanPage.tsx

현재 파일을 아래로 **완전히 교체**:

```tsx
import { useState, useEffect, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { apiClient } from '../api/client';
import { TutorialBox } from '../components/TutorialBox';
import { useColumnFilter } from '../hooks/useColumnFilter';
import { FilterableHeader } from '../components/FilterableHeader';
import DailyPlanPrintView from '../components/dp/DailyPlanPrintView';
import { Calendar, XCircle, Trash2 } from 'lucide-react';

interface DpBatch {
  batch_id: number;
  date_min: string | null;
  date_max: string | null;
  lot_count: number;
  finished_at: string | null;
  is_target: boolean;
  data_source: string;  // "local" | "erp"
}

export interface LotRaw {
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

function DataSourceTag({ source }: { source: string }) {
  if (source === 'erp') {
    return (
      <span className="text-[9px] bg-purple-100 text-purple-700 border border-purple-200 px-1.5 py-0.5 rounded font-bold uppercase tracking-tighter">
        ERP
      </span>
    );
  }
  return (
    <span className="text-[9px] bg-gray-100 text-gray-500 border border-gray-200 px-1.5 py-0.5 rounded font-bold uppercase tracking-tighter">
      Local
    </span>
  );
}

export default function DailyPlanPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [selectedBatchId, setSelectedBatchId] = useState<number | null>(null);
  const [viewMode, setViewMode] = useState<'raw' | 'print'>('raw');
  const [confirmDeleteId, setConfirmDeleteId] = useState<number | null>(null);

  // Batch 목록
  const { data: batches = [] } = useQuery<DpBatch[]>({
    queryKey: ['dp-batches'],
    queryFn: async () => (await apiClient.get('/dp/batches')).data,
  });

  // 자동으로 첫 번째 batch 선택 (최신 배치)
  useEffect(() => {
    if (batches.length > 0 && selectedBatchId === null) {
      const target = batches.find(b => b.is_target);
      if (target) {
        setSelectedBatchId(target.batch_id);
      } else {
        setSelectedBatchId(batches[0].batch_id);
      }
    }
  }, [batches, selectedBatchId]);

  // Raw lots
  const { data: lots = [], isLoading } = useQuery<LotRaw[]>({
    queryKey: ['dp-lots-raw', selectedBatchId],
    queryFn: async () => (await apiClient.get('/dp/lots-raw', { params: { batch_id: selectedBatchId } })).data,
    enabled: !!selectedBatchId,
  });

  const filterProps = useColumnFilter(lots);
  const { filtered: filteredLots, hasAnyFilter, clearAll } = filterProps;

  // Set Target mutation
  const setTargetMutation = useMutation({
    mutationFn: async (batch_id: number) =>
      apiClient.post('/dp/set-target', null, { params: { batch_id } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dp-batches'] }),
  });

  // Delete batch mutation
  const deleteMutation = useMutation({
    mutationFn: async (batch_id: number) =>
      apiClient.delete(`/dp/batches/${batch_id}`),
    onSuccess: (_, deletedBatchId) => {
      queryClient.invalidateQueries({ queryKey: ['dp-batches'] });
      // 삭제된 배치가 현재 선택된 배치였으면 선택 해제
      if (selectedBatchId === deletedBatchId) {
        setSelectedBatchId(null);
      }
      setConfirmDeleteId(null);
    },
    onError: () => {
      setConfirmDeleteId(null);
      alert('삭제 중 오류가 발생했습니다.');
    },
  });

  // BOM 모델 목록 (연결 여부 판단용)
  const { data: bomModels = [] } = useQuery<{ model_number: string }[]>({
    queryKey: ['bom-models-all'],
    queryFn: async () => (await apiClient.get('/bom/models', { params: { is_active: true } })).data,
    staleTime: 60000,
  });
  const bomModelSet = useMemo(
    () => new Set(bomModels.map((m) => m.model_number)),
    [bomModels]
  );

  // 동적 날짜 컬럼 계산
  const dateColumns = Array.from(
    new Set(filteredLots.flatMap((l) => Object.keys(l.daily_qty)))
  ).sort();

  const targetBatch = batches.find((b) => b.is_target);

  return (
    <div className="flex flex-col h-full">
      {/* Sticky 머릿말 */}
      <div className="sticky top-0 z-20 bg-gray-50 pb-3 space-y-3">
        <div className="flex justify-between items-center">
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <Calendar className="text-blue-600" />
              Daily Plan (일일생산계획)
            </h1>
            {targetBatch && (
              <span className="text-xs bg-green-100 text-green-700 border border-green-300 px-2 py-1 rounded-full font-semibold shadow-sm">
                Current Target: {targetBatch.date_min} ~ {targetBatch.date_max}
              </span>
            )}
          </div>
          <div className="flex items-center gap-2">
            {hasAnyFilter && (
              <button
                onClick={clearAll}
                className="flex items-center gap-1 text-xs text-red-500 hover:text-red-700 font-bold bg-red-50 px-3 py-1.5 rounded-lg border border-red-100 transition-all"
              >
                <XCircle size={14} />
                필터 초기화
              </button>
            )}

            {/* 뷰 전환 탭 */}
            <div className="flex rounded-lg border border-gray-200 overflow-hidden text-[10px] font-bold uppercase tracking-tight shadow-sm shrink-0">
              <button
                onClick={() => setViewMode('raw')}
                className={`px-3 py-1.5 transition-all ${viewMode === 'raw' ? 'bg-gray-800 text-white shadow-inner' : 'bg-white text-gray-500 hover:bg-gray-50'}`}
              >
                Raw Table
              </button>
              <button
                onClick={() => setViewMode('print')}
                className={`px-3 py-1.5 transition-all ${viewMode === 'print' ? 'bg-gray-800 text-white shadow-inner' : 'bg-white text-gray-500 hover:bg-gray-50'}`}
              >
                Print View
              </button>
            </div>
          </div>
        </div>

        <TutorialBox pageKey="daily-plan">
          좌측 목록에서 열람할 계획 파일을 선택하세요. <b>[Set as Target]</b>을 클릭하면 해당 계획이 PSI 및 소요자재 산출의 기준이 됩니다. <b>Local</b> 태그는 수동 import, <b>ERP</b> 태그는 ERP 연동 데이터를 의미합니다.
        </TutorialBox>
      </div>

      {/* 본문: 좌측 batch 목록 + 우측 테이블 */}
      <div className="flex flex-1 min-h-0 gap-4 mt-2">

        {/* 좌측: Batch 목록 */}
        <div className="w-72 shrink-0 overflow-y-auto space-y-2 pr-1 no-print">
          {batches.map((b) => (
            <div
              key={b.batch_id}
              onClick={() => setSelectedBatchId(b.batch_id)}
              className={`p-3 rounded-xl border cursor-pointer transition-all relative ${
                selectedBatchId === b.batch_id
                  ? 'border-blue-500 bg-blue-50 shadow-md ring-1 ring-blue-200'
                  : 'border-gray-200 bg-white hover:border-blue-300 hover:shadow-sm'
              }`}
            >
              {/* 헤더: 날짜 범위 + Target 배지 + 출처 태그 */}
              <div className="flex items-center justify-between mb-1 gap-1">
                <span className={`font-bold text-sm truncate ${selectedBatchId === b.batch_id ? 'text-blue-700' : 'text-gray-700'}`}>
                  {b.date_min} ~ {b.date_max}
                </span>
                <div className="flex items-center gap-1 shrink-0">
                  <DataSourceTag source={b.data_source} />
                  {b.is_target && (
                    <span className="text-[9px] bg-blue-600 text-white px-1.5 py-0.5 rounded-sm font-black uppercase">
                      Target
                    </span>
                  )}
                </div>
              </div>

              {/* 메타 정보 */}
              <div className="text-[11px] text-gray-500 mb-2 flex justify-between">
                <span>{b.lot_count} lots</span>
                <span>{b.finished_at?.slice(5, 16).replace('T', ' ')}</span>
              </div>

              {/* 액션 버튼 */}
              <div className="flex gap-1.5">
                {!b.is_target && (
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      setTargetMutation.mutate(b.batch_id);
                    }}
                    className="flex-1 py-1 text-[11px] text-blue-600 font-semibold hover:bg-blue-100 bg-white border border-blue-200 rounded-md transition-colors"
                  >
                    Set as Target
                  </button>
                )}

                {/* 삭제 버튼 */}
                {confirmDeleteId === b.batch_id ? (
                  /* 확인 상태 */
                  <div
                    className="flex gap-1 flex-1"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <button
                      onClick={() => deleteMutation.mutate(b.batch_id)}
                      disabled={deleteMutation.isPending}
                      className="flex-1 py-1 text-[11px] text-white font-bold bg-red-500 hover:bg-red-600 rounded-md transition-colors disabled:opacity-50"
                    >
                      {deleteMutation.isPending ? '삭제 중...' : '확인 삭제'}
                    </button>
                    <button
                      onClick={() => setConfirmDeleteId(null)}
                      className="flex-1 py-1 text-[11px] text-gray-600 font-semibold bg-gray-100 hover:bg-gray-200 rounded-md transition-colors"
                    >
                      취소
                    </button>
                  </div>
                ) : (
                  /* 기본 삭제 버튼 */
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      setConfirmDeleteId(b.batch_id);
                    }}
                    title="이 배치 삭제"
                    className={`flex items-center justify-center px-2 py-1 rounded-md border transition-colors text-[11px] ${
                      b.is_target
                        ? 'border-gray-100 text-gray-300 bg-white cursor-not-allowed'
                        : 'border-red-100 text-red-400 hover:bg-red-50 hover:text-red-600 bg-white'
                    }`}
                    disabled={b.is_target}
                  >
                    <Trash2 size={12} />
                  </button>
                )}
              </div>

              {/* Target 배치 삭제 안내 */}
              {b.is_target && confirmDeleteId === b.batch_id && (
                <p className="text-[10px] text-orange-500 mt-1">
                  Target 배치입니다. 먼저 다른 배치를 Target으로 설정하세요.
                </p>
              )}
            </div>
          ))}
          {batches.length === 0 && (
            <div className="text-sm text-gray-400 p-8 text-center bg-white rounded-xl border border-dashed">
              DP 파일이 없습니다.<br />Import 탭에서 업로드하세요.
            </div>
          )}
        </div>

        {/* 우측: 테이블 영역 */}
        <div className="flex-1 overflow-hidden flex flex-col">
          {viewMode === 'raw' ? (
            <div className="flex-1 overflow-hidden bg-white rounded-xl border shadow-sm flex flex-col">
              {isLoading ? (
                <div className="flex-1 flex flex-col items-center justify-center space-y-4">
                  <div className="w-10 h-10 border-4 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
                  <p className="text-gray-400 text-sm font-medium">데이터 로드 중...</p>
                </div>
              ) : filteredLots.length === 0 ? (
                <div className="flex-1 flex items-center justify-center text-gray-400 font-medium">
                  {selectedBatchId ? '필터링된 데이터가 없습니다.' : '좌측에서 Batch를 선택하세요.'}
                </div>
              ) : (
                <div className="flex-1 overflow-auto">
                  <table className="w-full text-xs border-collapse relative">
                    <thead className="sticky top-0 bg-gray-100 z-10 shadow-sm">
                      <tr>
                        <FilterableHeader label="Line" field="line_code" {...filterProps} className="text-left whitespace-nowrap" />
                        <th className="px-3 py-2.5 text-left border-b font-bold text-gray-600 whitespace-nowrap">PST (Start)</th>
                        <FilterableHeader label="W/O (제번)" field="wo_number" {...filterProps} className="text-left whitespace-nowrap" />
                        <FilterableHeader label="Model.Suffix" field="model_number" {...filterProps} className="text-left whitespace-nowrap" />
                        <th className="px-3 py-2.5 text-right border-b font-bold text-gray-600 whitespace-nowrap">Lot Qty</th>
                        <th className="px-3 py-2.5 text-right border-b font-bold text-gray-600 whitespace-nowrap">Remain</th>
                        {dateColumns.map((d) => (
                          <th key={d} className="px-3 py-2.5 text-right border-b font-bold text-gray-600 whitespace-nowrap">
                            {d.slice(5)}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y border-b">
                      {filteredLots.map((lot, idx) => (
                        <tr key={idx} className="hover:bg-blue-50/50 transition-colors group">
                          <td className="px-3 py-2 font-mono text-xs font-semibold text-purple-700 bg-purple-50/30 whitespace-nowrap">
                            {lot.line_code || '-'}
                          </td>
                          <td className="px-3 py-2 text-gray-400 whitespace-nowrap">
                            {lot.planned_start ? lot.planned_start.slice(5, 16).replace('T', ' ') : '-'}
                          </td>
                          <td className="px-3 py-2 font-mono font-bold text-gray-700 whitespace-nowrap">{lot.wo_number}</td>
                          <td
                            className={`px-3 py-2 font-medium whitespace-nowrap select-none ${
                              bomModelSet.has(lot.model_number)
                                ? 'text-blue-600 cursor-pointer hover:text-blue-800 hover:underline'
                                : 'text-red-500 cursor-not-allowed'
                            }`}
                            title={
                              bomModelSet.has(lot.model_number)
                                ? `더블클릭: ${lot.model_number} BOM 보기`
                                : `BOM 미등록: ${lot.model_number}`
                            }
                            onDoubleClick={() => {
                              if (bomModelSet.has(lot.model_number)) {
                                navigate(`/bom/${encodeURIComponent(lot.model_number)}`);
                              }
                            }}
                          >
                            {lot.model_number}
                          </td>
                          <td className="px-3 py-2 text-right font-semibold text-gray-700">{lot.planned_qty.toLocaleString()}</td>
                          <td className={`px-3 py-2 text-right font-black ${lot.remain_qty > 0 ? 'text-orange-500' : 'text-green-600'}`}>
                            {lot.remain_qty.toLocaleString()}
                          </td>
                          {dateColumns.map((d) => (
                            <td key={d} className={`px-3 py-2 text-right font-medium ${lot.daily_qty[d] ? 'text-gray-900 bg-blue-50/20' : 'text-gray-300'}`}>
                              {lot.daily_qty[d] ? lot.daily_qty[d].toLocaleString() : '-'}
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                    <tfoot className="sticky bottom-0 bg-gray-900 text-white z-10">
                      <tr className="font-bold">
                        <td colSpan={4} className="px-3 py-2.5 text-right uppercase tracking-wider text-[10px]">Grand Total</td>
                        <td className="px-3 py-2.5 text-right text-sm">
                          {filteredLots.reduce((s, l) => s + l.planned_qty, 0).toLocaleString()}
                        </td>
                        <td className="px-3 py-2.5 text-right text-sm">
                          {filteredLots.reduce((s, l) => s + l.remain_qty, 0).toLocaleString()}
                        </td>
                        {dateColumns.map((d) => (
                          <td key={d} className="px-3 py-2.5 text-right text-sm text-blue-300">
                            {filteredLots.reduce((s, l) => s + (l.daily_qty[d] || 0), 0).toLocaleString()}
                          </td>
                        ))}
                      </tr>
                    </tfoot>
                  </table>
                </div>
              )}
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
      </div>
    </div>
  );
}
```

---

## 실행 순서

```
1. alembic upgrade head 실행 및 성공 확인
2. backend/models/import_batch.py — data_source 필드 추가
3. backend/api/routes/dp.py — delete endpoint + /batches 응답에 data_source 추가
4. backend/services/folder_import_service.py — data_source="local" 추가
5. backend/api/routes/import_pipeline.py — ImportBatch 생성 모든 위치에 data_source="local" 추가
6. 백엔드 재시작 (kill + restart uvicorn)
7. API 동작 확인:
   GET  /api/dp/batches       → data_source 필드 포함 확인
   DELETE /api/dp/batches/1   → 204/200 응답 확인 (존재하는 non-target 배치 id 사용)
8. .WebUI/src/pages/DailyPlanPage.tsx 교체
9. npm run build
10. vite preview 재시작 (port 3000)
11. 브라우저 확인:
    - 각 배치 카드에 Local 또는 ERP 태그 표시
    - 삭제 버튼 클릭 → 인라인 확인/취소 버튼 표시
    - 확인 → 배치 삭제 후 목록에서 사라짐
    - Target 배치는 삭제 버튼 비활성화(회색)
```

---

## 검증 기준 (Acceptance Criteria)

### 18-A/B: Migration
- [ ] `alembic upgrade head` 오류 없이 완료
- [ ] `SELECT data_source FROM import_batches LIMIT 5;` → "local" 값 반환

### 18-C: Delete API
- [ ] `DELETE /api/dp/batches/{id}` 200 응답
- [ ] DB에서 해당 lot, plan, batch 레코드 삭제 확인
- [ ] 삭제된 batch가 Redis target이었으면 `dp:target_batch_id` 키 삭제 확인
- [ ] 존재하지 않는 batch_id → 404 응답

### 18-D: data_source 설정
- [ ] 폴더 import 또는 파일 업로드 후 `import_batches.data_source = 'local'` 확인

### 18-E: API 응답
- [ ] `GET /api/dp/batches` 응답에 `data_source` 필드 포함

### 18-F: 프론트엔드
- [ ] 배치 카드에 Local 태그 (회색) 또는 ERP 태그 (보라색) 표시
- [ ] 삭제 버튼 클릭 시 인라인 확인/취소 버튼으로 전환
- [ ] 삭제 확인 시 배치 목록에서 즉시 사라짐
- [ ] 삭제된 배치가 선택 중이었으면 우측 테이블 초기화됨
- [ ] Target 배치의 삭제 버튼은 비활성화(gray, cursor-not-allowed)

---

## 주의사항

1. **Target 배치 삭제 방지**: 프론트엔드에서 `disabled={b.is_target}` 처리로 방지. 백엔드에서도 추가 방어:
   ```python
   # delete endpoint 상단에 추가 (선택 사항, 안전을 위해 권장)
   redis = await get_redis()
   target_raw = await redis.get("dp:target_batch_id")
   if target_raw and int(target_raw) == batch_id:
       raise HTTPException(status_code=400, detail="Cannot delete the current target batch. Set another batch as target first.")
   ```
   단, 위 코드를 추가하면 **Redis target 삭제 로직(step 7)은 불필요**해지므로 일관성 있게 선택할 것.
   **권장**: 백엔드에서 target 삭제를 막지 말고, 프론트에서 UI로만 막는다. (유연성 유지)

2. **`PartListSnapshot` import 경로**: `delete endpoint`에서 `from models.part_list import PartListSnapshot` — 파일이 `part_list.py`가 아닐 경우 실제 경로로 수정.

3. **Alembic `down_revision`**: `alembic heads` 명령 실행 결과값을 그대로 사용. 틀리면 migration 체인이 깨짐.

4. **`Trash2` icon**: lucide-react에서 import. `import { Calendar, XCircle, Trash2 } from 'lucide-react';`
