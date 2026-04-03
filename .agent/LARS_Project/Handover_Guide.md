# LARS 프로젝트 인수인계 가이드

> 마지막 갱신: 2026-04-04

## 1. 이 프로젝트를 처음 받았다면

다음 순서로 읽습니다.

1. `.agent/Identity.md`
2. `.agent/User_Profile.md`
3. `.agent/Work/Work_Guide.md`
4. `.agent/LARS_Project/LARS_Project.md`
5. `.agent/LARS_Project/Platform_Architecture.md`
6. `.agent/LARS_Project/Data_Model.md`
7. `.agent/LARS_Project/MVP_Spec.md`
8. `.agent/LARS_Project/Migration_Plan.md`
9. `.agent/LARS_Project/Issues.md`
10. `.agent/LARS_Project/VBA_Review.md`
11. `.agent/LARS_Project/Csharp_Review.md`

## 2. 현재 판단 기준

- 최종 제품은 웹 기반 플랫폼이다.
- AR은 문서 자동화 엔진이다.
- LARS는 AR을 품는 상위 플랫폼이다.
- 레거시 WPF/C# 구현은 참고 자산일 뿐이다.

## 3. 지금 바로 해야 할 일

- 레거시 VBA의 핵심 규칙을 Golden Sample 기준으로 추출한다.
- 새 저장소 구조와 API/Worker 골격을 만든다.
- 메타데이터 중심 데이터 모델을 DB 스키마로 구체화한다.

## 4. 하지 말아야 할 일

- WPF를 최종 구조로 다시 키우지 않는다.
- VBA의 UI 구조를 웹에 그대로 복제하지 않는다.
- 파일 저장만 하고 metadata 구조화를 미루지 않는다.
