# 에이전트 업무 표준 가이드 (Agent Work Standard Guide)

> 마지막 갱신: 2026-04-04
> 이 문서는 LARS 프로젝트를 포함한 일반 업무 절차를 정의합니다.

## 1. 문서 계층

- `.agent/Identity.md`: 최상위 행동 원칙
- `.agent/User_Profile.md`: 사용자 이해 수준과 커뮤니케이션 전략
- `.agent/Work/Work_Guide.md`: 공통 작업 절차
- `.agent/LARS_Project/*.md`: LARS 프로젝트의 Source of Truth

## 2. 표준 작업 순서

### Phase 1. Sync
- 모든 작업 시작 전에 핵심 문서를 다시 읽고 변경 사항을 반영합니다.
- 프로젝트 방향이 바뀌었는지 확인하고, 낡은 계획이 남아 있으면 즉시 갱신 또는 폐기합니다.

### Phase 2. Plan
- 코드 수정 전에 기획 문서, 구조 문서, 로드맵, 데이터 모델 문서를 먼저 정리합니다.
- 사용자의 요구가 플랫폼 수준 변경이면 코드보다 문서 재정비를 먼저 수행합니다.

### Phase 3. Execute
- 구현은 `아키텍처 -> 데이터 모델 -> API -> 워커 -> UI` 순서로 진행합니다.
- 레거시 코드는 참고하되, 현재 Source of Truth와 충돌하면 Source of Truth를 우선합니다.

### Phase 4. Verify
- 테스트 가능하면 테스트를 수행합니다.
- 테스트가 불가능한 환경이면 제한 사항을 명시하고, 정적 검토 결과를 남깁니다.

### Phase 5. Document
- 구조 변경 시 다음 문서가 최신 상태인지 확인합니다.
- `LARS_Project.md`
- `Platform_Architecture.md`
- `Data_Model.md`
- `MVP_Spec.md`
- `Migration_Plan.md`
- `Issues.md`
- `Handover_Guide.md`

## 3. 문서 작성 원칙

- 한 문서는 하나의 역할만 담당하게 씁니다.
- 과거 계획을 남길 때는 `역사적 참고`인지 `현재 계획`인지 명확히 표시합니다.
- 낡은 계획이 현재 작업을 방해하면 과감히 덮어씁니다.
- 프로젝트를 처음 보는 사람도 문서만 읽으면 다음 행동을 정할 수 있어야 합니다.

## 4. LARS 프로젝트 특수 규칙

- LARS의 최종 목표는 데스크톱 앱이 아니라 `AR 서버엔진 + LARS 플랫폼`입니다.
- WPF/C# 구현은 검증용 자산일 뿐이며, 최종 구조를 제한하지 않습니다.
- 기술 선택의 기준은 `운영성`, `동시성`, `메타데이터 중심 구조`, `AI 연동성`입니다.
