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
- Date: 2026-05-16
- Role: Coder
- Action: Phase 5 완료 — PSI 전면 재설계 (실무 Expeditor 구조 이식)
- Reason: 실제 업무 PSI 시트 구조(품목당 4행, 담당자 마킹, 재고/불량 직접 관리)와 LARS 기능 간 Gap 해소
- Result: DB 스키마 확장(inventory, defect, is_picked), Polars 기반 고성능 PSI 매트릭스 API, React 2행 블록 테이블 UI 구현, TS 오류 0건
- Ref: LARS_Project/Phase5_Coder_Instructions.md, LARS_Project/Phase5_Coder_Report.md
---

---

- Date: 2026-05-16
- Role: Chief
- Action: Phase6_Coder_Instructions.md 작성 — 일일 운영 자동화 (Advance_Day + One_Click_Solution)
- Reason: VBA Expeditor_DayShift.bas / Expeditor_PSI.bas 분석으로 매일 아침 4단계 워크플로우 확인, BOMDB(196개)/DPDB(29개) 실데이터 폴더 활용 가능 확인
- Result: 폴더 기반 Import API, Advance_Day API, One_Click_Solution API(5단계), Dashboard 하루시작 버튼 설계 완료
- Ref: LARS_Project/Phase6_Coder_Instructions.md
---

---
- Date: 2026-05-16
- Role: Chief
- Action: Phase 5 서비스 적용 — 백엔드 재시작 + Alembic 003 마이그레이션 적용
- Reason: Phase 5 코드 완료 후 실행 중인 서비스에 미반영 상태였음
- Result: 마이그레이션 head(cd7af37a0e4e), 백엔드/프론트엔드 재기동 완료
- Ref: (없음)
---

---
- Date: 2026-05-16
- Role: Coder
- Action: Phase 7 완료 — ItemMaster 강화 (Redis 캐싱, 비동기 Rebuild, 업체명 파싱)
- Reason: 대규모 품목 데이터(9,000건+) 조회 성능 최적화 및 실무 중심의 가독성/사용성 향상
- Result: Redis 전역 캐싱 도입, Background Task 기반 비동기 재구성, 업체명 정제 파싱 로직, BOM 사용처 역조회 모달 구현 완료
- Ref: LARS_Project/Phase7_Coder_Instructions.md, LARS_Project/Phase7_Coder_Report.md
---

---
- Date: 2026-05-16
- Role: Coder
- Action: Phase 6.1 완료 — DP Import FK 버그 수정 및 에러 처리 강화
- Reason: Phase 6 폴더 임포트 시 발생한 FK 위반 오류 해결 및 비표준 파일에 대한 피드백 개선
- Result: daily_plan_service에서 선행 레코드 삭제 로직 추가, daily_plan_parser에서 형식 감지 로직 보강, 6개 파일 재임포트 성공 확인
- Ref: LARS_Project/Phase6_1_Coder_Instructions.md, LARS_Project/Phase6_1_Coder_Report.md
---

---
- Date: 2026-05-16
- Role: Chief
- Action: Phase5_Coder_Instructions.md 작성 — PSI 전면 재설계 지시서
- Reason: Expeditor_Public.xlsb 분석 결과 현재 LARS PSI와 실제 업무 PSI 간 구조적 Gap 확인
- Result: 담당자별 필터, D-Day 상대 날짜, 2차협력사, 재고/불량수량, is_picked 기능 설계 완료
- Ref: LARS_Project/Phase5_Coder_Instructions.md
---

---
- Date: 2026-05-16
- Role: Coder
- Action: Phase 6 완료 — 일일 운영 자동화 (Advance_Day + One_Click_Solution)
- Reason: 수동 데이터 취합 및 재계산 과정(BOM/DP 취합, 재고 전진, 소요량 재계산, 부족분 티켓 생성)을 단일 API로 통합 자동화
- Result: folder_import_service 구현, advance_day 및 one_click_solution API 작성, 대시보드 One-Click 버튼 추가, 빌드 검증 성공
- Ref: LARS_Project/Phase6_Coder_Instructions.md, LARS_Project/Phase6_Coder_Report.md
---

---
- Date: 2026-05-16
- Role: Chief
- Action: LARS_Consolidated_Report.md 작성 — Phase 1~4.1 전체 보고서 압축 통합
- Reason: 세션 간 컨텍스트 인계 효율화 및 보고서 파편화 해소
- Result: 단일 통합 보고서 생성 (현황/설계결정/잔여과제/기술부채 포함)
- Ref: LARS_Project/LARS_Consolidated_Report.md
---

---

- Date: 2026-05-16
- Role: Chief
- Action: Phase 8 Coder 지시서 작성 + DP API 버그 직접 수정 — Daily Plan 뷰어 재설계
- Reason: list_plans()의 pl.col(InstrumentedAttribute) TypeError 버그로 DP 목록 전체 500 오류. UI도 2-panel 클릭 구조로 실무 불가 판단
- Result: daily_plan_service.py 오류 1줄 직접 제거 (DP 목록 API 정상화), 날짜 기준 /dp/daily + /dp/dates API 설계, 웹 뷰어/인쇄 뷰어 탭 UI 설계 완료
- Ref: LARS_Project/Phase8_Coder_Instructions.md

---

- Date: 2026-05-16
- Role: Chief
- Action: Phase 7 Coder 지시서 작성 — ItemMaster 강화 (업체명 파싱 + Redis 캐싱 + Background Rebuild + BOM 역조회)
- Reason: vendor_raw의 EKHQ_업체명_코드 형식 파싱 요청, Redis 캐싱 상시화, BOM 갱신 시에만 조건부 Background rebuild, 사용처 역조회 UI 구현 필요
- Result: 7개 Task 설계 완료 (vendor 파싱 regex, Redis cache-aside, should_rebuild 조건 로직, background engine-scoped session, BOM Polars 집계, Progress UI 폴링)
- Ref: LARS_Project/Phase7_Coder_Instructions.md

---

- Date: 2026-05-16
- Role: Chief
- Action: Phase 6.1 버그 분석 및 Coder 지시서 작성 — DP Import FK 제약 위반 수정
- Reason: Phase 6 완료 후 `POST /api/v1/import/folder/dp` 실행 시 7개 파일 전부 실패 확인. 원인 분석: `daily_plan_service.py`가 `daily_plan_lots` DELETE 시 `part_list_snapshots`의 FK 참조 제거를 누락
- Result: 버그 2종 특정 (Critical: FK 위반 / Minor: 비표준 CSV 형식), Phase6_1_Coder_Instructions.md 작성 완료
- Ref: LARS_Project/Phase6_1_Coder_Instructions.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase 8 완료 — Daily Plan 뷰어 재설계 (웹 뷰어 + 인쇄 뷰어)
- Reason: 날짜 기반 조회 및 인쇄 최적화 레이아웃 도입으로 생산 현장 실무 대응력 강화
- Result: /dp/daily, /dp/dates API 구현, 탭 구조의 웹/인쇄 뷰어 프론트엔드 전면 개편, 빌드 및 TS 검증 완료
- Ref: LARS_Project/Phase8_Coder_Report.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase 9 완료 — BOM Suffix 통합, 백그라운드 모니터 및 UI 고도화
- Reason: 모델 식별 체계 정밀화 및 사용자 경험(UX) 강화를 통한 시스템 완성도 제고
- Result: BomModel 복합키(Model+Suffix) 적용, 사이드바 진행 상태 모니터 UI 추가, Sticky 레이아웃 및 튜토리얼 시스템 전면 도입
- Ref: LARS_Project/Phase9_Coder_Report.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase 10 완료 — DP Viewer 전면 재설계 (Batch 기반 Flat View)
- Reason: "Excel 시트처럼 Raw 데이터를 보고 싶다"는 사용자 요구사항 반영 및 시스템 전체의 기준 DP(Target) 관리 기능 도입
- Result: /dp/batches, /dp/lots-raw 등 API 4종 구현, 2-Panel 방식의 프론트엔드 UI 전면 개편, PSI/PartList 연동 완료
- Ref: LARS_Project/Phase10_Coder_Report.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase 11 완료 — Import 자동 후처리 및 통합 Background Monitor
- Reason: 임포트 후처리 과정의 블로킹 현상 제거 및 다중 백그라운드 작업 가시성 확보
- Result: 통합 Status API 구현, 모든 후처리 로직 비동기(BackgroundTasks) 전환, 멀티 태스크 지원 모니터 UI 업그레이드
- Ref: LARS_Project/Phase11_Coder_Report.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase 12 완료 — BOM List 그룹핑 및 트리 뷰어 개선
- Reason: 복합 모델(Model+Suffix) 체계의 가독성 향상 및 대규모 BOM 데이터 탐색 편의성 제공
- Result: BOM 목록 모델 그룹화 및 Variant 접기 기능, 계층 트리 구조 빌드 및 인터랙티브 토글 뷰어 구현 완료
- Ref: LARS_Project/Phase12_Coder_Report.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase 13 완료 — ItemMaster 분리 및 범용 컬럼 필터 도입
- Reason: 품목 관리 체계 구체화 및 전사 데이터 테이블의 탐색 편의성(Searchability) 대폭 강화
- Result: ItemMaster 구매품/사내생산품 탭 분리, useColumnFilter 훅 및 FilterableHeader 컴포넌트 기반 범용 필터 시스템 전 페이지 적용 완료
- Ref: LARS_Project/Phase13_Coder_Report.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase 14 완료 — 사이드바 접기/펼치기 및 유저 관리 강화
- Reason: 화면 공간 효율성 증대 및 실무 조직 체계에 맞는 사용자 정보 관리 기반 구축
- Result: 접힘 모드(아이콘+툴팁) 사이드바 구현, User 모델 프로필 필드 확장, 내 프로필 페이지 및 Admin 인라인 편집 기능 추가
- Ref: LARS_Project/Phase14_Coder_Report.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase 15 완료 — DP 구조 수정 및 시스템 상태 표시줄 도입
- Reason: 사용자 반복 지적 사항(Line/Suffix 누락) 해결 및 시스템 가동 상태 상시 시각화
- Result: DailyPlanLot suffix 컬럼 추가, /dp/lots-raw API Line 조인 및 모델명 보정, SystemStatusBar(DB/AI/Time) 구현 및 사이드바 통합 완료
- Ref: LARS_Project/Phase15_Coder_Report.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase Remediation 완료 — 긴급 감사 지적 사항 보완
- Reason: Phase 14 누락 파일 생성, 서비스 미재시동으로 인한 API 미로드 해결 및 사용자 의도에 따른 UI 보정
- Result: ProfilePage, users API, Admin 인라인 편집 보완 완료. 백엔드/프론트엔드 전면 재기동 및 전수 검증 성공. ItemMaster 기본탭 '사내생산품'으로 변경
- Ref: LARS_Project/Phase_Remediation_Report.md

---

- Date: 2026-05-16
- Role: Coder
- Action: Phase 16 완료 — DP Print Format View 구현
- Reason: 생산 현장 엑셀 양식과 동일한 웹 뷰 제공 및 필터링 기반 인쇄 편의성 강화
- Result: /dp/lots-raw 실적 데이터 추가, DailyPlanPrintView 컴포넌트 개발, 탭 전환 UI 및 A3 Landscape 인쇄 최적화 완료
- Ref: LARS_Project/Phase16_Coder_Report.md

---

- Date: 2026-05-17
- Role: Coder
- Action: Phase 17 완료 — BOM Substitute Fix + BOM Amount View 구현
- Reason: 대체품(S) 렌더링 버그 수정 및 BOM 계층 구조 전개 기반의 실제 소요량 산출 기능 제공
- Result: buildTree 알고리즘 수정, /api/v1/bom/amount API 구현, BOMAmountView 탭 및 테이블 UI 추가 완료
- Ref: LARS_Project/Phase17_Coder_Report.md

---

- Date: 2026-05-17
- Role: Coder
- Action: Phase 18 완료 — DP Batch 삭제 및 출처 관리
- Reason: 생산 계획 데이터의 불필요한 이력 제거 기능 제공 및 데이터 신뢰도(출처) 시각화
- Result: import_batches 테이블 data_source 컬럼 추가, 배치 삭제 API 구현, 프론트엔드 삭제 UX 및 출처 태그 시스템 적용 완료
- Ref: LARS_Project/Phase18_Coder_Report.md

---

---

- Date: 2026-05-17
- Role: Chief / Coder (Gemini)
- Action: Phase 19 완료 — Daily PartList 고도화
- Reason: BOM Amount 기반 정확한 자재소요량 계산 및 다각도 분석 뷰(Lot View, PSI Matrix) 제공 필요
- Result: recompute_for_dates() BOM 경로 누적 qty 적용, LotViewRow/LotViewResponse/PsiMatrixRow/PsiMatrixResponse 스키마 추가, get_lot_view()/get_psi_matrix() Polars 피벗 구현, GET /pl/lot-view + /pl/psi-matrix 엔드포인트 추가, DP set-target → BackgroundTask 자동 재계산, PartListPage.tsx 3탭(요약/Lot뷰/PSI매트릭스) 구현
- Ref: Phase19_Coder_Instructions.md (삭제됨), Phase19_Coder_Report.md (삭제됨)

---

---

- Date: 2026-05-17
- Role: Chief / Coder (Gemini)
- Action: Phase 20 완료 — User Assignment (Expeditor→Vendor 담당자 배정)
- Reason: 담당자별 필터링 기능 제공 및 개인화 대시보드/알림 시스템 기반 마련
- Result: UserAssignment SQLModel(user_assignments 테이블, UNIQUE user+resource_type+key), Alembic migration 20_user_assignments.py, Admin API (GET/POST/DELETE /admin/assignments, GET /admin/lines), AdminPage.tsx 4번째 탭 "담당 배정" 2패널 UI 구현
- Ref: Phase20_Coder_Instructions.md (삭제됨), Phase20_Coder_Report.md (삭제됨)

---

---

- Date: 2026-05-17
- Role: Chief / Coder (Gemini)
- Action: Phase 21 완료 — PSI 모듈 전면 재구성 (4행 블록 + 입출고 기록)
- Reason: 엑셀 실물 PSI 양식과 동일한 4행 블록 구조(소요/입고/불량/잔량) 구현 및 실시간 재고 시뮬레이션 기능 제공
- Result: PsiDailyRecord SQLModel(psi_daily_records), Alembic migration 21_psi_daily_records.py, build_psi_matrix_v2() balance 누적 계산, GET /psi/matrix-v2 + PUT /psi/daily-records/upsert + PATCH /psi/items/{id}/inventory API, PSIPage.tsx 2탭(매트릭스/입출고기록), PSIMatrixV2.tsx 4행 블록 rowSpan 인라인 편집, PSIRecordsTab.tsx CRUD
- Ref: Phase21_Coder_Instructions.md (삭제됨), Phase21_Coder_Report.md (삭제됨)

---

---

- Date: 2026-05-18
- Role: Chief / Coder (Gemini)
- Action: Phase 22 (Task A~H) 완료 — PartList UX 개선 & Vendor 파싱 수정
- Reason: CN 코드 협력사 파싱 버그 수정 및 Lot View 가독성·BOM 연동 강화
- Result: parse_vendor_name() split 기반 유틸(core/utils.py) 도입, LotViewResponse part_meta 스키마 확장, get_lot_view() description/uom 추가, LotView 2행 헤더(Description/품번+UOM), BOM 더블클릭 /bom/:model 이동, PSI Matrix UOM 고정 열 추가, PartList 필터 패널(Expeditor/SupplyType/Line), /pl/filter-options 엔드포인트
- Ref: Phase22_Coder_Instructions.md, Phase22_Coder_Report.md

---

---

- Date: 2026-05-18
- Role: Chief (Claude)
- Action: Phase 22 (Task I~N) 지시문 작성 완료 — PSI 다중 필터 & 열 재배치
- Reason: PSI 매트릭스에 Line/Model.Suffix/WorkOrder 다중선택 필터 및 고정열 순서 변경 요구사항 반영
- Result: 22-I GET /psi/filter-options 엔드포인트 설계, 22-J build_psi_matrix_v2() line/model/wo 필터 확장, 22-K 라우트 파라미터 확장, 22-L MultiSelectCombobox 컴포넌트 설계, 22-M PSIPage 필터 확장, 22-N PSIMatrixV2 열 재배치(협력사→2차협력사→품번→품명→재고, FIXED_COLS_WIDTH=512)
- Ref: Phase22_Coder_Instructions.md (하단 섹션)

---

## 현재 시스템 상태 (2026-05-18 기준)

### 서버 프로세스
| 서버 | 명령 | 포트 |
|---|---|---|
| 백엔드 | `venv/bin/uvicorn main:app --host 0.0.0.0 --port 8000 --reload` | 8000 |
| 프론트엔드 | `vite --port=3000 --host=0.0.0.0` | 3000 |

### 백엔드 (backend/)
| 구분 | 상태 |
|---|---|
| FastAPI + uvicorn | 정상 가동 |
| PostgreSQL 16 | 정상 |
| Alembic 마이그레이션 | 21_psi_daily_records 포함 최신 적용 |
| JWT Auth | 완료 (admin@lars.local / admin1234) |
| BOM API | Tree + Amount View + Reverse Lookup |
| DP API | Batch 목록/조회/삭제, Target 관리, /dp/lots-raw |
| PartList API | /pl, /pl/lot-view, /pl/psi-matrix, /pl/filter-options, /pl/export |
| PSI API | 4행 블록 매트릭스 V2, 입출고 기록 CRUD, /psi/filter-options |
| ItemMaster API | Redis 캐싱, Background rebuild, parse_vendor_name() |
| User Assignment | user_assignments 테이블, /admin/assignments |
| core/utils.py | parse_vendor_name() 공유 유틸 |

### 프론트엔드 (.WebUI/)
| 구분 | 상태 |
|---|---|
| BOM 상세 | Tree View + Amount View + 단일 suffix 단일행 표시 |
| DP Viewer | Batch 콤팩트 선택바 + Raw Table 라인필터 + Print View |
| PartList | 3탭(요약/Lot뷰/PSI매트릭스) + 필터 패널(Expeditor/SupplyType/Line) |
| PartList Lot뷰 | 2행 헤더(Description/품번+UOM), BOM 더블클릭 이동 |
| PartList PSI | Description→협력사→2차협력사→품번 고정열, 주차+요일 2행 날짜헤더, UOM열 |
| PSI 매트릭스 V2 | 4행 블록 rowSpan 인라인 편집, Expeditor/SupplyType 필터 |
| Admin | 담당자 배정 탭 (Vendor/Line/Model 배정 관리) |

### 실데이터 현황
| 테이블 | 수량 |
|---|---|
| BOM 모델 | 196개 |
| ItemMaster (활성) | 9,993개 |
| DP 배치 | 5개 (Target: id 629) |

### 미완료 / 다음 과제
| 항목 | 우선도 | 비고 |
|---|---|---|
| Phase 22-I~N 구현 | **High** | PSI 다중필터(Line/Model/WO) + PSIMatrixV2 열 재배치. 지시문: Phase22_Coder_Instructions.md |
| ERP 연동 import | Medium | data_source="erp" 태그 체계 완비, API 미구현 |
| pytest 단위 테스트 | Low | 미착수 |

---

## 주요 설계 결정 기록

1. **ModelNumber = Model.Suffix** — `LSGL6335X.ARSELGA` 형식이 BOM/DP/PSI 전체의 고유 키. bare model_code 단독 사용 금지
2. **Polars 전용** — 모든 DataFrame 연산에서 Pandas 사용 금지
3. **AI_MODE 4단계** — disabled / local / internal / cloud (.env 하나로 전환)
4. **BOM upsert** — delete+insert 폐기 → sort_order 기준 PK 보존 update/insert/delete
5. **is_active 패턴** — FK 참조 레코드(item_master 등) DELETE 불가 → is_active=False
6. **ItemMaster 완제품 제외** — `@CVZ.EKHQ` suffix 부품은 rebuild 시 is_active=False (자재관리 대상 아님)
7. **BOM 대체품(level=-1)** — tree 노드 아님, pathToNode Map으로 본부품 substitutes[]에 연결
8. **data_source 태그** — import_batches.data_source: "local"(수동) / "erp"(ERP연동, 향후)
9. **lars_ai_service 분리** — NAS(저사양)와 AI PC(RTX 4090)를 HTTP로 분리, GPU 추론 전담
10. **Vite Proxy** — 원격 접속 시 상대경로(/api/v1) 사용, IP 하드코딩 없음

---

> 상세 내용: `LARS_Project/LARS_Consolidated_Report.md` 참조
