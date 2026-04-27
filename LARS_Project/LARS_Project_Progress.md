# LARS Project Progress Log

> 작성일: 2026-04-27
> 역할: Chief
> 기준 문서: `LARS_Project/New_LARS_Project.md` (v3, 2026-04-26 승인)
> 목적: 프로젝트 진행 이력 추적 및 세션 간 컨텍스트 인계

---

## 프로젝트 개요

- **프로젝트명:** LARS (Logistics Agent & Reporting System)
- **목표:** BOM·DP·PSI·IT 물류 업무를 웹 기반으로 디지털화하고 AI 자연어 인터페이스로 자동화
- **기술 스택:** FastAPI + Polars + PostgreSQL + Redis + React 18 + Vite + TypeScript
- **AI 구조:** OllamaProvider(Local) / AIServiceProvider(Internal/Cloud), AI_MODE 환경변수 전환
- **배포 환경:** Synology NAS(LARS Core) + AI PC RTX 4090(lars_ai_service)

---

## 완료된 Phase 이력

---
- Date: 2026-04-26
- Role: Project Leader / Coder (Gemini Pro 3.1)
- Action: Phase 1 완료 — 인프라 + BOM/DP 모듈 + JWT Auth + Import 파이프라인 구축
- Reason: LARS Platform 백엔드 기반 공사 및 실데이터 BOM import 검증 필요
- Result: Docker Compose, SQLModel 모델 12종, Alembic 마이그레이션, BOM/DP 파서, BOM 서비스 + API, Import 파이프라인 end-to-end 동작 확인 (1,018개 BOM Item 적재 검증)
- Ref: LARS_Project/Phase1_Coder_Instructions.md, LARS_Project/Phase1_Coder_Report.md
---

---
- Date: 2026-04-26
- Role: Project Leader / Coder (Gemini Pro 3.1)
- Action: Phase 2 완료 — 비즈니스 모듈 전체 API + React 프론트엔드 실데이터 연결
- Reason: DP→PL→PSI 자동 계산 파이프라인 및 PSI 매트릭스 인라인 편집 기능 구현 필요
- Result: DP/PL/IT/PSI/효율/WIP/Dashboard/Admin API 구현, Alembic 002 마이그레이션, React SPA 전체 페이지(14개) 구성, TypeScript 오류 0건
- Ref: LARS_Project/Phase2_Coder_Instructions.md, LARS_Project/Phase2_Coder_Report.md
---

---
- Date: 2026-04-26
- Role: Project Leader / Coder (Gemini Pro 3.1)
- Action: Phase 3 완료 — AI 통합(LLM/STT/TTS) + Vite Proxy 이식성 수정 + PSI 백그라운드 모니터
- Reason: 원격 브라우저 접속 문제 해결 및 LLM 추상 레이어, 음성 인터페이스, Ticket 자동 생성 구현 필요
- Result: OllamaProvider/CloudProvider, LARSAgent Tool 루프, Faster-Whisper STT, edge-tts TTS, APScheduler PSI 모니터(15분), Ticket CRUD, AI Chat/Ticket 페이지, TypeScript 오류 0건
- Ref: LARS_Project/Phase3_Coder_Instructions.md, LARS_Project/Phase3_Coder_Report.md
---

---
- Date: 2026-04-26
- Role: Chief / Coder (Gemini Pro 3.1)
- Action: Phase 3.5 완료 — AI 아키텍처 분산화 리팩토링 + BOM upsert 버그 수정 + 전역 에러 핸들러
- Reason: Technical Review에서 지적된 BOM PK 훼손 버그 및 Synology NAS/AI PC 분리 배포 아키텍처 도입 결정
- Result: AIServiceProvider, lars_ai_service/ 독립 FastAPI 앱(LLM proxy/STT GPU/TTS), AI_MODE 환경변수 설계, BOM sort_order 기반 PK 보존 upsert, 전역 예외 핸들러 3종, 스케줄러 config 이관, TypeScript 오류 0건
- Ref: LARS_Project/Phase3_5_Coder_Instructions.md, LARS_Project/Phase3_5_Coder_Report.md
---

---
- Date: 2026-04-26
- Role: Chief / Coder (Gemini Pro 3.1)
- Action: Phase 4 완료 — Multi-file Import, ItemMaster 자동화, AutoReport 탭 구조 도입
- Reason: Owner 실사용 리뷰 피드백 3건 반영 (다중 파일 업로드, IT 수동 import 불필요, 메뉴 Full Name화)
- Result: /import/upload-multi, /preview-multi, /process-multi API, rebuild_from_bom() BOM 파생 IT 자동 갱신, AutoReport 아코디언 사이드바, Full Name 페이지 제목 적용, TypeScript 오류 0건
- Ref: LARS_Project/Phase4_Coder_Instructions.md, LARS_Project/Phase4_Coder_Report.md
---

---
- Date: 2026-04-26
- Role: Chief / Coder (Gemini Pro 3.1)
- Action: Phase 4.1 완료 — Multi-file Import Network Error 버그 수정 + Progress Bar UX 개선
- Reason: Phase 4 후 실사용 시 upload-multi 엔드포인트에서 NameError → Network Error 발생 확인
- Result: import_pipeline.py 스키마 import 누락 수정, target_table 유효값에서 item_master 완전 제거, 파일별 개별 /upload 병렬 업로드 + Axios onUploadProgress Progress Bar, TypeScript 오류 0건
- Ref: LARS_Project/Phase4_1_Coder_Instructions.md, LARS_Project/Phase4_1_Coder_Report.md
---

---
- Date: 2026-04-27
- Role: Chief
- Action: LARS_Project_Progress.md 최초 작성 — 전체 Phase 이력 집약
- Reason: Agent_Rules.md Section 12 Project Progress Log Rule 준수 및 향후 세션 컨텍스트 인계 기반 마련
- Result: Phase 1~4.1 이력 6건 기록, 현재 상태 및 미완료 항목 명시
- Ref: (본 파일)
---

---
- Date: 2026-04-27
- Role: Chief
- Action: Server_Startup_Guide.md 작성 — 실제 .env 및 docker-compose.yml 기반 시동 절차 문서화
- Reason: 운영자/개발자가 세션마다 시동 방법을 별도 확인 없이 즉시 실행할 수 있도록 단일 문서화 요청
- Result: 6단계 시동 순서(Docker→Alembic→Admin→Backend→Frontend→AI Service), AI_MODE 전환 방법, 트러블슈팅 작성 완료
- Ref: LARS_Project/Server_Startup_Guide.md
---

---

## 현재 시스템 상태 (2026-04-27 기준)

### 백엔드 (backend/)
| 구분 | 상태 |
|---|---|
| FastAPI 서버 | 구현 완료 (uvicorn --host 0.0.0.0 --port 8000) |
| PostgreSQL 16 + pgvector | Docker Compose 구성 완료 |
| Alembic 마이그레이션 | 002까지 적용 완료 |
| JWT Auth | 완료 (admin@lars.local / admin1234) |
| BOM/DP/PL/IT/PSI/효율/WIP API | 전체 완료 |
| Import 파이프라인 | 단일 + 다중 파일 완료 |
| AI Chat/STT/TTS API | 완료 (AI_MODE 환경변수 제어) |
| Ticket CRUD | 완료 |
| PSI 백그라운드 모니터 | 완료 (APScheduler 15분 간격) |
| lars_ai_service/ | 완료 (AI PC 별도 배포용) |

### 프론트엔드 (.WebUI/)
| 구분 | 상태 |
|---|---|
| React SPA (14페이지) | 전체 구현 완료 |
| Vite Proxy (이식성) | 완료 (localhost 하드코딩 제거) |
| JWT 인증 흐름 | 완료 (Axios 인터셉터, 자동 refresh) |
| AutoReport 탭 구조 | 완료 (아코디언 사이드바) |
| PSI 매트릭스 인라인 편집 | 완료 |
| BOM Tree 계층 시각화 | 완료 |
| Multi-file Import + Progress Bar | 완료 |
| AI Chat + 음성 입력 | 완료 |
| TypeScript 오류 | 0건 확인됨 |

### 미완료 / 잔여 과제
| 항목 | 우선도 | 비고 |
|---|---|---|
| pytest 단위 테스트 | High | bom_parser, daily_plan_parser 복잡 로직 미검증 |
| Celery 비동기 Import | Medium | Phase 4.1 보고서 권고 (현재 동기 처리) |
| Redis 캐싱 (BOM 트리) | Medium | 아키텍처 설계됨, 미구현 |
| 파트너 사용자 권한 격리 | Medium | New_LARS_Project.md Phase 4 목표 미구현 |
| Cloud LLM 역할 (report_generator, data_analyst) | Low | AI_MODE=cloud 시 수동 구성 필요 |
| 음성/전화 통합 (PJSIP/SIP.js) | Low | New_LARS_Project.md 섹션 10 미구현 |
| 부하 테스트 (PSI 동시 50명) | Low | 운영 전 검증 필요 |

---

## 주요 설계 결정 기록

1. **Polars 전용**: 모든 DataFrame 연산에 Pandas 사용 금지 (New_LARS_Project.md 원칙 5)
2. **AI_MODE 4단계**: disabled / local / internal / cloud — .env 환경변수 하나로 전환
3. **BOM upsert**: delete+insert 폐기 → sort_order 기준 PK 보존 update/insert/delete
4. **ItemMaster 자동화**: 수동 Import 제거 → BOM Import 시 rebuild_from_bom() 자동 트리거
5. **lars_ai_service 분리**: NAS(저사양)와 AI PC(RTX 4090)를 HTTP로 분리, GPU 추론 전담
6. **Vite Proxy**: 원격 브라우저 접속 시 IP 하드코딩 없이 상대경로(/api/v1)로 처리
