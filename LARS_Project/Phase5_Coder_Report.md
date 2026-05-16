# Phase 5 Coder Report — PSI 전면 재설계

## 1. 개요
실제 물류 업무 환경(`Expeditor_Public.xlsb`)의 PSI 관리 방식에 맞춰 LARS의 PSI 기능을 전면 재설계하였습니다. 품목별 2행 블록 구조, 담당자 필터, 인라인 재고 편집 및 팔로업 마킹(is_picked) 기능을 포함합니다.

## 2. 완료된 작업 항목

### Task 5-A: DB 스키마 확장
- `item_master` 테이블에 4개 컬럼 추가: `lower_vendor_raw`, `inventory_qty`, `defect_qty`, `is_picked`
- Alembic 마이그레이션 실행 완료 (`cd7af37a0e4e_add_psi_fields_to_item_master.py`)

### Task 5-B: 백엔드 — PSI API 재설계
- **신규 스키마:** `PsiRowFull`, `PsiFilterParams`, `DateHeader` 등 정의
- **서비스 로직:** `build_psi_full_matrix` 구현 (Polars 기반 소요량 집계)
- **API 엔드포인트:** 
  - `GET /api/v1/psi/matrix`: 전체 필터링된 PSI 매트릭스 반환
  - `PUT /api/v1/psi/item/{id}/inventory`: 재고/불량 업데이트
  - `PATCH /api/v1/psi/item/{id}/pick`: 팔로업 마킹 토글
  - `GET /api/v1/psi/models`: 모델 목록 반환

### Task 5-C: 프론트엔드 — PSI 페이지 전면 재설계
- **신규 컴포넌트:** `PSIMatrixFull.tsx` (2행 블록 구조, Sticky 컬럼, 주차별 헤더 병합)
- **필터 패널:** 담당자, Supply Type, 모델 코드, 기준일 필터 구현
- **인라인 편집:** 클릭 시 즉시 수정 및 DB 반영

## 3. 검증 결과
- **백엔드 로직 테스트:** `test_psi_v5.py`를 통해 8,000건 이상의 매트릭스 데이터 생성 및 필터링 정상 작동 확인
- **TypeScript 검증:** `npx tsc --noEmit` 결과 오류 0건
- **Python 문법 검증:** `py_compile` 결과 오류 0건
- **UI 렌더링:** `npm run build`를 통한 빌드 무결성 확인 (Vite Production Build)

## 4. 수정 및 생성된 파일 목록

- `backend/models/item_master.py` (모델 업데이트)
- `backend/schemas/item_master.py` (스키마 업데이트)
- `backend/schemas/psi.py` (신규 스키마 추가)
- `backend/services/psi_service.py` (핵심 로직 구현)
- `backend/api/routes/psi.py` (신규 엔드포인트 추가)
- `backend/alembic/versions/cd7af37a0e4e_add_psi_fields_to_item_master.py` (마이그레이션)
- `.WebUI/src/types/logistics.ts` (타입 정의)
- `.WebUI/src/components/psi/PSIMatrixFull.tsx` (신규 컴포넌트)
- `.WebUI/src/pages/PSIPage.tsx` (페이지 재설계)

## 5. 특이사항
- **성능 최적화:** Polars를 사용하여 수만 건의 소요량 스냅샷을 빠르게 그룹화하여 반환합니다.
- **UI 라이브러리:** `shadcn/ui`의 Checkbox, Select 컴포넌트 부재로 인해 표준 HTML 태그와 Tailwind CSS를 조합하여 일관된 디자인을 유지하면서 빌드 오류를 해결하였습니다.
- **날짜 형식:** 지시서에 따라 D-Day를 `D+0` 라벨로 처리하도록 백엔드와 프론트엔드 간 프로토콜을 통일하였습니다.
