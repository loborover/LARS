# Phase 6 Coder Instructions — 일일 운영 자동화 (Advance_Day + One_Click_Solution)

> 작성일: 2026-05-16
> 작성자: Chief
> 대상: Coder (Gemini)
> 참조: `/test/AutoReport/Expeditor_Modules/Expeditor_DayShift.bas`
> 참조: `/test/AutoReport/Expeditor_Modules/Expeditor_PSI.bas`
> 참조: `LARS_Project/Phase5_Coder_Instructions.md` (PSI 재설계 완료)
> 기준 문서: `LARS_Project/New_LARS_Project.md`

---

## 배경 및 목적

실제 업무에서 매일 아침 다음 순서로 VBA 워크플로우가 실행된다:

```
[매일 아침]
  Step 1. Advance_Day    → 날짜 1일 전진 (D+1이 D-Day로)
  Step 2. ERP에서 DP 파일 수령 → DPDB 폴더에 저장
  Step 3. PSI 피벗 새로 고침 (Power Query)
  Step 4. One_Click_Solution → itemMaster 갱신 + PSI 초기화 + Ticket 생성
```

이 Phase에서는 위 4단계 워크플로우를 LARS 웹 서비스로 완전히 구현한다.
추가로 `/test/AutoReport/BOMDB/` (196개) 및 `/test/AutoReport/DPDB/` (29개) 실데이터를
서버 파일시스템에서 직접 읽어 Import하는 **폴더 기반 Import API**를 구현한다.

---

## VBA 로직 분석 요약

### Advance_Day 핵심 알고리즘
PSI 시트의 각 품목(4행 블록)에 대해:
1. `D-Day`의 소요수량 → `inventory_qty`에 반영 (당일 소요분 차감 기록)
2. 재고 입력행(Row2)을 왼쪽으로 1칸 shift (D+1이 D-Day로 이동, 마지막 열 = 0)
3. 소요 입력행(Row3)을 왼쪽으로 1칸 shift (동일)

→ **LARS 구현:** `psi_records`의 날짜를 하루씩 앞당기고, 만료된 D-Day 기록을 정리

### One_Click_Solution 5단계
1. 수동 입력값 저장 (재고수량, 불량수량 보존)
2. PSI 초기화 (소요량 재계산)
3. Ticket 자동 생성 (부족분 발생 품목)
4. 수동 입력값 복원
5. 신규 Ticket 생성

→ **LARS 구현:** 단일 API 호출로 5단계 자동 실행

---

## Task 목록

---

### Task 6-A: 폴더 기반 Import API

**목표:** 서버 파일시스템의 BOMDB/DPDB 폴더를 직접 스캔하여 일괄 Import한다.

#### 6-A-1: backend/services/folder_import_service.py (신규)

핵심 알고리즘:
```
scan_folder(folder_path, file_type):
  1. folder_path 내 파일 목록 수집
     - BOM: "*.xlsx" 패턴 (BOMDB)
     - DP:  "Excel_Export_*.xlsx", "Production_Plan_*.csv" (DPDB)
  
  2. import_batches 테이블에서 이미 처리된 파일명 조회
     (status='success'이고 source_name이 동일한 파일)
  
  3. 신규 파일만 필터링 (미처리 또는 수정시간 변경)
  
  4. 각 파일에 대해:
     a. ImportBatch 레코드 생성 (status='processing')
     b. 기존 parser(bom_parser / daily_plan_parser) 호출
     c. 검증 후 DB upsert
     d. 성공/실패 결과를 ImportBatch에 기록
  
  5. 결과 요약 반환: {total, success, failed, skipped}
```

**경로 상수 (core/config.py에 추가):**
```python
BOMDB_PATH: str = "/test/AutoReport/BOMDB"
DPDB_PATH: str = "/test/AutoReport/DPDB"
```

#### 6-A-2: api/routes/import_pipeline.py에 엔드포인트 추가

```
POST /api/v1/import/folder/bom
  → BOMDB_PATH 스캔 → 신규 BOM 파일 일괄 Import
  → BOM Import 완료 후 rebuild_from_bom() 자동 실행
  → 결과: {total, success, failed, skipped, files: [...]}

POST /api/v1/import/folder/dp
  → DPDB_PATH 스캔 → 신규 DP 파일 일괄 Import
  → DP Import 완료 후 PSI required_qty 재계산 트리거
  → 결과: {total, success, failed, skipped, files: [...]}
```

권한: `manager` 이상

---

### Task 6-B: Advance_Day API

**목표:** 날짜를 1일 전진시키는 일일 운영 API를 구현한다.

#### 핵심 알고리즘

```
POST /api/v1/psi/advance-day

처리 순서:
1. 오늘 날짜(today) 기준 D-Day 레코드 조회
   SELECT * FROM psi_records WHERE psi_date = today

2. 각 item의 D-Day required_qty를 inventory_qty에서 차감
   (D-Day 소요분이 실제로 사용됐다고 간주)
   item_master.inventory_qty -= psi_records.required_qty (where psi_date = today)
   단, inventory_qty가 음수가 되지 않도록 max(0, ...) 처리

3. 만료된 D-Day 이전 레코드 삭제
   DELETE FROM psi_records WHERE psi_date < today

4. PSI 행렬의 날짜 범위를 tomorrow(D-Day) ~ D+30으로 재설정
   (기존 레코드는 유지, 신규 날짜 슬롯만 추가)

5. 결과 반환: {advanced_date: today, items_processed: N, next_d_day: tomorrow}
```

권한: `manager` 이상

---

### Task 6-C: One_Click_Solution API

**목표:** 매일 아침 실행하는 5단계 워크플로우를 단일 API로 구현한다.

```
POST /api/v1/psi/one-click

실행 순서 (각 단계 실패 시 중단하지 않고 계속 진행, 결과에 단계별 상태 기록):

Step 1. Advance_Day 실행
  → 날짜 1일 전진

Step 2. DPDB 폴더 스캔 및 신규 DP Import
  → /import/folder/dp 로직 실행

Step 3. PSI required_qty 재계산
  → psi_service.recompute_all() 실행
  → 모든 item_master 품목에 대해 part_list_snapshots 기준 소요량 재집계

Step 4. 부족분 Ticket 자동 생성
  → psi_records에서 shortage_qty > 0 인 레코드 조회
  → 기존 open Ticket이 없는 품목에 한해 신규 Ticket 생성
  → category='psi_alert', priority 결정 기준:
      shortage_qty > 100 → 'critical'
      shortage_qty > 50  → 'high'
      나머지             → 'normal'

Step 5. WebSocket 브로드캐스트
  → Dashboard에 완료 알림 전송

반환 형식:
{
  "steps": [
    {"step": 1, "name": "advance_day", "status": "ok", "detail": "..."},
    {"step": 2, "name": "dp_import", "status": "ok", "detail": "success:3, skipped:26"},
    {"step": 3, "name": "psi_recompute", "status": "ok", "detail": "items:86"},
    {"step": 4, "name": "ticket_create", "status": "ok", "detail": "created:5"},
    {"step": 5, "name": "broadcast", "status": "ok"}
  ],
  "elapsed_sec": 12.4
}
```

권한: `manager` 이상

---

### Task 6-D: 프론트엔드 — 일일 운영 UI

#### 6-D-1: Dashboard 페이지 상단에 "하루 시작" 버튼 추가

버튼 클릭 시:
1. `POST /api/v1/psi/one-click` 호출
2. 5단계 진행 상황을 단계별로 표시 (Step 1/5 → 2/5 → ... → 완료)
3. 완료 후 결과 요약 팝업 (성공/실패 단계, 생성된 Ticket 수)

컴포넌트: `OnClickSolution.tsx` (신규)
- 버튼: "📋 하루 시작 (One-Click)" — manager 이상만 표시
- 로딩 상태: Step별 체크리스트 (✅ 완료 / ⏳ 진행 중 / ❌ 실패)

#### 6-D-2: Import 페이지에 폴더 Import 섹션 추가

기존 파일 업로드 UI 아래에 새 섹션 추가:

```
[서버 폴더 Import]
  ┌─────────────────────────────────────────────────┐
  │  BOM 폴더 Import   [스캔 및 Import 실행]        │
  │  /test/AutoReport/BOMDB (196개 파일 감지됨)     │
  ├─────────────────────────────────────────────────┤
  │  DP 폴더 Import    [스캔 및 Import 실행]         │
  │  /test/AutoReport/DPDB (29개 파일 감지됨)       │
  └─────────────────────────────────────────────────┘
```

각 버튼 클릭 시 처리 결과를 테이블로 표시:
- 파일명 / 상태(성공/실패/건너뜀) / 적재 건수

---

### Task 6-E: 통합 검증

순서대로 실행한다:

1. **폴더 Import 검증**
   ```
   POST /api/v1/import/folder/bom
   → 196개 중 신규 파일 처리 수 확인
   → BOM 모델 목록 조회: GET /api/v1/bom/models
   
   POST /api/v1/import/folder/dp
   → 신규 DP 파일 처리 수 확인
   → PSI 매트릭스 확인: GET /api/v1/psi/matrix
   ```

2. **Advance_Day 검증**
   ```
   POST /api/v1/psi/advance-day
   → 반환값 확인 (advanced_date, items_processed)
   → psi_records 날짜 범위 변경 확인
   ```

3. **One_Click_Solution 검증**
   ```
   POST /api/v1/psi/one-click
   → 5단계 결과 확인
   → 생성된 Ticket 확인: GET /api/v1/tickets
   → Dashboard WebSocket 알림 확인
   ```

4. **프론트엔드 검증**
   - Dashboard "하루 시작" 버튼 클릭 → 단계별 UI 확인
   - Import 페이지 폴더 Import 섹션 동작 확인
   - TypeScript 오류 0건: `npx tsc --noEmit`

5. **빌드 확인**
   ```bash
   cd /test/LARS/.WebUI && npm run build
   ```

---

## 구현 시 주의사항

1. **Polars 전용** — DataFrame 연산에 Pandas 사용 금지
2. **파일 중복 처리 방지** — `import_batches`의 `source_name` + `status='success'` 조합으로 이미 처리된 파일 스킵
3. **원자성** — One_Click_Solution의 각 Step은 독립적으로 실행. 한 Step 실패가 전체를 중단시키지 않음
4. **inventory_qty 하한** — Advance_Day 시 음수 방지: `max(0, inventory_qty - required_qty)`
5. **One_Click_Solution 권한** — `manager` 이상만 실행 가능 (무분별한 실행 방지)
6. **config.py 경로** — BOMDB_PATH, DPDB_PATH는 환경변수로 오버라이드 가능하도록 `Optional[str]`로 설계

---

## 완료 보고 형식

작업 완료 후 `LARS_Project/Phase6_Coder_Report.md`를 작성하여 제출한다.
보고서에는 완료 항목, 검증 결과(특히 BOMDB/DPDB Import 수량), 특이사항, 수정 파일 목록을 포함한다.
