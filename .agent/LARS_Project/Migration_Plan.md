# LARS 서버형 Migration Plan

> 마지막 갱신: 2026-04-05
> 이 문서는 현재 유효한 유일한 Migration Plan입니다.

## 현재 기준선

- 문서 기준 Source of Truth는 `.agent/LARS_Project/`입니다.
- 레거시 자산은 `VBA/`, WPF/C# 루트 자산, `TestSet/`에 존재합니다.
- 아직 서버형 `web/api/worker` 구현 골격은 생성되지 않았습니다.

## Phase 0. 문서 정비

- [x] 기존 WPF 중심 계획 폐기
- [x] AR/LARS 정체성 재정의
- [x] 플랫폼 아키텍처 문서화
- [x] 데이터 모델 초안 작성
- [x] MVP 범위 정의
- [x] 현재 저장소 현실 반영 문구 보강

## Phase 1. 레거시 분석 자산 정리

- [ ] VBA 핵심 모듈별 비즈니스 규칙 재정리
- [ ] 현재 C#/WPF 구현에서 재사용 가능한 알고리즘만 추출
- [ ] `TestSet/` 자산을 기준으로 Golden Sample 후보 목록화
- [ ] Golden Sample 입력/출력 계약 확정

## Phase 2. 새 서버 저장소/구조 생성

- [ ] `web`, `api`, `worker` 골격 생성
- [ ] Docker Compose 초안 작성
- [ ] PostgreSQL / Redis 연결 구성
- [ ] 공통 설정 및 환경 변수 정책 수립
- [ ] 별도 저장소로 분리할지, 현 저장소 내 병행 구조로 갈지 결정

## Phase 3. AR 핵심 엔진 구현

- [ ] BOM Processor
- [ ] DailyPlan Processor
- [ ] PartList Processor
- [ ] ItemCounter Engine
- [ ] MultiDocument Matcher
- [ ] PDF Exporter
- [ ] 레거시 대비 검증 테스트 추가

## Phase 4. 메타데이터 계층 구현

- [ ] Job / File / Artifact 스키마 구현
- [ ] 검색 가능한 구조화 메타데이터 저장
- [ ] 감사 로그 구현
- [ ] AI 제안 메타데이터 승인 모델 반영

## Phase 5. MVP Web UI 구현

- [ ] 로그인
- [ ] 업로드 화면
- [ ] 작업 목록/상태 화면
- [ ] 결과 상세 조회 화면
- [ ] PDF 다운로드 화면

## Phase 6. 운영 준비

- [ ] 백업 정책
- [ ] 로그/모니터링
- [ ] 조직/협력사 권한 정책
- [ ] 장애 대응 문서

## Phase 7. LARS 상위 기능 확장

- [ ] 물류 시각화
- [ ] AI metadata read/write
- [ ] 자연어 조회
- [ ] AI chatbot
