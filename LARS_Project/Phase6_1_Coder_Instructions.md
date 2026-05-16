# Phase 6.1 Coder Instructions — DP Import FK 버그 수정

> 작성일: 2026-05-16
> 작성자: Chief
> 대상: Coder (Gemini)
> 참조: `LARS_Project/Phase6_Coder_Report.md`

---

## 배경 및 목적

Phase 6 완료 후 `POST /api/v1/import/folder/dp` 실행 시 7개 파일이 모두 실패하는 버그가 확인되었다.  
원인 분석 결과 두 가지 문제가 존재한다.

---

## 버그 1 (Critical) — FK 제약 위반으로 DP 재임포트 전체 실패

### 증상

```json
{
  "filename": "Production_Plan_Assembly[R+F]_20260514074251_CVZ.csv",
  "status": "failed",
  "message": "ForeignKeyViolationError: update or delete on table 'daily_plan_lots' 
              violates foreign key constraint 'part_list_snapshots_lot_id_fkey'"
}
```

### 원인

`daily_plan_service.py` line 48:

```python
await session.execute(delete(DailyPlanLot).where(DailyPlanLot.plan_id == plan.id))
```

`part_list_snapshots.lot_id`가 `daily_plan_lots.id`를 FK로 참조하고 있어,  
PSI 계산(PL 생성)이 한 번이라도 실행된 이후 DP 재임포트 시 항상 이 에러가 발생한다.

### 수정 방법

`backend/services/daily_plan_service.py`의 `import_from_df()` 함수 수정:

**수정 위치:** "Delete existing DailyPlanLots" 주석 앞에 PartListSnapshot 먼저 삭제 추가

**알고리즘:**
```
1. DailyPlanLot.plan_id = plan.id 인 lot들의 ID 목록을 서브쿼리로 구성
2. part_list_snapshots WHERE lot_id IN (서브쿼리) 먼저 DELETE
3. 그 다음 daily_plan_lots DELETE (기존 로직 그대로 유지)
```

**import 추가 필요:**
```python
from models.part_list import PartListSnapshot
```

**핵심 코드 변경 (기존 line 48 위치):**
```python
# 1. 먼저 FK 참조 레코드(part_list_snapshots) 삭제
lot_ids_subq = select(DailyPlanLot.id).where(DailyPlanLot.plan_id == plan.id)
await session.execute(
    delete(PartListSnapshot).where(PartListSnapshot.lot_id.in_(lot_ids_subq))
)
# 2. 그 다음 lots 삭제 (기존 코드)
await session.execute(delete(DailyPlanLot).where(DailyPlanLot.plan_id == plan.id))
```

---

## 버그 2 (Non-critical) — 다른 형식의 ERP 내보내기 파일

### 증상

```json
{
  "filename": "Production_Plan_Assembly[R+F]_20260512110234_CVZ.csv",
  "status": "failed",
  "message": "CSV missing required column: model_code"
}
```

### 원인

해당 파일(`20260512110234`)은 한국어 날짜 헤더(`05월 12일`) 및 다른 컬럼 구조를 가진  
별도 ERP 내보내기 형식이다. 현재 파서가 지원하지 않는 포맷이다.

실제 파일 헤더:
```
Line, Demand ID, Model.Suffix, PST, Lot Qty, Remain Qty, 05월 12일, ...
```
(표준 파일 헤더: `Plant, Target ID, Line, Update, Demand ID, Model, Suffix, PST, ...`)

### 수정 방법

`backend/parsers/daily_plan_parser.py`의 `parse_csv()` 함수 앞부분에  
지원하지 않는 형식 감지 로직 추가:

**알고리즘:**
```
parse_csv(file_path):
  1. 헤더 행을 읽어서 "Model" 컬럼이 없고 "Model.Suffix" 컬럼도 없으면
     → raise ValueError("CSV missing required column: model_code")  [현재 동작 유지]
  
  2. "Model" 컬럼이 없고 "Model.Suffix"는 있지만,
     날짜 컬럼이 MM/DD 형식이 아닌 경우(한국어 인코딩 등)
     → raise ValueError("Unsupported CSV format: Korean date headers detected. 
                        Use English ERP export format.")
```

**감지 조건:**
- 날짜 컬럼 감지: `re.match(r"\d{2}/\d{2}", col.strip())`가 하나도 매칭 안 되면 비표준 형식
- 에러 메시지에 명확한 안내 포함

---

## Task 목록

### Task 6.1-A: daily_plan_service.py FK 수정 (Critical)

수정 파일: `backend/services/daily_plan_service.py`

1. 상단 import에 `from models.part_list import PartListSnapshot` 추가
2. `import_from_df()` 내 "Delete existing DailyPlanLots" 구역에 PartListSnapshot 먼저 삭제하는 코드 삽입

### Task 6.1-B: daily_plan_parser.py 비표준 형식 에러 메시지 개선 (Minor)

수정 파일: `backend/parsers/daily_plan_parser.py`

1. `parse_csv()` 내 날짜 컬럼 감지 후 컬럼이 0개면 → 명확한 에러 메시지 반환
2. `model_code` 컬럼 없을 때 에러 메시지에 "Use English ERP export format" 안내 추가

---

## 통합 검증

```bash
# 1. FK 수정 후 폴더 DP 임포트 재실행
POST /api/v1/import/folder/dp
→ 기대값: success: 7, failed: 0, skipped: 1 (20260512110234 skipped or failed with clear message)

# 2. Python 문법 검증
cd /test/LARS/backend && source venv/bin/activate
python3 -m py_compile services/daily_plan_service.py
python3 -m py_compile parsers/daily_plan_parser.py

# 3. 백엔드 재시작 후 재검증
nohup uvicorn main:app --host 0.0.0.0 --port 8000 > /tmp/lars_backend.log 2>&1 &
```

---

## 완료 보고 형식

작업 완료 후 `LARS_Project/Phase6_1_Coder_Report.md`를 작성하여 제출한다.
