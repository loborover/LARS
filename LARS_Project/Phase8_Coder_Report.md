# Phase 8 Coder Report — Daily Plan 뷰어 재설계 완료

## 1. 개요
기존의 2-panel 구조(목록-상세)에서 탈피하여, 실무에서 가장 많이 사용되는 **날짜 기준 생산 계획 뷰**로 전면 재설계하였습니다. 웹 브라우저에서의 모니터링과 생산 현장 배포를 위한 인쇄 기능을 모두 구현하였습니다.

## 2. 작업 내용

### 2.1 백엔드 (FastAPI)
- **Schema 추가**: `DailyLotView`, `DailyLineView`, `DailyPlanViewResponse` 등 집계 데이터 전달을 위한 전용 스키마 정의 (`backend/schemas/daily_plan.py`)
- **Service 구현**: `daily_plan_service.get_daily_view()` 구현. Polars JOIN 연산 대신 SQLAlchemy를 사용하여 라인별/로트별 데이터를 효율적으로 그룹화하고 `daily_qty_json`에서 해당 날짜의 수량만 정밀하게 추출합니다. (`backend/services/daily_plan_service.py`)
- **API Endpoint**:
    - `GET /api/v1/dp/dates`: 데이터가 존재하는 유효 날짜 목록 반환 (달력용)
    - `GET /api/v1/dp/daily`: 특정 날짜의 라인별 생산 계획 매트릭스 반환
    - 라우팅 우선순위를 조정하여 `/daily`가 `/{plan_id}` 변수에 매칭되지 않도록 보장하였습니다.

### 2.2 프론트엔드 (React + Vite)
- **타입 정의**: 백엔드 응답 규격에 맞춘 TypeScript 인터페이스 추가 (`src/types/logistics.ts`)
- **컴포넌트 개발**:
    - `DailyPlanViewer`: 웹 환경에 최적화된 라인별 그룹화 테이블. 소계 및 합계 강조.
    - `DailyPlanPrint`: 인쇄 최적화 뷰. `@media print` CSS를 사용하여 버튼, 사이드바를 숨기고 정갈한 격자 무늬 테이블로 렌더링. `window.print()` 연동.
- **페이지 재설계**: 날짜 및 라인별 필터바, 웹/인쇄 탭 네비게이션을 포함한 통합 대시보드 형태로 `DailyPlanPage` 전면 개편.

## 3. 검증 결과

### 3.1 API 응답 샘플 (`/daily?date=2026-05-14`)
```json
{
  "date": "2026-05-14",
  "total_qty": 976.0,
  "lines": [
    {
      "line_code": "C11",
      "line_name": "C11",
      "total_daily_qty": 866.0,
      "lots": [ ... 30개 항목 ... ]
    },
    ...
  ]
}
```

### 3.2 빌드 및 정적 분석
- **TypeScript**: `npx tsc --noEmit` 결과 오류 0건.
- **Vite Build**: `npm run build` 성공 (488.30 kB JS bundle).
- **Backend**: Python 컴파일 체크 및 유닛 실행 확인 완료.

## 4. 특이사항 및 향후 과제
- **DUMMY 라인 필터링**: 지시사항에 따라 `line_code='DUMMY'`인 데이터는 실제 뷰에서 제외 처리하였습니다.
- **인쇄 UX**: 대시보드 우측 상단의 '인쇄 뷰어' 탭을 통해 인쇄 시뮬레이션을 확인한 후 즉시 인쇄가 가능합니다.
