# Phase 9 Coder Report — 시스템 고도화 및 UI/UX 개선 완료

## 1. 개요
Phase 9에서는 BOM 모델 식별 체계의 정밀화(Suffix 통합), 백그라운드 작업 모니터링 강화, 그리고 전사적인 UI/UX 일관성(Sticky Header, Tutorial 시스템) 확보를 목표로 작업을 수행하였습니다.

## 2. 작업 상세

### 2.1 BOM Model + Suffix 통합 (Task 9-A)
- **DB 스키마 변경**: `bom_models` 테이블에 `suffix` 컬럼을 추가하고, `(model_code, suffix)` 복합 유니크 제약 조건을 적용하였습니다.
- **Alembic 마이그레이션**: `04afd703e75e_bom_model_add_suffix.py` 생성 및 적용 완료.
- **Backend 전파**:
    - `BomModelRead` 스키마에 `model_number` 계산 필드 추가 (`model_code.suffix` 형식).
    - `bom_service`의 모든 조회/임포트 로직이 Suffix를 고려하도록 수정.
    - `item_master_service`, `daily_plan_service` 등 연관 서비스에서 모델 참조 시 Suffix를 포함하도록 고도화.
- **API**: `/bom/models/{model_number:path}` 경로를 통해 `.`이 포함된 모델 번호도 정상 조회 가능하도록 처리.

### 2.2 Background Process Monitor (Task 9-B)
- **컴포넌트 개발**: `.WebUI/src/components/BackgroundMonitor.tsx` 신규 생성.
- **기능**: ItemMaster Rebuild 등 시간이 걸리는 백그라운드 작업의 진행률을 2초 간격 폴링으로 실시간 표시합니다. 완료/실패 시 5초 후 자동 숨김 처리됩니다.
- **레이아웃 통합**: 사이드바 최하단에 고정 배치하여 모든 페이지에서 진행 상태를 확인할 수 있습니다.

### 2.3 UI/UX 개선 (Task 9-C, 9-D)
- **Sticky Header & Layout**:
    - `BOMListPage`, `BOMDetailPage`, `DailyPlanPage`, `ItemMasterPage`, `PSIPage`, `PartListPage` 전면에 적용.
    - 제목과 필터 영역은 `z-20` 레이어로 상단 고정, 테이블 헤더는 `z-10` 레이어로 그 아래 고정되어 대량 데이터 스크롤 시에도 맥락을 유지합니다.
- **Tutorial 시스템**:
    - `useTutorial` 훅 및 `TutorialBox` 컴포넌트 구현.
    - `localStorage`를 사용하여 사용자의 도움말 숨김 설정을 브라우저 세션 간 유지합니다.
    - 모든 주요 페이지 상단에 맞춤형 가이드를 배치하고, 숨겼을 때도 '도움말 보기' 버튼을 통해 재열람 가능하게 구현하였습니다.

## 3. 검증 결과
- **백엔드**: Alembic upgrade 성공 및 Suffix 기반 데이터 정합성 확인.
- **프론트엔드**: `npx tsc --noEmit` 결과 오류 0건, `npm run build` 성공.
- **동작 확인**:
    - `/api/v1/bom/models` 응답에 `model_number` 포함 확인.
    - 사이드바 모니터 UI 정상 렌더링 확인.
    - 도움말 토글 및 Sticky 레이아웃 동작 확인.

## 4. 특이사항
- `alembic` 자동 생성 시 `sqlmodel` 모듈 참조 오류가 발생하여, 마이그레이션 파일 내 타입을 `sa.String()` 및 `sa.Float()`으로 수동 보정하여 해결하였습니다.
