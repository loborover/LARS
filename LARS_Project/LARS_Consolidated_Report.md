# LARS 프로젝트 통합 진척 보고서

> 작성일: 2026-05-16
> 작성자: Chief
> 목적: Phase 1~4.1 전체 보고서 압축 통합 — 다음 세션 컨텍스트 인계용
> 원본 참조: Phase1~4.1 Coder Instructions/Report, Technical_Review_Report, New_LARS_Project.md

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|---|---|
| 프로젝트명 | LARS (Logistics Agent & Reporting System) |
| 목표 | BOM·DP·PSI·IT 물류 업무 웹 디지털화 + AI 자연어 인터페이스 자동화 |
| 기술 스택 | FastAPI + Polars + PostgreSQL 16 + pgvector + Redis + React 18 + Vite + TypeScript |
| AI 구조 | LLMProvider 추상 레이어 / AI_MODE 환경변수 전환 (disabled/local/internal/cloud) |
| 배포 환경 | Synology NAS DS925+ (LARS Core) / AI PC RTX 4090 (lars_ai_service, 선택) |
| 접속 URL | http://sslshg.iptime.org:3000 |
| 기본 계정 | admin@lars.local / admin1234 |

---

## 2. 핵심 설계 원칙 (변경 불가)

1. **LLM Provider 종속성 없음** — 모든 LLM 호출은 `LLMProvider` 추상 레이어 경유
2. **Polars 전용** — 모든 DataFrame 연산에서 Pandas 사용 금지
3. **Import 우선** — 수동 Excel/CSV import 기반, 향후 API auto-sync 확장 가능 구조
4. **AI_MODE 4단계** — disabled / local / internal / cloud (.env 하나로 전환)
5. **버전 관리 책임** — git 작업은 사용자 전담, AI는 코드 작성만 담당

---

## 3. 완료 Phase 이력

### Phase 1 — 인프라 + BOM/DP 기반 (완료)
- Docker Compose (PostgreSQL 16 + pgvector + Redis)
- SQLModel 모델 12종 + Alembic 001 마이그레이션
- JWT Auth (login/refresh/me) + RBAC (admin/manager/internal/partner/viewer)
- BOM Parser: 도트 기반 level 파싱, 대체품(`*S*`) 핸들링, path 트리 구조 계산
- Daily Plan Parser: Excel (`W/O 계획수량` 동적 헤더) + CSV (글로브 문자 우회)
- Import 파이프라인: upload → preview → process end-to-end
- **검증:** 실데이터 BOM 1,018개 item 적재 확인

### Phase 2 — 비즈니스 모듈 전체 + 프론트엔드 (완료)
- Alembic 002: `daily_qty_json` 컬럼 추가
- DP→PL 자동 계산 파이프라인 (BOM × DP Lot 소요량 스냅샷)
- PSI 행렬 빌드: IT 기준 part_number × date, shortage_qty 자동 계산
- IT/효율/WIP/Dashboard/Admin/Ticket API 전체 구현
- React SPA 14페이지 구성 (JWT 인터셉터, 자동 토큰 갱신)
- PSI 인라인 편집 + 부족분 빨간 하이라이트
- BOM Tree 들여쓰기 계층 시각화
- WebSocket 대시보드 실시간 알림
- **검증:** TypeScript 오류 0건

### Phase 3 — AI 통합 (완료)
- Vite Proxy 이식성 수정 (localhost 하드코딩 제거 → 상대경로)
- OllamaProvider / CloudProvider 추상화
- LARSAgent Tool 루프: query_psi, get_bom_tree, create_ticket, list_tickets, get_dp_summary, bom_reverse_lookup
- Faster-Whisper STT (`/ai/voice/transcribe`)
- edge-tts TTS 한국어 음성 합성
- APScheduler PSI 모니터 (15분 간격, 부족 시 자동 Ticket + WebSocket 브로드캐스트)
- AI Chat / Ticket 페이지 구현
- **검증:** TypeScript 오류 0건

### Phase 3.5 — AI 아키텍처 분산화 리팩토링 (완료)
- **BOM upsert 버그 수정**: DELETE+INSERT 폐기 → sort_order 기반 PK 보존 update/insert/delete
- lars_ai_service/ 독립 FastAPI (LLM proxy / STT GPU / TTS) — AI PC 전용 배포
- AIServiceProvider: NAS ↔ AI PC OpenAI 호환 HTTP 통신
- 전역 예외 핸들러 3종 (ParseError → HTTP 400, 일반 오류)
- 스케줄러 config 이관 (`SCHEDULER_TIMEZONE`, `PSI_MONITOR_INTERVAL_MINUTES`)
- Admin 페이지 AI 설정 UI + 연결 테스트 버튼

### Phase 4 — Multi-file Import + ItemMaster 자동화 + AutoReport 탭 (완료)
- Multi-file Import API 3종: `/upload-multi`, `/preview-multi`, `/process-multi`
- `rebuild_from_bom()`: BOM import 시 item_master 자동 갱신 (수동 IT import 제거)
- AutoReport 아코디언 사이드바 (BOM/DP/PL/IT/PSI 그룹화)
- 전 메뉴 Full Name 한국어 명칭 적용
- **검증:** TypeScript 오류 0건

### Phase 4.1 — Upload 버그 수정 + Progress Bar UX (완료)
- `import_pipeline.py` 스키마 import 누락 → NameError 수정
- target_table 유효값에서 item_master 완전 제거
- 파일별 개별 `/upload` 병렬 업로드 + Axios onUploadProgress Progress Bar
- 상태 시각화: 업로드 중(파란색 %) / 완료(초록 ✅) / 실패(빨간 ❌ + 툴팁)
- **검증:** TypeScript 오류 0건

---

## 4. 현재 시스템 상태

### 백엔드 (`backend/`)
| 구분 | 상태 |
|---|---|
| FastAPI 서버 | 실행 중 (PID: 2026, port 8000) |
| PostgreSQL 16 + pgvector | Docker 실행 중 (port 5433) |
| Redis | Docker 실행 중 (port 6379) |
| Alembic | 마이그레이션 최신 (head: 5a44df45d409) |
| JWT Auth | 완료 (admin@lars.local / admin1234) |
| AI 모드 | cloud (Gemini 2.5 Flash) |
| 전체 API | BOM/DP/PL/IT/PSI/효율/WIP/Import/AI/Dashboard/Ticket/Admin 완료 |

### 프론트엔드 (`.WebUI/`)
| 구분 | 상태 |
|---|---|
| Vite preview 서버 | 실행 중 (PID: 2533, port 3000) |
| React SPA 14페이지 | 전체 완료 |
| Production 빌드 | 완료 (dist/ — 462KB JS, gzip 144KB) |
| allowedHosts | sslshg.iptime.org 등록 완료 |

### 서버 기동 명령 (재시작 시 참조)
```bash
# [NAS SSH] Docker
cd /volume3/docker/LARS && sudo docker compose up -d

# [컨테이너 내] 백엔드
cd /test/LARS/backend && source venv/bin/activate
nohup uvicorn main:app --host 0.0.0.0 --port 8000 > /tmp/lars_backend.log 2>&1 &

# [컨테이너 내] 프론트엔드 (production)
cd /test/LARS/.WebUI
nohup npm run preview > /tmp/lars_frontend.log 2>&1 &
# 코드 변경 시: npm run build 후 재기동
```

---

## 5. 주요 설계 결정 기록

| # | 결정 | 이유 |
|---|---|---|
| 1 | Polars 전용, Pandas 금지 | 성능 원칙 |
| 2 | AI_MODE 4단계 환경변수 전환 | 장비 유연성 |
| 3 | BOM upsert: sort_order 기반 PK 보존 | Phase 3.5 버그 수정 |
| 4 | ItemMaster = BOM에서 자동 파생 | 수동 import 불필요 |
| 5 | lars_ai_service 분리 (NAS / AI PC) | NAS 저사양 대응 |
| 6 | Vite Proxy 상대경로 | 원격 접속 이식성 |
| 7 | Production 빌드 (vite preview) | 외부 접속 속도 개선 |

---

## 6. 미완료 잔여 과제

| 항목 | 우선도 | 비고 |
|---|---|---|
| **pytest 단위 테스트** | **High** | bom_parser, daily_plan_parser 복잡 로직 미검증 — 회귀 위험 |
| Celery 비동기 Import | Medium | 현재 동기 처리, 대용량 시 타임아웃 위험 |
| Redis 캐싱 (BOM 트리 TTL 1h) | Medium | 아키텍처 설계됨, 미구현 |
| 파트너 사용자 권한 격리 | Medium | 담당 품목만 PSI 조회 허용 |
| Cloud LLM 역할 (report_generator, data_analyst) | Low | AI_MODE=cloud 시 고급 분석 Agent |
| 음성/전화 통합 (PJSIP/SIP.js) | Low | 사내 IP-PBX 연동 |
| 부하 테스트 (PSI 동시 50명) | Low | 운영 전 검증 필요 |

---

## 7. 알려진 버그 / 기술 부채

| 항목 | 위험도 | 내용 |
|---|---|---|
| 테스트 코드 전무 | High | bom_parser._compute_paths, _deduplicate 로직 엣지케이스 미검증 |
| 동기 Import | Medium | 대용량 Excel 업로드 시 브라우저 타임아웃 가능성 |
| Redis 캐시 미구현 | Medium | BOM 트리 반복 쿼리 시 DB 부하 |

---

*이 문서는 Phase 1~4.1 전체 보고서의 압축 통합본입니다.*
*세부 내용은 각 Phase의 Coder Instructions/Report 원본을 참조하십시오.*
