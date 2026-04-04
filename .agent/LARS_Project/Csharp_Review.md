# 레거시 C# 자산 리뷰

> 마지막 갱신: 2026-04-05
> 이 문서는 현재 저장소의 WPF/C# 구현을 `최종 방향`이 아닌 `참고 자산`으로 평가합니다.

## 1. 성격

- 현재 C# 코드는 WPF 데스크톱 앱 기반입니다.
- 최종 목표 구조와는 다르므로 아키텍처를 그대로 계승하지 않습니다.
- 다만 아래 로직은 참고 가치가 있습니다.

## 2. 실제 자산 위치

- 솔루션/프로젝트: `LARS.sln`, `LARS.csproj`
- UI: `Views/`, `ViewModels/`, `App.xaml`
- 서비스 로직: `Services/`
- 도메인 모델: `Models/`

## 3. 재사용 후보

- BOM / DailyPlan / PartList 파싱 규칙의 일부
- PDF 생성 아이디어
- 파일 유효성 검사 로직 일부
- ItemCounter 계산 로직 일부
- Feeder 관리 규칙 일부

## 4. 재사용 비권장 영역

- WPF ViewModel 계층
- 데스크톱 UI 이벤트 흐름
- 경로 관리와 로컬 상태 저장 방식
- WPF 전용 테스트/디버그 API

## 5. 결론

- 현재 C# 구현은 `알고리즘 참고본`으로만 사용합니다.
- 새 플랫폼은 FastAPI/Next.js/PostgreSQL/Redis/Celery 기준으로 별도 설계합니다.
