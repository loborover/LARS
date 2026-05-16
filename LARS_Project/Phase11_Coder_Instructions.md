# Phase 11 Coder Instructions — Import 자동 후처리 & 통합 Background Monitor

**Role:** Coder (Gemini)  
**Date:** 2026-05-16  
**Priority:** Critical

---

## 문제 현황

### 문제 1: Import 후처리가 동기 블로킹
- BOM import → `rebuild_from_bom(session)` 동기 호출 (request 차단)
- DP upload+process → `part_list_service.recompute_for_dates()` 동기 호출
- DP folder import → `psi_service.recompute_all(session)` 동기 호출
- 결과: 사용자가 "Import 완료" 버튼 누르면 수백 초 대기 or 타임아웃

### 문제 2: BackgroundMonitor가 ItemMaster만 감지
- 현재 `BackgroundMonitor.tsx`는 `/items/rebuild/status` 하나만 폴링
- PSI 재계산, PartList 재계산 상태는 UI에서 전혀 확인 불가
- 사이드바에 마운트는 됐으나 실제로 표시되는 경우가 거의 없음

---

## 목표

1. BOM/DP Import 완료 즉시 응답 반환 → 후처리는 Background에서 자동 실행
2. 통합 Background Status API (하나의 endpoint에서 모든 태스크 상태 반환)
3. `BackgroundMonitor.tsx` 업그레이드 — 모든 태스크 표시

---

## Task 11-A: 통합 Background Status API

### 11-A-1. Redis 키 규약 정의

각 백그라운드 태스크는 아래 형식의 Redis 키로 상태를 기록한다:

| 태스크 | Redis 키 |
|--------|---------|
| ItemMaster 재구성 | `itemmaster:rebuild_status` (기존 유지) |
| PartList 재계산 | `partlist:recompute_status` |
| PSI 재계산 | `psi:recompute_status` |

각 키의 값 형식 (JSON):
```json
{
  "status": "idle | running | done | failed",
  "progress": 0,
  "total": 0,
  "processed": 0,
  "label": "소요자재 재계산",
  "started_at": "ISO string or null",
  "finished_at": "ISO string or null",
  "error": "string or null"
}
```

### 11-A-2. 통합 Status 엔드포인트

새 파일: `backend/api/routes/background.py`

```python
from fastapi import APIRouter
from core.redis_client import get_redis
import json

router = APIRouter()

TASK_KEYS = [
    ("item_rebuild",       "itemmaster:rebuild_status",  "ItemMaster 재구성"),
    ("part_list_recompute","partlist:recompute_status",  "소요자재 재계산"),
    ("psi_recompute",      "psi:recompute_status",       "PSI 재계산"),
]

@router.get("/status")
async def get_background_status() -> list[dict]:
    redis = await get_redis()
    result = []
    for task_id, redis_key, label in TASK_KEYS:
        raw = await redis.get(redis_key)
        if raw:
            data = json.loads(raw)
            data["id"] = task_id
            data["label"] = label
        else:
            data = {
                "id": task_id,
                "label": label,
                "status": "idle",
                "progress": 0,
                "total": 0,
                "processed": 0,
                "started_at": None,
                "finished_at": None,
                "error": None,
            }
        result.append(data)
    return result
```

### 11-A-3. main.py에 라우터 등록

`backend/main.py`에서:
```python
from api.routes import background
app.include_router(background.router, prefix="/api/v1/background", tags=["background"])
```

---

## Task 11-B: PartList 재계산 Background 함수

`backend/services/part_list_service.py`에 추가:

```python
async def recompute_background(engine, dates: list, batch_id: int):
    """Background에서 PartList 재계산, Redis에 진행 상태 기록"""
    from sqlalchemy.orm import sessionmaker
    from sqlalchemy.ext.asyncio import AsyncSession
    from core.redis_client import get_redis
    from datetime import datetime
    import json

    redis = await get_redis()
    STATUS_KEY = "partlist:recompute_status"

    async def set_status(status, progress, processed, total, error=None):
        await redis.set(STATUS_KEY, json.dumps({
            "status": status,
            "progress": progress,
            "total": total,
            "processed": processed,
            "label": "소요자재 재계산",
            "started_at": datetime.utcnow().isoformat(),
            "finished_at": datetime.utcnow().isoformat() if status in ("done", "failed") else None,
            "error": error,
        }))

    await set_status("running", 0, 0, len(dates))

    try:
        AsyncSessionLocal = sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)
        async with AsyncSessionLocal() as session:
            total = len(dates)
            for i, d in enumerate(dates):
                await recompute_for_dates(session, [d], batch_id)
                progress = int((i + 1) / total * 100) if total > 0 else 100
                await set_status("running", progress, i + 1, total)

        await set_status("done", 100, len(dates), len(dates))
    except Exception as e:
        await set_status("failed", 0, 0, 0, error=str(e))
```

---

## Task 11-C: PSI 재계산 Background 함수

`backend/services/psi_service.py`에 추가:

```python
async def recompute_all_background(engine):
    """Background에서 PSI 전체 재계산, Redis에 진행 상태 기록"""
    from sqlalchemy.orm import sessionmaker
    from sqlalchemy.ext.asyncio import AsyncSession
    from core.redis_client import get_redis
    from datetime import datetime
    import json

    redis = await get_redis()
    STATUS_KEY = "psi:recompute_status"

    async def set_status(status, progress, processed=0, total=0, error=None):
        await redis.set(STATUS_KEY, json.dumps({
            "status": status,
            "progress": progress,
            "total": total,
            "processed": processed,
            "label": "PSI 재계산",
            "started_at": datetime.utcnow().isoformat(),
            "finished_at": datetime.utcnow().isoformat() if status in ("done", "failed") else None,
            "error": error,
        }))

    await set_status("running", 0)

    try:
        AsyncSessionLocal = sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)
        async with AsyncSessionLocal() as session:
            # 기존 recompute_all 로직 실행
            # (psi_service.recompute_all이 있으면 호출, 없으면 직접 구현)
            await set_status("running", 30)
            await recompute_all(session)  # 기존 동기 함수 재사용
            await set_status("done", 100, total=1, processed=1)
    except Exception as e:
        await set_status("failed", 0, error=str(e))
```

**주의**: `recompute_all(session)` 함수가 `psi_service.py`에 없다면 기존 `recompute_required_for_dates` 등을 호출하는 로직으로 대체할 것. 현재 `import_folder_dp`에서 `await psi_service.recompute_all(session)` 호출이 있으므로 이 함수가 존재한다고 가정한다.

---

## Task 11-D: Import Pipeline 수정

### 대상 파일: `backend/api/routes/import_pipeline.py`

모든 동기 후처리 호출을 `BackgroundTasks`로 교체한다.

#### `process_batch()` 수정

```python
from fastapi import APIRouter, Depends, HTTPException, UploadFile, File, Form, BackgroundTasks
from core.database import engine  # engine import 추가

@router.post("/batches/{batch_id}/process", response_model=BatchRead)
async def process_batch(
    batch_id: int,
    background_tasks: BackgroundTasks,     # 추가
    session: AsyncSession = Depends(get_session)
):
    # ... (기존 batch 조회 로직 유지) ...

    try:
        if batch.target_table == "bom":
            df = bom_parser.parse(file_path)
            # ... 검증 및 import ...
            inserted = await bom_service.import_from_df(session, df, batch.id)
            batch.records_inserted = inserted
            
            # 동기 호출 제거 → Background로 교체
            # 제거: await item_master_service.rebuild_from_bom(session)
            background_tasks.add_task(
                item_master_service.rebuild_from_bom_background, engine
            )
            
        elif batch.target_table == "daily_plan":
            df = daily_plan_parser.parse(file_path)
            # ... 검증 및 import ...
            inserted = await daily_plan_service.import_from_df(session, df, batch.id)
            batch.records_inserted = inserted
            
            dates = await daily_plan_service.get_dates_in_df(df)
            
            # 동기 호출 제거 → Background로 교체
            # 제거: await part_list_service.recompute_for_dates(session, dates, batch.id)
            background_tasks.add_task(
                part_list_service.recompute_background, engine, dates, batch.id
            )
            background_tasks.add_task(
                psi_service.recompute_all_background, engine
            )
        
        batch.status = "success"
        batch.finished_at = datetime.utcnow()
        
    except Exception as e:
        # ... 기존 예외 처리 유지 ...
```

#### `process_multi_batch()` 수정

```python
@router.post("/batches/process-multi", response_model=MultiProcessResponse)
async def process_multi_batch(
    batch_ids: list[int],
    background_tasks: BackgroundTasks,   # 추가
    session: AsyncSession = Depends(get_session)
):
```
`process_multi_batch`는 내부에서 `process_batch`를 직접 호출하는 대신, 각 배치를 직접 처리하고 background_tasks를 전달해야 한다. 현재 `await process_batch(batch_id, session)` 호출을 `await process_batch(batch_id, background_tasks, session)` 로 변경하거나 로직을 인라인으로 펼칠 것.

#### `import_folder_bom()` / `import_folder_dp()` 수정

```python
@router.post("/folder/bom")
async def import_folder_bom(
    background_tasks: BackgroundTasks,   # 추가
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
) -> Dict[str, Any]:
    path = settings.BOMDB_PATH
    result = await folder_import_service.scan_and_import_folder(session, path, "bom", current_user.id)
    if result.get("success", 0) > 0:
        # 동기 제거 → Background
        background_tasks.add_task(item_master_service.rebuild_from_bom_background, engine)
    return result

@router.post("/folder/dp")
async def import_folder_dp(
    background_tasks: BackgroundTasks,   # 추가
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
) -> Dict[str, Any]:
    path = settings.DPDB_PATH
    result = await folder_import_service.scan_and_import_folder(session, path, "dp", current_user.id)
    if result.get("success", 0) > 0:
        # 동기 제거 → Background (dates는 service에서 추출 불가하므로 전체 재계산)
        background_tasks.add_task(psi_service.recompute_all_background, engine)
    return result
```

#### `folder_import_service.py` 내 동기 후처리 제거

`scan_and_import_folder()` 함수 내 사후 처리 블록:
```python
# 제거할 부분:
if success > 0:
    if file_type == "bom":
        await item_master_service.rebuild_from_bom(session)   # 제거
    elif file_type == "dp":
        pass  # 유지 (이미 pass임)
```

---

## Task 11-E: BackgroundMonitor.tsx 업그레이드

현재 파일(`/test/LARS/.WebUI/src/components/BackgroundMonitor.tsx`)을 아래로 전면 교체:

```tsx
import { useEffect, useState, useRef } from 'react';
import { apiClient } from '../api/client';

interface TaskStatus {
  id: string;
  label: string;
  status: 'idle' | 'running' | 'done' | 'failed';
  progress: number;
  total: number;
  processed: number;
  error: string | null;
  started_at: string | null;
  finished_at: string | null;
}

export function BackgroundMonitor() {
  const [tasks, setTasks] = useState<TaskStatus[]>([]);
  const [hiddenTasks, setHiddenTasks] = useState<Set<string>>(new Set());
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const hideTimers = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map());

  const poll = async () => {
    try {
      const res = await apiClient.get('/background/status');
      const data: TaskStatus[] = res.data;
      setTasks(data);

      // 완료/실패 태스크는 5초 후 자동 숨김
      for (const task of data) {
        if ((task.status === 'done' || task.status === 'failed') && !hideTimers.current.has(task.id)) {
          const timer = setTimeout(() => {
            setHiddenTasks((prev) => new Set([...prev, task.id]));
            hideTimers.current.delete(task.id);
          }, 5000);
          hideTimers.current.set(task.id, timer);
        }
        // running 상태로 돌아오면 숨김 해제
        if (task.status === 'running') {
          setHiddenTasks((prev) => {
            const next = new Set(prev);
            next.delete(task.id);
            return next;
          });
          const existing = hideTimers.current.get(task.id);
          if (existing) {
            clearTimeout(existing);
            hideTimers.current.delete(task.id);
          }
        }
      }
    } catch { /* ignore */ }
  };

  useEffect(() => {
    poll();
    intervalRef.current = setInterval(poll, 2000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
      hideTimers.current.forEach(clearTimeout);
    };
  }, []);

  // idle이거나 숨겨진 태스크 제외
  const visibleTasks = tasks.filter(
    (t) => t.status !== 'idle' && !hiddenTasks.has(t.id)
  );

  if (visibleTasks.length === 0) return null;

  return (
    <div className="mx-2 mb-2 space-y-1.5">
      {visibleTasks.map((task) => (
        <div
          key={task.id}
          className="p-2.5 bg-gray-800 rounded-lg text-xs text-white border border-gray-700 shadow"
        >
          <div className="flex items-center justify-between mb-1">
            <span className="font-semibold text-gray-200">{task.label}</span>
            {task.status === 'running' && (
              <span className="text-blue-400 text-[10px]">{task.progress}%</span>
            )}
          </div>

          {task.status === 'running' && (
            <>
              <div className="w-full bg-gray-600 rounded-full h-1 mb-1">
                <div
                  className="bg-blue-400 h-1 rounded-full transition-all duration-500"
                  style={{ width: `${task.progress}%` }}
                />
              </div>
              {task.total > 0 && (
                <div className="text-gray-500 text-[10px]">
                  {task.processed.toLocaleString()} / {task.total.toLocaleString()}
                </div>
              )}
            </>
          )}

          {task.status === 'done' && (
            <div className="flex items-center gap-1 text-green-400 font-bold">
              <span>✓</span><span>Complete</span>
            </div>
          )}

          {task.status === 'failed' && (
            <div className="text-red-400">
              ✕ {task.error ?? 'Failed'}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
```

---

## 검증 체크리스트

### 백엔드
- [ ] `GET /api/v1/background/status` 응답에 3개 태스크(item_rebuild, part_list_recompute, psi_recompute) 포함
- [ ] BOM Import 후 즉시 200 응답 반환 (블로킹 없음)
- [ ] 백그라운드에서 `itemmaster:rebuild_status` Redis 키 업데이트되는지 확인
- [ ] DP Import 후 즉시 200 응답 반환
- [ ] 백그라운드에서 `partlist:recompute_status`, `psi:recompute_status` 업데이트되는지 확인
- [ ] `/background/status` 폴링 시 status가 `running` → `done` 순으로 변화 확인

### 프론트엔드
- [ ] BOM Import 시작 → 사이드바 하단에 "ItemMaster 재구성" 카드 표시
- [ ] DP Import 시작 → "소요자재 재계산", "PSI 재계산" 카드 표시
- [ ] 각 카드에 progress bar + 퍼센트 표시
- [ ] 완료 시 "✓ Complete" → 5초 후 사라짐
- [ ] 실패 시 빨간 에러 메시지 표시
- [ ] 여러 태스크 동시 running 시 여러 카드 동시 표시
- [ ] `npm run build` TypeScript 오류 0건

---

## 완료 후

- `Phase11_Coder_Report.md` 작성
- `npm run build` + 프리뷰 재시작
- 백엔드 재시작 (main.py에 background router 추가됨)
- Git commit: `"Phase 11: async background pipeline + unified monitor UI"`
