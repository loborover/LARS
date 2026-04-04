# LARS 프로젝트 인수인계 가이드

> 마지막 갱신: 2026-04-05

## 1. 이 프로젝트를 처음 받았다면

다음 순서로 읽습니다.

1. `.agent/Startup.md`
2. `.agent/User_Profile.md`
3. `.agent/Work_Guide.md`
4. `.agent/Agents/Agent_Rules.md`
5. 사용자에게 역할을 지정받습니다: `Project Leader`, `Coder`, `Quality Assurance Manager`, `Prompt Manager`, `Code Reviewer`, `Teacher`
6. 지정된 역할 문서 하나만 읽습니다.
7. `.agent/LARS_Project/LARS_Project.md`
8. `.agent/LARS_Project/Platform_Architecture.md`
9. `.agent/LARS_Project/Data_Model.md`
10. `.agent/LARS_Project/MVP_Spec.md`
11. `.agent/LARS_Project/Migration_Plan.md`
12. `.agent/LARS_Project/Issues.md`
13. `.agent/LARS_Project/VBA_Review.md`
14. `.agent/LARS_Project/Csharp_Review.md`

## 2. 현재 판단 기준

- 최종 제품은 웹 기반 플랫폼이다.
- AR은 문서 자동화 엔진이다.
- LARS는 AR을 품는 상위 플랫폼이다.
- 레거시 WPF/C# 구현은 참고 자산일 뿐이다.
- `VBA/`, WPF/C# 루트 자산, `TestSet/`은 분석 대상이지만 Source of Truth는 아니다.
- AI 세션은 공통 규칙을 먼저 읽고, 역할 지정 후 해당 역할 문서 하나만 읽어야 한다.
- `Prompt Manager`는 범용 프롬프트 영역인 `.agent/`와 `.agent/Agents/`를 관리할 수 있다.
- Prompt 작성 직군이 아닌 역할은 범용 프롬프트를 수정하거나 삭제할 수 없다.
- Prompt 작성 직군이 아닌 역할의 문서 작성은 `*_Project` 등 프로젝트 종속 영역에서만 수행해야 한다.
- `Code Reviewer`는 `Quality Assurance` 계열의 하위 역할로서 코드 리뷰만 수행한다.
- `Teacher`는 사용자 질문 수준을 가늠해 매우 자세하고 기초적으로 설명하는 교육 역할이다.

## 3. 현재 저장소에서 바로 확인할 자산

- `VBA/`: 원본 비즈니스 로직
- 저장소 루트의 `LARS.sln`, `LARS.csproj`, `Views/`, `ViewModels/`, `Services/`: 레거시 WPF/C# 구현
- `TestSet/`: 샘플 입력, 검증 출력, 간단한 테스트 하네스
- `.agent/LARS_Project/`: 현재 계획과 판단 기준

## 4. 지금 바로 해야 할 일

- `TestSet/` 기준으로 Golden Sample 후보와 기대 출력을 목록화합니다.
- 레거시 VBA의 핵심 규칙을 입력/출력 계약 단위로 추출합니다.
- 새 저장소 구조와 API/Worker 골격 전략을 확정합니다.
- 메타데이터 중심 데이터 모델을 DB 스키마 수준으로 구체화합니다.

## 5. 하지 말아야 할 일

- WPF를 최종 구조로 다시 키우지 않는다.
- VBA의 UI 구조를 웹에 그대로 복제하지 않는다.
- 파일 저장만 하고 metadata 구조화를 미루지 않는다.
- 샘플 자산이 존재하는데도 검증 기준이 전혀 없다고 단정하지 않는다.
