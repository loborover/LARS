# Phase 10 Coder Report — DP Viewer 전면 재설계 완료

## 1. 개요
사용자의 "Excel Sheet처럼 Raw 데이터를 보고 싶다"는 요구사항에 맞춰 Daily Plan Viewer를 완전히 재설계하였습니다. 이제 날짜별 상세 뷰가 아닌, 특정 Import Batch를 선택하여 전체 로트를 Flat한 테이블 형태로 한눈에 확인할 수 있습니다.

## 2. 작업 상세

### 2.1 백엔드 API 고도화 (Task 10-A)
- **Batch 목록 API**: `GET /api/v1/dp/batches` 구현. 업로드된 DP 파일 목록, 날짜 범위, 현재 Target 여부를 반환합니다.
- **Raw Lots API**: `GET /api/v1/dp/lots-raw` 구현. 특정 배치의 모든 로트 데이터를 Flat하게 반환하며, W/O가 없는 행은 자동으로 필터링합니다. Suffix 정보를 포함한 모델 번호와 날짜별 수량(`daily_qty`)을 딕셔너리 형태로 제공합니다.
- **Target 설정 API**: `POST /api/v1/dp/set-target` 및 `GET /api/v1/dp/target-batch` 구현. Redis를 사용하여 현재 시스템 전체(PSI, PartList 등)에서 기준으로 삼을 DP 배치를 관리합니다.

### 2.2 프론트엔드 UI 전면 개편 (Task 10-B)
- **2-Panel 레이아웃**: 좌측에는 DP 배치 목록을, 우측에는 선택된 배치의 상세 데이터를 표시하는 현대적인 인터페이스를 도입하였습니다.
- **Dynamic Date Columns**: 각 로트의 `daily_qty_json` 데이터를 분석하여 테이블 컬럼을 동적으로 생성합니다.
- **Excel-like View**: PST, W/O, Model.Suffix, Lot Qty, Remain Qty 및 일자별 수량을 한 행에 표시하여 데이터 가독성을 극대화했습니다.
- **Visual Feedback**: 잔량(Remain)이 남은 경우 주황색으로 강조하고, 전체 합계(Grand Total) 행을 하단에 고정(Sticky) 배치하였습니다.

### 2.3 시스템 통합 (Task 10-C)
- **Target DP 연동**: `psi_service.py`와 `part_list_service.py`가 데이터를 집계할 때 Redis에 설정된 `dp:target_batch_id`를 참조하도록 수정하였습니다. 이를 통해 여러 DP 파일 중 사용자가 지정한 "확정 계획"을 기준으로 모든 계산이 이루어집니다.

### 2.4 정리 작업 (Task 10-D)
- Phase 8에서 사용되었던 `DailyPlanViewer.tsx` 및 `DailyPlanPrint.tsx` 컴포넌트를 삭제하여 코드 베이스를 간결하게 유지하였습니다.

## 3. 검증 결과
- **백엔드**: Python 구문 체크 통과 및 신규 엔드포인트 4종 구현 완료.
- **프론트엔드**: `npx tsc --noEmit` 결과 오류 0건, Vite 빌드 성공.
- **UX**: 좌측 배치 선택 시 우측 테이블 즉시 갱신 및 Target 설정 기능 정상 동작 확인.

## 4. 특이사항
- 기존의 `/dp/daily`, `/dp/dates` API는 하위 호환성을 위해 유지하였습니다.
- `DailyPlanPage` 상단에 `TutorialBox`를 배치하여 신규 레이아웃 사용법을 안내하였습니다.
