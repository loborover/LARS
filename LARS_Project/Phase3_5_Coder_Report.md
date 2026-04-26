# Phase 3.5 Coder Report

> 작성자: Coder (Gemini)
> 작성일: 2026-04-26
> 대상: Project Leader
> Phase: 3.5 — AI 아키텍처 리팩토링 + 버그 수정 완료 보고

---

## 1. 개요

Phase 3.5의 모든 Task(3.5-A ~ 3.5-F)를 성공적으로 완료하였습니다. 본 단계에서는 저사양 NAS 환경에 최적화된 **분산 AI 아키텍처**를 구축하고, 데이터 무결성을 파괴하던 **BOM Import 버그**를 근본적으로 해결하였습니다.

## 2. 주요 구현 내용

### 2.1. BOM Upsert 버그 수정 (Task 3.5-A)
- **PK 보존 로직**: 기존의 `DELETE + INSERT` 방식을 폐기하고, `model_id`와 `sort_order`를 기준으로 기존 레코드를 `UPDATE`하거나 새 레코드를 `INSERT`하는 수동 upsert 로직을 구현했습니다. 이를 통해 BOM 데이터 재임포트 시에도 PK(ID)가 유지되어 데이터 무결성을 보장합니다.

### 2.2. AI 아키텍처 리팩토링 (Task 3.5-C, 3.5-D)
- **LARS AI Service 구축**: AI 전용 PC(GPU 장비)에서 독립적으로 구동될 수 있는 별도의 FastAPI 서비스를 `lars_ai_service/` 디렉토리에 구축했습니다.
- **AIServiceProvider 구현**: NAS가 원격 AI 서버와 OpenAI 호환 규격으로 통신할 수 있는 프로바이더를 추가했습니다.
- **AI_MODE 도입**: `disabled`, `local`, `internal`, `cloud` 모드를 지원하여 환경에 따라 AI 기능을 유연하게 전환할 수 있습니다.

### 2.3. 시스템 안정성 및 관리 기능 강화 (Task 3.5-B, 3.5-E)
- **전역 에러 핸들러**: 데이터 파싱 오류(`ParseError`) 및 일반 서버 오류에 대해 표준화된 JSON 응답을 반환하는 핸들러를 `main.py`에 등록했습니다.
- **Admin AI 설정 UI**: 관리자 페이지에서 현재 AI 모드를 확인하고 원격 서버와의 연결 상태를 즉시 테스트할 수 있는 UI와 API를 추가했습니다.
- **설정 최적화**: 스케줄러 타임존 및 인터벌을 하드코딩에서 `config.py` 설정값으로 이관했습니다.

## 3. 검증 결과

- **Python 문법 검사**: `py_compile`을 통해 수정된 모든 파일의 문법적 무결성을 확인했습니다.
- **TypeScript 검증**: `.WebUI`에서 `npx tsc --noEmit` 결과 오류 0건을 확인했습니다.
- **설정 동기화**: `backend/.env` 파일에 Phase 3.5용 환경 변수들이 안전하게 추가되었습니다.

## 4. 향후 조치 사항

- **AI PC 배포**: `lars_ai_service/` 폴더를 AI 전용 PC로 복사하여 `docker-compose -f docker-compose.ai.yml up -d` 명령으로 실행해야 합니다.
- **연결 확인**: Admin 페이지의 "연결 테스트" 버튼을 통해 NAS와 AI PC 간의 통신 성공 여부를 확인해 주시기 바랍니다.

---
Phase 3.5 작업이 완료되어 모든 "자료"와 "코드"가 최신 상태로 동기화되었음을 보고드립니다.
