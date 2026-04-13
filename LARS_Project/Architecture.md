# LARS Platform: Architecture

> Last updated: 2026-04-13

## 1. System Overview
LARS 플랫폼은 "AI-Friendly"와 "Performance"를 최우선 목표로 하는 현대적 웹 아키텍처를 채택합니다. Python의 강력한 AI 생태계를 활용하면서, 데이터 처리 병목을 최소화하는 고성능 라이브러리를 결합합니다.

## 2. Technology Stack

### 2.1. Backend (Core API & AI Gateway)
- **Framework:** Python `FastAPI` (비동기 I/O 기반 초고속 REST/GraphQL API)
- **Data Processing:** `Polars` (Rust 기반의 초고속 데이터프레임 라이브러리, Pandas 대비 압도적 성능으로 대규모 Excel/CSV, BOM 데이터 연산 처리)
- **AI / Agent:** `LangChain` 또는 `CrewAI` (역할 기반 멀티 에이전트 오케스트레이션)
- **Database:** `PostgreSQL` (메타데이터 및 로지스틱스 트랜잭션), `Vector DB` (자연어 검색 및 회의록 임베딩 검색용)

### 2.2. Frontend (Logistics Dashboard)
- **Framework:** `React.js` + `Vite` + `TypeScript` (기존 `.WebUI` 디렉토리 활용)
- **UI Components:** `shadcn/ui` 기반의 Ticket, Card, Node 시스템 (대시보드 위젯형 설계)
- **State Management:** `Zustand` 또는 `React Query`

## 3. Core Modules

### 3.1. AutoReport Engine (Legacy VBA Migration)
- **BOM Service:** 레벨(`Lvl`) 파싱 및 다단계 구조 트리를 Graph Node 형태로 변환하여 캐싱.
- **DailyPlan Service:** 엑셀 시트 형태를 벗어나, 날짜별/라인별 생산 계획을 Ticket 단위 DB 레코드로 변환 후 View Layer에서 조합.
- **PartList Service:** 필요 부품 및 재고 매핑, Agent가 즉시 쿼리할 수 있는 JSON/API 형태 제공.

### 3.2. Dashboard Entities
- **Card:** 요약된 메타데이터를 나타내는 대시보드 컴포넌트 (예: 오늘 생산량, 부족한 자재).
- **Ticket:** 수행해야 할 구체적 작업 단위 (예: "BOM 업데이트 승인 대기", "자재 발주 요망"). AI Agent가 스스로 Ticket을 발행하고 Close 할 수 있음.
- **Node:** 시스템 혹은 공급망 상의 특정 지점 (예: 특정 라인, 특정 벤더).

### 3.3. Autonomous Agent Layer
- **Input/Output:** 사용자의 자연어, 이메일, 외부 시스템의 Webhook.
- **Action Planner:** LLM이 `AutoReport Engine` API, `Dashboard` API (Ticket 발행 등) Tool을 직접 호출(Tool Calling)하여 실무 수행.
- **Monitoring:** 로그, 에러, 작업 상태를 실시간으로 분석하고 이상 발생 시 Manager에게 보고서(Card 형태) 송출.