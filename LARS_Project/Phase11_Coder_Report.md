# Phase 11 Coder Report — Import 자동 후처리 및 통합 Background Monitor 완료

## 1. 개요
BOM 및 Daily Plan 임포트 후 발생하는 대량 데이터 처리(ItemMaster 재구성, 소요자재 재계산 등)를 비동기(Background) 방식으로 전환하여 시스템 응답성을 대폭 개선하고, 작업 진행 상태를 실시간으로 확인할 수 있는 통합 모니터링 시스템을 구축하였습니다.

## 2. 작업 상세

### 2.1 통합 Background Status API (Task 11-A)
- **API 구현**: `backend/api/routes/background.py` 신규 생성.
- **기능**: Redis에 저장된 여러 태스크(`item_rebuild`, `part_list_recompute`, `psi_recompute`)의 상태를 하나의 엔드포인트에서 통합 반환합니다.
- **라우터 등록**: `api/router.py`를 통해 `/api/v1/background/status` 경로를 확보하였습니다.

### 2.2 비동기 후처리 로직 구현 (Task 11-B, 11-C)
- **PartList 비동기화**: `part_list_service.recompute_background` 구현. 개별 날짜별로 재계산하며 Redis에 진행률(progress)을 기록합니다.
- **PSI 비동기화**: `psi_service.recompute_all_background` 구현. 전체 재계산 과정을 비동기로 수행합니다.

### 2.3 Import Pipeline 최적화 (Task 11-D)
- **Non-blocking 전환**: `import_pipeline.py`의 모든 후처리 호출을 FastAPI `BackgroundTasks`로 교체하였습니다. 이제 사용자는 임포트 완료 즉시 다음 작업을 수행할 수 있습니다.
- **Folder Import 연동**: 서버 내 폴더 스캔 방식(`folder_import_service`)에서도 동일한 비동기 메커니즘을 적용하였습니다.

### 2.4 BackgroundMonitor UI 업그레이드 (Task 11-E)
- **멀티 태스크 지원**: 단일 작업만 감지하던 기존 UI를 개선하여, 현재 실행 중인 모든 백그라운드 작업을 리스트 형태로 표시합니다.
- **UX 개선**: 작업별 진행바, 퍼센트, 처리 건수를 시각화하였으며 완료/실패 시 5초 후 자동 숨김 기능을 통해 화면 공간을 효율적으로 사용합니다.

## 3. 검증 결과
- **백엔드**: `/background/status` API 정상 작동 및 Redis 상태 값 업데이트 확인.
- **프론트엔드**: `npm run build` 성공 및 사이드바 내 통합 모니터 UI 렌더링 확인.
- **응답성**: 임포트 버튼 클릭 시 후처리 대기 없이 즉시 완료 처리됨을 확인.

## 4. 특이사항
- `psi_service.py` 내에 `recompute_all(session)` 함수가 이미 존재하여 이를 재사용함으로써 코드 중복을 최소화하였습니다.
