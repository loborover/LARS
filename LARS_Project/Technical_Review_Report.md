# LARS Platform: Technical Code Review Report

> Last updated: 2026-04-26
> Author: Code Reviewer (AI Agent)
> Status: Active Phase (Foundation & Architecture) Review

## 1. 아키텍처 및 기술 스택 평가 (Strengths)

*   **고성능 파이프라인 구성:** FastAPI의 비동기(Async) 처리와 Polars/fastexcel 조합은 대용량 엑셀(BOM, DailyPlan) 데이터를 처리하는 데 있어 최상의 성능을 낼 수 있는 탁월한 아키텍처입니다. 기존 VBA 매크로 대비 압도적인 성능 향상이 기대됩니다.
*   **명확한 관심사 분리(SoC):** `api(Router)` -> `services(Business Logic)` -> `parsers(Data Processing)`로 이어지는 레이어 분리가 잘 되어 있어, 향후 AI Agent가 Tool로써 각 서비스를 호출(Tool Calling)하기에 매우 적합한 구조입니다.
*   **최신 프론트엔드 생태계:** React 19, Vite, Zustand, React Query v5, Tailwind CSS v4 등 프론트엔드 성능과 개발 생산성을 극대화할 수 있는 모던 스택이 완벽히 구성되어 있습니다.

## 2. 버그 및 정확성 위험 (Bugs & Correctness Risks)

*   **BOM 데이터 병합(Upsert) 로직의 결함 (`backend/services/bom_service.py`)**
    *   **문제점:** `import_from_df` 함수에서 기존 BOM 아이템을 갱신할 때, 주석에는 "upsert"를 명시했으나 실제로는 `delete()` 후 `add_all()`을 통해 기존 데이터를 전체 삭제하고 재삽입하고 있습니다.
    *   **위험성:** 이 방식은 매 임포트마다 `BomItem.id`(Primary Key)를 새로 발급하게 만듭니다. 만약 이후 구현될 `Ticket`, `WIP`, `DailyPlan` 모델 등에서 `bom_items.id`를 Foreign Key로 참조하고 있다면, 임포트 즉시 모든 연관 데이터가 깨지거나(Cascade Delete) 고아(Orphan) 데이터가 되는 치명적 데이터 무결성 훼손이 발생합니다.

## 3. 회귀 및 유지보수 위험 (Maintainability & Regression Risks)

*   **단위 테스트(Test Coverage) 전면 누락**
    *   **문제점:** 백엔드(`backend/tests/`) 및 프론트엔드 모두 테스트 코드가 전무합니다. 
    *   **위험성:** 특히 `bom_parser.py`의 `_compute_paths`나 `_deduplicate` 함수는 계층형 트리 데이터를 다루는 매우 복잡한 로직입니다. 테스트 코드 없이 추후 유지보수를 진행할 경우, 엣지 케이스(대체품 계산, 깊이 변경 등)에서 회귀 버그(Regression)가 발생할 확률이 극도로 높습니다.
*   **전역 예외 처리(Global Exception Handling) 부재**
    *   **문제점:** 파서에서 발생하는 `ParseError`나 DB 무결성 에러에 대한 FastAPI 전역 예외 처리기가 없습니다.
    *   **위험성:** 파일 파싱에 실패하거나 엑셀 포맷이 다를 경우, 클라이언트(혹은 AI Agent)에게 유용한 에러 메시지가 아닌 서버 내부 에러(HTTP 500)가 반환되어 디버깅과 자동화 복구를 방해합니다.
*   **하드코딩된 스케줄러 환경 (`backend/main.py`)**
    *   **문제점:** PSI 모니터링 스케줄러의 타임존이 `Asia/Seoul`로 하드코딩되어 있습니다.
    *   **위험성:** 글로벌 물류 환경에 배포 시 시간대 불일치 오류를 야기할 수 있습니다.

## 4. 개선 권고안 (Recommendations)

1.  **안전한 Upsert 로직으로 리팩토링:** `bom_service.py`의 데이터 임포트 로직을 전체 삭제/재삽입 방식에서 PostgreSQL의 `ON CONFLICT DO UPDATE` 구문이나 SQLAlchemy의 `merge()`를 활용하여 Primary Key를 보존하는 진정한 Upsert 방식으로 변경해야 합니다.
2.  **테스트 스위트(Test Suite) 즉각 도입:** 가장 복잡한 비즈니스 로직인 `parsers/bom_parser.py` 및 `parsers/daily_plan_parser.py`에 대해 `pytest` 기반의 단위 테스트를 최우선으로 작성해야 합니다.
3.  **에러 핸들링 미들웨어 추가:** `backend/main.py`에 `@app.exception_handler(ParseError)` 등을 추가하여 파싱 에러 발생 시 HTTP 400 Bad Request와 함께 원인(예: "필수 컬럼 누락")을 JSON으로 명확하게 반환하도록 개선하십시오.
4.  **설정 유연성 확보:** 타임존, 스케줄러 인터벌, Redis 연결 상태 등의 인프라 종속적인 값들은 `.env` 기반의 `core.config`를 거쳐 동적으로 주입되도록 수정하는 것을 권장합니다.

---
> 본 문서는 Code Reviewer 에이전트에 의해 작성되었습니다. 구현 담당자(Coder) 혹은 총괄(Chief) 역할 수행 시, 위 권고안을 바탕으로 안전하게 시스템을 수정 및 개선할 수 있습니다.