# LARS 프로젝트 통합 진척 보고서

> 최종 업데이트: 2026-05-17
> 작성자: Chief (Claude)
> 목적: Phase 1~18 전체 압축 통합 — 다음 세션 컨텍스트 인계용
> 원본 참조: Phase1~18 Coder Instructions/Report

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|---|---|
| 프로젝트명 | LARS (Logistics Agent & Reporting System) |
| 목표 | BOM·DP·PSI·IT 물류 업무 웹 디지털화 + AI 자연어 인터페이스 자동화 |
| 기술 스택 | FastAPI + Polars + PostgreSQL 16 + pgvector + Redis + React 18 + Vite + TypeScript |
| AI 구조 | LLMProvider 추상 레이어 / AI_MODE 환경변수 전환 (disabled/local/internal/cloud) |
| 배포 환경 | Synology NAS DS925+ (LARS Core) + AI PC RTX 4090 (lars_ai_service, 선택) |
| 접속 URL | http://sslshg.iptime.org:3000 |
| 기본 계정 | admin@lars.local / admin1234 |
| API Prefix | `/api/v1` |

---

## 2. 핵심 설계 원칙 (변경 불가)

1. **Polars 전용** — 모든 DataFrame 연산에서 Pandas 사용 금지
2. **LLM Provider 종속성 없음** — 모든 LLM 호출은 `LLMProvider` 추상 레이어 경유
3. **AI_MODE 4단계** — disabled / local / internal / cloud (.env 하나로 전환)
4. **ModelNumber = Model.Suffix** — `LSGL6335X.ARSELGA` 형식이 고유 키. bare model_code 단독 사용 금지
5. **is_active 패턴** — FK 참조 레코드는 DELETE 대신 `is_active=False` 처리
6. **BOM sort_order 기반 upsert** — delete+insert 폐기, PK 보존 update/insert/delete

---

## 3. 데이터 현황 (2026-05-17 실측)

| 테이블 | 수량 | 비고 |
|---|---|---|
| bom_models | 196개 | 모두 model_code + suffix 복합키 |
| bom_items | ~41,000개 | level=-1(대체품) 포함 |
| daily_plan_lots | ~1,439개 | suffix 컬럼 보정 완료 |
| import_batches (DP) | 5개 | data_source=local, target=id 629 |
| item_master | 9,993개 활성 | @CVZ.EKHQ 완제품 196개 is_active=False |
| psi_models | 126개 | Model.Suffix 형식 정상 |

---

## 4. 완료 Phase 요약

### Phase 1 — 인프라 기반 (2026-04-26)
Docker Compose(PG16+pgvector+Redis), SQLModel 12종, Alembic 001, JWT Auth(RBAC), BOM/DP 파서, Import 파이프라인 end-to-end. 실데이터 BOM 1,018개 item 적재 검증.

### Phase 2 — 비즈니스 모듈 전체 (2026-04-26)
Alembic 002(`daily_qty_json`), DP→PL 자동계산, PSI 행렬(shortage_qty), IT/효율/WIP/Dashboard/Admin/Ticket API, React SPA 14페이지, JWT 인터셉터.

### Phase 3 — AI 통합 (2026-04-26)
OllamaProvider/CloudProvider, LARSAgent Tool 루프, Faster-Whisper STT, edge-tts TTS, APScheduler PSI 모니터(15분), Vite Proxy 이식성 수정(localhost 하드코딩 제거).

### Phase 3.5 — AI 아키텍처 분산화 (2026-04-26)
AIServiceProvider, lars_ai_service/ 독립 FastAPI(AI PC 분리 배포), BOM upsert PK 보존, 전역 예외 핸들러 3종.

### Phase 4 — Multi-file Import + ItemMaster 자동화 (2026-04-26)
/upload-multi, /preview-multi, /process-multi, rebuild_from_bom() BOM 파생 IT 자동갱신, AutoReport 아코디언 사이드바.

### Phase 4.1 — Upload 버그 수정 (2026-04-26)
import_pipeline.py NameError 수정, 파일별 개별 업로드 + Axios onUploadProgress Progress Bar.

### Phase 5 — PSI 전면 재설계 (2026-05-16)
DB 스키마 확장(inventory, defect, is_picked), Polars 고성능 PSI 매트릭스, React 2행 블록 테이블 UI. Alembic 003.

### Phase 6 — 일일 운영 자동화 (2026-05-16)
folder_import_service(BOMDB/DPDB 폴더 스캔), advance_day API(재고 전진), one_click_solution API(5단계 자동화), Dashboard One-Click 버튼.

### Phase 6.1 — DP Import FK 버그 수정 (2026-05-16)
daily_plan_service: lots DELETE 전 part_list_snapshots FK 참조 선행 삭제. daily_plan_parser 비표준 CSV 형식 감지 보강.

### Phase 7 — ItemMaster 강화 (2026-05-16)
Redis cache-aside(TTL 300s, key: `itemmaster:all`), Background rebuild(should_rebuild 조건부), vendor_raw 정제 파싱(`^[A-Z]+_(.+)_KR\d+$`), BOM 사용처 역조회 Polars 집계.

### Phase 8 — Daily Plan 뷰어 재설계 (2026-05-16)
/dp/daily, /dp/dates API, 날짜 기반 웹/인쇄 뷰어 탭 UI. daily_plan_service.py TypeError 버그 직접 수정.

### Phase 9 — BOM Suffix 통합 + UX 강화 (2026-05-16)
BomModel 복합키(model_code+suffix), 사이드바 진행 상태 모니터, Sticky 레이아웃, TutorialBox 시스템.

### Phase 10 — DP Viewer Batch 기반 재설계 (2026-05-16)
/dp/batches, /dp/lots-raw API, 2-Panel Batch 선택 + Flat 테이블 뷰. Target DP 개념 도입(Redis `dp:target_batch_id`).

### Phase 11 — Import 비동기 후처리 + 모니터 (2026-05-16)
통합 Status API, 모든 후처리 BackgroundTasks 전환, 멀티 태스크 진행 모니터 UI.

### Phase 12 — BOM 목록 그룹핑 + 트리 뷰어 (2026-05-16)
BOM 목록 모델 그룹화/Variant 접기, 계층 트리 인터랙티브 토글 뷰어.

### Phase 13 — ItemMaster 분리 + 범용 컬럼 필터 (2026-05-16)
구매품/사내생산품 탭 분리, useColumnFilter 훅 + FilterableHeader 컴포넌트 전 페이지 적용.

### Phase 14 — 사이드바 접기/유저 관리 강화 (2026-05-16)
접힘 모드 사이드바(아이콘+툴팁), User 모델 프로필 필드 확장(phone, company, department, rank, position), 내 프로필 페이지, Admin 인라인 편집.

### Phase 15 — DP Line 열 + SystemStatusBar (2026-05-16)
DailyPlanLot suffix 컬럼 추가, /dp/lots-raw Line 조인 + model_number 보정, SystemStatusBar(DB/AI/시간) 사이드바 통합.

**중요 데이터 수정 (Phase 15 병행)**:
- BOM suffix='' 문제: 전체 BOM 삭제 후 196개 파일 재임포트
- DP suffix=NULL 문제: CSV 재파싱 + wo_number 매칭 UPDATE(1,439건)
- PSI `get_active_models()`: `(model_code, suffix)` 조인 → `Model.Suffix` 형식 반환
- ItemMaster `@CVZ.EKHQ` 완제품 196개 `is_active=False` 처리

### Phase 16 — DP Print Format View (2026-05-16)
/dp/lots-raw 실적 데이터(input_qty, output_qty) 추가, DailyPlanPrintView.tsx(라인 탭, 모델 pill, A3 인쇄), Raw/Print 탭 토글.

### Phase 17 — BOM Substitute Fix + Amount View (2026-05-17)
**17-A**: buildTree 알고리즘 교체 — level=-1 대체품을 `pathToNode` Map으로 본부품 `substitutes[]`에 연결(stack push 제외). TreeRow에서 본부품 행→대체품 행→자식 행 순 렌더링.  
**17-B**: `GET /api/v1/bom/amount/{model_number:path}` — path 분해로 조상 qty 곱셈, part_number별 합산. level=0(루트)·level=-1(대체품) 제외.  
**17-C**: BOMAmountView.tsx(품번별 소요량 플랫 테이블, Grand Total), BOMDetailPage 토글([Tree View]/[Amount View]).  
검증: CBGJ3023D.ABDELNA 기준 175개 고유 품번, occurrence_count 정상 집계.

### Phase 18 — DP Batch 삭제 + 출처 태그 (2026-05-17)
**18-A/B**: import_batches 테이블 `data_source VARCHAR DEFAULT 'local'` 추가, Alembic migration `f1a8e1b9` 적용.  
**18-C**: `DELETE /api/v1/dp/batches/{batch_id}` — PartListSnapshot→DailyPlanLot→DailyPlan(빈 것)→ImportBatch 순 cascade. Target이었으면 Redis `dp:target_batch_id` 초기화.  
**18-D/E**: 모든 ImportBatch 생성 경로에 `data_source="local"` 명시. /dp/batches 응답에 `data_source` 포함.  
**18-F**: DailyPlanPage 배치 카드에 Local(회색)/ERP(보라) 태그, Trash2 아이콘 + 인라인 확인/취소 UX. Target 배치 삭제 버튼 비활성화.

---

## 5. 핵심 DB 스키마 요약

```
bom_models:       id, model_code, suffix (UNIQUE), description, version, is_active, import_batch_id
bom_items:        id, model_id(FK), level(-1=대체품), part_number, description, qty, uom,
                  vendor_raw, supply_type, path(materialized), sort_order, import_batch_id
daily_plan_lots:  id, plan_id(FK), wo_number, model_code, suffix, lot_number, planned_qty,
                  input_qty, output_qty, planned_start, sort_order, daily_qty_json, import_batch_id
import_batches:   id, source_type, source_name, target_table, status, data_source(local/erp),
                  started_by(FK→users), started_at, finished_at
item_master:      id, part_number(UNIQUE), description, level, vendor_raw, lower_vendor_raw,
                  is_active, tracking_user_id, import_batch_id
psi_records:      id, item_id(FK→item_master), date, lot_id(FK), inventory, defect, is_picked,
                  required_qty, shortage_qty
users:            id, email(UNIQUE), display_name, role, is_active, hashed_pw,
                  phone, company, department, rank, position
```

---

## 6. Redis 키 목록

| 키 | 용도 |
|---|---|
| `dp:target_batch_id` | 현재 Target DP batch ID |
| `itemmaster:all` | ItemMaster 전체 캐시 (TTL 300s) |
| `itemmaster:rebuild_status` | rebuild 진행 상태 JSON |
| `itemmaster:last_rebuild_at` | 마지막 rebuild 시각 (ISO) |
| `psi:recompute_status` | PSI 재계산 진행 상태 JSON |

---

## 7. 서버 시동 명령

```bash
# 백엔드
cd /test/LARS/backend
nohup venv/bin/uvicorn main:app --host 0.0.0.0 --port 8000 > server.log 2>&1 &

# 프론트엔드
cd /test/LARS/.WebUI
npm run build
nohup npx vite preview --port 3000 --host 0.0.0.0 > /tmp/vite.log 2>&1 &
```

---

## 8. 미완료 / 잔여 과제

| 항목 | 우선도 | 비고 |
|---|---|---|
| DP × BOM Amount 조인 → 일일 자재소요량(Daily PSI) | **High** | Phase 17 Amount View 완성으로 기반 마련. 다음 우선 과제 |
| ERP 연동 import 경로 | High | data_source="erp" 태그 체계 완비. API 연동 엔드포인트 미구현 |
| pytest 단위 테스트 | Medium | bom_parser, daily_plan_parser 복잡 로직 미검증 |
| PSI 재계산 정확도 검증 | Medium | Model.Suffix 보정 후 실데이터 대조 필요 |
| 파트너 사용자 권한 격리 | Medium | RBAC 기반 구현됨, partner role 페이지 격리 미구현 |
| Cloud LLM 역할 구성 | Low | AI_MODE=cloud 수동 설정 필요 |
| 음성/전화 통합 (SIP.js) | Low | 미착수 |
| 부하 테스트 | Low | 운영 전 검증 필요 |
