# LARS Platform: Project Plan

> Last updated: 2026-04-13
> Status: Active Phase (Foundation & Architecture)

## 1. Vision & Core Objectives
LARS (Logistics Agent & Reporting System) Platform은 물류/제조 실무 환경에서 인간 작업자의 역할을 완전히 대체하고, 관리자(Manager) 계층과 자연어로 소통하며 자율적으로 업무를 수행하는 초고성능 AI 기반 물류 자동화 웹 서비스입니다.

### 핵심 5대 요구사항
1. **Performance-oriented Web Service:** 최대한 성능 효율적인 아키텍처 구성. 누구나 웹에 접속하여 Human-readable 데이터셋 제공받을 수 있음.
2. **Backend Logic Modernization:** 기존 Excel VBA(BOM, DailyPlan, PartList, itemCounter) 핵심 비즈니스 로직을 분석, 추출하여 최고 효율의 백엔드 서비스로 전환.
3. **Logistics Dashboard:** 누구나 접근 가능한 물류 모니터링 환경 구축. 데이터를 명확한 관리 단위(Republished Cards, Tickets, Nodes 등)로 시각화 및 제어.
4. **AI-Friendly Architecture:** 모든 데이터와 서비스는 Agent AI가 접근, 제어하기 쉽도록(API Tool Calling) 설계. 궁극적으로 자연어 명령만으로 메타데이터 검색 및 가공된 문서 도출 보장.
5. **Autonomous Agent Operations:** 최종 단계에서 LARS AI 에이전트는 실무 환경 모니터링, 외부/내부 작업자와의 대화(통화, 메시지), 회의록 기록 등 실무진 영역을 100% 자율 수행 (인간은 Manager만 존재).

## 2. Phase Roadmap

### Phase 1: Core Backend & Dashboard Foundation (Current)
- 기존 VBA 코드를 역공학하여 핵심 로직 파악 (BOM, DailyPlan, PartList).
- Python 기반 초고성능 백엔드(FastAPI + Polars) 기초 공사.
- React 기반 Logistics Dashboard 기초 구축 (Ticket, Card, Node 개념 도입).

### Phase 2: Data Pipeline & Automation Services
- Excel/PDF 문서 파싱 및 가공 파이프라인 완성.
- VBA 기반의 `AutoReport` 기능을 고성능 REST API 서비스로 전환 완료.
- AI Agent가 활용할 수 있는 형태의 메타데이터 DB 및 검색 API 구축.

### Phase 3: Agent AI Integration
- LangChain / LLM 프레임워크 도입.
- Agent가 백엔드 API(Tools)를 스스로 호출하여 문서를 생성, 수정, 조회하는 구조 완성.
- 자연어 질의응답 및 자율 리포팅 모듈 오픈.

### Phase 4: Autonomous Operations (Ultimate Goal)
- 실시간 통신 및 음성 봇 연동 (전화, 회의 참여).
- 작업자 및 물류 현장 피드백 실시간 모니터링 체계 통합.
- 무인 자율 물류 관리 달성.