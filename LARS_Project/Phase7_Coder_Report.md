# Phase 7 Coder Report — ItemMaster 강화

## 1. 개요
ItemMaster의 데이터 정밀도와 시스템 성능을 높이기 위해 업체명 파싱, Redis 기반 전역 캐싱, 비동기 Background Rebuild, 그리고 BOM 사용처 역조회 기능을 성공적으로 구현하였습니다.

## 2. 완료된 작업 항목

### Task 7-A: 업체명 파싱 (Computed Fields)
- **파싱 로직:** `EKHQ_업체명_KR12345` 형식에서 실업체명만 추출하는 정규식 패턴(`^[A-Z]+_(.+)_KR\d+$`)을 적용하였습니다.
- **스키마 확장:** `ItemMasterRead`에 `vendor_name`, `lower_vendor_name` 필드를 추가하여 UI에서 가독성 높은 업체명을 즉시 확인할 수 있도록 했습니다.

### Task 7-B & 7-C: Redis 클라이언트 및 캐싱
- **Redis 통합:** `core/redis_client.py`를 신규 생성하고 `main.py`의 lifespan을 통해 연결 관리를 자동화하였습니다.
- **전역 캐싱:** `itemmaster:all` 키를 사용하여 활성 품목 전체를 캐싱(TTL 300초)하며, 검색 시 캐시 데이터를 활용하여 DB 부하를 0으로 줄였습니다. 품목 생성/수정/재구성 시 캐시가 자동으로 무효화됩니다.

### Task 7-D: 조건부 Background Rebuild
- **비동기 처리:** 대규모 BOM 재구성 시 HTTP 타임아웃을 방지하기 위해 `BackgroundTasks`와 자체 `AsyncSession`을 사용하는 비동기 프로세스로 전환하였습니다.
- **진행 상황 폴링:** Redis를 통해 처리 중인 품목 수와 백분율을 실시간으로 기록하며, 프론트엔드에서 이를 폴링하여 시각화합니다.
- **지능형 실행:** 마지막 BOM 임포트 시각과 마지막 재구성 시각을 비교하여 필요한 경우에만 실행되도록 최적화하였습니다.

### Task 7-E: BOM 역조회 강화
- **Polars 집계:** 각 품목이 어떤 상위 모델에서 사용되는지, 총 소요량과 모든 사용 경로(Path)를 Polars로 고속 집계하여 반환하는 기능을 구현하였습니다.

### Task 7-F: 프론트엔드 UI 개선
- **가독성 향상:** `ItemMasterPage`와 `PartListPage`에서 복잡한 원시 업체명 대신 정제된 업체명을 우선 표시합니다.
- **신규 컴포넌트:** 
  - `RebuildProgress.tsx`: 실시간 재구성 진행률 표시 및 트리거 버튼.
  - `BomUsageModal.tsx`: 품목별 상세 사용처(모델, 수량, 경로) 조회 모달.

## 3. 검증 결과
- **백엔드 로직 테스트:** 
  - 업체명 파싱: `EKHQ_서브원_...` → `서브원` 변환 정상 확인.
  - Redis 캐싱: 9,709건의 품목 데이터 캐싱 및 조회 성공.
  - BOM 역조회: 특정 품목(ID 1)에 대해 82개 모델에서의 사용 내역 집계 확인.
- **TypeScript 검증:** `npx tsc --noEmit` 결과 오류 0건.
- **빌드 검증:** Vite 프로덕션 빌드 성공.

## 4. 수정 및 생성된 파일 목록
- `backend/core/redis_client.py` (신규)
- `backend/core/database.py` (engine alias 추가)
- `backend/main.py` (Redis lifespan 추가)
- `backend/schemas/item_master.py` (computed 필드 및 역조회 스키마)
- `backend/services/item_master_service.py` (핵심 로직 및 비동기 처리)
- `backend/api/routes/items.py` (재구성 및 상태 조회 엔드포인트)
- `.WebUI/src/components/items/RebuildProgress.tsx` (신규)
- `.WebUI/src/components/items/BomUsageModal.tsx` (신규)
- `.WebUI/src/pages/ItemMasterPage.tsx` (UI 통합)
- `.WebUI/src/pages/PartListPage.tsx` (업체명 표시 수정)

## 5. 특이사항
- Redis 연결 실패 시 자동으로 DB 조회로 전환되는 Fallback 로직을 적용하여 시스템 안정성을 확보하였습니다.
- `shadcn/ui` 라이브러리 부재 상황을 고려하여 Tailwind CSS만으로 모달 및 프로그레스 바 UI를 독자적으로 구현하였습니다.
