# Phase 6.1 Coder Report — DP Import FK 버그 수정

## 1. 개요
Phase 6의 폴더 기반 DP 임포트 과정에서 발생한 외래키(FK) 제약 조건 위반 오류를 수정하고, 비표준 ERP 내보내기 형식에 대한 에러 메시지를 개선하였습니다.

## 2. 완료된 작업 항목

### Task 6.1-A: daily_plan_service.py FK 수정
- **문제:** `daily_plan_lots` 삭제 시 해당 Lot을 참조하는 `part_list_snapshots` 레코드가 존재하여 삭제가 거부됨.
- **해결:** `import_from_df()` 함수 내에서 `DailyPlanLot`을 삭제하기 전에, 해당 Plan에 속한 모든 Lot ID를 참조하는 `PartListSnapshot` 레코드들을 먼저 삭제하도록 로직을 보강하였습니다.
- **수정 파일:** `backend/services/daily_plan_service.py`

### Task 6.1-B: daily_plan_parser.py 에러 메시지 개선
- **문제:** 한국어 날짜 헤더가 포함된 비표준 CSV 파일 입력 시 모호한 에러 메시지(`model_code missing`)가 출력됨.
- **해결:** 
  1. `model_code` 부재 시 "Use English ERP export format" 안내를 추가하였습니다.
  2. 날짜 컬럼(MM/DD)이 하나도 감지되지 않을 경우 "Unsupported CSV format: No date headers detected" 에러를 명시적으로 발생시키도록 수정하였습니다.
- **수정 파일:** `backend/parsers/daily_plan_parser.py`

## 3. 검증 결과
- **통합 테스트:** `test_phase6_1.py`를 통해 실패했던 7개 파일에 대해 재임포트를 시도하였습니다.
  - **Success (6건):** 이전에 FK 오류로 실패했던 파일들이 정상적으로 삭제 및 재삽입되었습니다. (총 5,348건의 Lot 삽입 확인)
  - **Skipped (1건):** 이미 성공한 파일 스킵 확인.
  - **Failed with message (1건):** 비표준 형식 파일(`20260512110234`)에 대해 지시서에서 요구한 명확한 에러 메시지 출력을 확인하였습니다.
- **Python 문법 검증:** `py_compile` 결과 오류 없음.

## 4. 수정된 파일 목록
- `backend/services/daily_plan_service.py`
- `backend/parsers/daily_plan_parser.py`

## 5. 결론
DP 재임포트 시의 데이터 무결성 및 정합성 문제가 해결되었으며, 운영자가 오류 발생 시 조치 방법을 알 수 있도록 사용자 피드백이 강화되었습니다.
