# Phase 18 Coder Report — DP Batch 삭제 및 출처 관리 완료

## 1. 개요
Daily Plan(DP) 데이터의 생명주기 관리를 위해 특정 배치(Import Batch)를 삭제할 수 있는 기능을 구현하고, 데이터의 출처(Local/ERP)를 구분하여 시각화할 수 있도록 시스템을 고도화하였습니다.

## 2. 작업 상세

### 2.1 DB 스키마 확장 및 Migration (Task 18-A, 18-B)
- **컬럼 추가**: `import_batches` 테이블에 `data_source` (VARCHAR) 컬럼을 추가하였습니다.
- **Migration**: 직접 수동 마이그레이션 파일(`f1a8e1b9_add_data_source_to_import_batches.py`)을 작성하고 `alembic upgrade head`를 통해 적용 완료하였습니다. 기본값은 `'local'`로 설정되었습니다.
- **모델 반영**: `ImportBatch` SQLModel 클래스에 `data_source` 필드를 추가하였습니다.

### 2.2 백엔드 API 고도화 (Task 18-C, 18-E)
- **삭제 API 구현**: `DELETE /api/v1/dp/batches/{batch_id}` 엔드포인트를 추가하였습니다.
    - **Cascade 삭제**: 데이터 무결성을 위해 `PartListSnapshot` → `DailyPlanLot` → `DailyPlan` (참조가 없는 경우) → `ImportBatch` 순서로 수동 삭제 로직을 구현하였습니다.
    - **Redis 연동**: 삭제된 배치가 현재 'Target'으로 설정되어 있었다면 Redis에서 해당 정보를 자동으로 초기화합니다.
- **조회 API 수정**: `GET /api/v1/dp/batches` 응답에 `data_source` 필드를 포함하여 프론트엔드에서 태그를 표시할 수 있게 하였습니다.

### 2.3 데이터 출처 추적 (Task 18-D)
- **Local 태그 자동 설정**: `folder_import_service.py`와 `import_pipeline.py`의 모든 임포트 경로에 `data_source="local"` 설정을 추가하여 수동으로 유입된 데이터임을 명시하였습니다.

### 2.4 프론트엔드 UI 개편 (Task 18-F)
- **출처 태그**: 배치 카드 우측 상단에 "Local" (회색) 또는 "ERP" (보라색) 배지를 표시하여 데이터의 성격을 한눈에 파악할 수 있습니다.
- **배치 삭제 UX**: 
    - 각 배치 카드에 휴지통 아이콘(Trash2)을 배치하였습니다.
    - 클릭 시 즉시 삭제되지 않고 "확인 삭제 / 취소" 버튼으로 전환되는 인라인 확인 UI를 적용하여 실수를 방지하였습니다.
    - **보안**: 현재 시스템의 기준인 'Target' 배치는 삭제할 수 없도록 버튼을 비활성화 처리하였습니다.
- **BOM 연동**: DP 테이블의 모델 번호가 실제 BOM에 등록되어 있는지 대조하여, 미등록 모델은 빨간색으로 표시하고 등록된 모델은 더블클릭 시 BOM 상세로 즉시 이동하는 기능을 통합하였습니다.

## 3. 검증 결과
- **DB**: `alembic upgrade` 성공 및 `data_source` 컬럼 생성 확인.
- **API**: `/dp/batches` 응답에 출처 정보 포함 및 `DELETE` 호출 시 연관 데이터(Lot, Snapshot)의 물리적 삭제 확인 완료.
- **프론트엔드**: `npx tsc --noEmit` 결과 오류 0건 및 Vite 빌드 성공.

## 4. 특이사항
- `DailyPlanPage.tsx`를 전면 교체하면서 기존의 필터 및 탭 전환 로직과 신규 삭제/태그 기능을 완벽하게 통합하였습니다.
