# Phase 15 Coder Report — DP 구조 수정 및 시스템 상태 표시줄 완료

## 1. 개요
사용자의 반복 지적 사항인 Daily Plan(DP) 테이블의 정보 누락(Line, Suffix)을 해결하고, 시스템의 주요 상태(DB, AI, 서버 시간)를 상시 모니터링할 수 있는 상태 표시줄을 도입하였습니다.

## 2. 작업 상세

### 2.1 Daily Plan 데이터 구조 개선 (Task 15-A, 15-B)
- **DB 스키마 확장**: `daily_plan_lots` 테이블에 `suffix` 컬럼을 추가하였습니다. (Alembic 마이그레이션 `daily_plan_lot_add_suffix` 적용 완료)
- **서비스 레이어 수정**: DP 임포트 시 파일에서 파싱된 `suffix`를 DB에 직접 저장하도록 `daily_plan_service.py`를 수정하였습니다.
- **API 고도화**: `GET /api/v1/dp/lots-raw` API가 `ProductionLine`과 조인하여 `line_code`를 반환하도록 수정하였으며, 모델 번호 표시 시 `lot.suffix`를 우선적으로 결합하여 항상 `Model.Suffix` 형식을 유지하도록 개선하였습니다.

### 2.2 프론트엔드 DP 뷰어 개편 (Task 15-C)
- **컬럼 재배치**: "Line" 열을 가장 좌측 첫 번째 컬럼으로 배치하여 생산 라인별 구분을 명확히 하였습니다.
- **Suffix 표시 보장**: `model_number` 필드를 사용하여 BOM 연결 여부와 관계없이 DP 파일의 Suffix가 테이블에 정확히 표시되도록 하였습니다.
- **레이아웃 보정**: 신규 컬럼 추가에 따라 하단 합계 행(Grand Total)의 `colSpan`을 4로 조정하여 테이블 레이아웃 무너짐을 방지하였습니다.

### 2.3 시스템 상태 표시줄 도입 (Task 15-E)
- **헬스 체크 API**: `backend/api/routes/health.py`를 신규 생성하여 DB 연결 상태와 AI API 설정 여부를 체크하는 엔드포인트를 마련하였습니다.
- **SystemStatusBar 컴포넌트**: 사이드바 최하단에 상주하며 DB/AI 상태(신호등 표시) 및 실시간 서버 시간을 표시하는 UI를 구현하였습니다. 사이드바 접힘 모드에서도 상태 점(Dot)은 상시 노출됩니다.

### 2.4 UI/UX 검증 및 수정 (Task 15-D, 15-F)
- **BOM Suffix 검증**: `BOMListPage`와 `BOMDetailPage`의 렌더링 로직을 전수 점검하여 Suffix가 있는 경우 하이라이트 표시가 정상 작동함을 확인하였습니다.
- **BackgroundMonitor 확인**: `/background/status` API와의 연동을 재확인하였으며, Redis 상태 값에 따라 PSI 재계산 등의 작업이 실시간으로 인디케이터에 표시됨을 검증하였습니다.

## 3. 검증 결과
- **백엔드**: `/health/status` 및 `/dp/lots-raw` API 응답 정상 확인.
- **프론트엔드**: TypeScript 오류 0건 및 Vite 빌드/프리뷰 성공.
- **API 응답 샘플 (`/dp/lots-raw`)**:
  ```json
  {
    "line_code": "C11",
    "wo_number": "WO-20260516-001",
    "model_number": "LSGL6335X.ASTLLGA",
    "planned_qty": 100,
    "remain_qty": 100,
    "daily_qty": {"2026-05-16": 50.0}
  }
  ```

## 4. 특이사항
- 신규 추가된 `suffix` 컬럼은 이후 수행되는 DP Import부터 데이터가 적재됩니다. 기존 데이터는 빈 문자열로 표시됩니다.
