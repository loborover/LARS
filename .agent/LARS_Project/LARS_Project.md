# LARS 프로젝트 기획서

> **LARS** = **L**ogistics **A**utomation **R**eport **S**ystem
> 목적지향형 물류 종합(모니터링 및 기획, 보고서 생성) 시스템
> **AutoReport** = **Auto**matic **Report**
> 생산 라인의 BOM / DailyPlan / PartList 데이터를 자동으로 가공·시각화·인쇄하는 보고서 모듈

---

## 1. 프로젝트 개요

### 1.1. 배경

기존에 **Excel VBA 매크로**로 운영되던 생산 라인 보고서 자동화 도구를
**Windows 데스크톱 애플리케이션(WPF + C#)** 으로 재구현하는 프로젝트입니다.

### 1.2. 목적

- VBA/Excel COM 의존성 제거 → 독립 실행형 애플리케이션
- 유지보수성·확장성 향상 (MVVM 아키텍처, DI, 서비스 분리)
- PDF 직접 출력 (Excel 인쇄 기능 불필요)
- 향후 웹 대시보드·DB 연동 등 확장 가능한 구조

### 1.3. 대상 사용자

- 생산관리 담당자 (현장 라인 보고서 출력)
- 자재관리 담당자 (PartList 기반 자재 집계)
- 물류관리 담당자 (PartList 기반 자재 공급 및 공대차 배출 계획)

---

## 2. 핵심 기능

### 2.0. LARS 플랫폼 계층 구조 (Platform Hierarchy)

LARS는 단순한 보고서 자동화 툴을 넘어 **자재/물류 종합 모니터링 시스템** 환경을 구축하기 위해 다음과 같이 최상위 도메인을 분할합니다.

- **📊 AutoReport (기존 물류 보고서 모듈)**
  - **Documents**: 실제 보고서 형식으로 출력/조회되는 결과물 탭 (BOM, DailyPlan, PartList, MultiDocuments, ItemCounter)
    - *각 뷰어 탭 상단에 '🧩 에디터' 버튼이 배치되어 VME 팝업 창 즉시 호출 가능.*
    - **UI 개편 및 파일목록 관리 체계 (Side-by-side & 개별 파일 통제)**:
      - 기존의 상/하단 분할 구조(Expander)를 폐지하고 **좌(파일 목록) / 우(미리보기 뷰어)의 사이드-바이-사이드 레이아웃**을 전면 도입했습니다. `GridSplitter`를 제공하여 사용자 편의에 맞게 창 영역을 조절할 수 있습니다.
      - 이제 스캔된 파일 목록의 **개별 행을 우클릭(컨텍스트 메뉴)**하여 구체적인 작업(오른쪽 미리보기에 띄우기, 엑셀 프로그램으로 원본 열기, 원본 파일 물리적 영구 삭제)을 곧바로 수행할 수 있는 완벽한 물리 파일 통제 관리 기능을 제공합니다.
      - **Deep Validation 지원**: 스캔 단계에서 단순 파일명 매칭(1차)에 그치지 않고, 후진적인 런타임 오류를 방지하기 위해 엑셀 파일을 백그라운드에서 직접 열어 헤더 패턴이나 식별 셀을 분석(2차)하는 VBA 수준의 **이중 진위 판별 스캔 로직**이 이식되었습니다.
  - **Basic info**: 보고서 가공을 위한 기초 설정 및 데이터베이스 관리 탭 (Feeder Management)
- **🤖 Ai (인공지능 통제 및 모니터링 모듈 - 신규 도메인)**
  - **LLM**: 로컬 또는 클라우드 언어 모델 관리 / 프롬프팅
  - **Calling**: 외부 시스템 API 호출 및 데이터 스크래핑 제어
  - **ChatLog**: 사용자 질의응답 내역 기록 및 프롬프트 로깅

### 2.1. BOM Viewer

- **입력**: `@CVZ` 패턴이 포함된 Excel 파일 (SAP BOM Export)
- **처리**: 불필요 열 삭제, BOM Level 필터링(0/1/2/3/S/Q), 모델명 자동 추출
- **출력**: 가공된 BOM 표시 + PDF 출력

### 2.2. DailyPlan Viewer

- **입력**: `Excel_Export_` 패턴 파일 (MES DailyPlan Export)
- **처리**: 날짜/라인 메타 파싱, 불필요 열 삭제, 모델 그루핑(ModelGrouping), 시각화
- **출력**: 모델별 그룹 경계 표시 + 요일 색상 + PDF 출력

### 2.3. PartList Viewer

- **입력**: `Excel_Export_` 패턴 파일 (MES PartList Export)
- **처리**: ★ 가장 복잡한 파이프라인
  - 투입시점 병합 (YYYYMMDD + Input Time)
  - D-Day N일 트리밍
  - 모델+Suffix 병합, 동일 부품 열 합치기(PartCombine)
  - 중복 W/O 행 제거+합산, 벤더명 정규화(Replacing_Parts)
  - Feeder 기반 컬럼 필터링
- **출력**: 정규화된 PartList 표시 + PDF 출력

### 2.4. ItemCounter

- **입력**: 가공된 PartList 데이터
- **처리**: 셀 텍스트 → itemUnit 분해 (Re_Categorizing), ID_Hash 기준 병합, 날짜별 Count 집계
- **출력**: 자재별 날짜별 투입 수량 테이블

### 2.5. MultiDocuments

- **입력**: DailyPlan + PartList 파일 폴더
- **처리**: 파일명에서 날짜+라인 추출 → 교집합 매핑
- **출력**: 매칭된 문서 쌍 ListView + 일괄 처리

### 2.6. Visual Macro Editor (VME) [독립 팝업 창]

- **역할**: 사용자가 코딩 없이 **드래그&드롭 블록 에디터**로 데이터 가공 파이프라인(매크로)을 직접 조립·실행할 수 있게 하는 시각적 자동화 도구.
  - 내장 탭이 아닌 **독립된 팝업 창(Window)** 형태로 동작하며, **Documents**의 모든 하위 탭에 있는 '🧩 에디터' 버튼을 통해 쉽게 접근 가능합니다.
- **배경**: 현장 담당자마다 원하는 보고서 양식이 다르지만, 관리자가 모든 요청에 코드를 수정해줄 수 없음. VBA 매크로보다 훨씬 낮은 진입 장벽으로 사용자 자율 커스터마이징을 지원.
- **핵심 기능**:

  | 기능 | 설명 |
  |------|------|
  | **블록 팔레트** | 입력/열조작/행조작/변환/집계/출력 카테고리의 14종 블록을 제공 |
  | **파이프라인 캔버스** | 블록을 좌→우로 배치하여 실행 순서를 시각적으로 편집 |
  | **속성 패널** | 선택된 블록의 세부 설정(대상 열, 필터 조건 등)을 편집 |
  | **매크로 실행 엔진** | `MacroRunner`가 토폴로지 정렬로 블록을 순차 실행, 결과를 DataTable로 반환 |
  | **Raw / Processed View** | 매크로 실행 전 원본(Raw)과 실행 후 가공 결과(Processed)를 나란히 비교하는 이중 뷰 |
  | **JSON 저장/불러오기** | 매크로를 JSON으로 영속화, 타 PC로 이식 가능 |

- **향후 확장 — Drawing Engine (Shape 조작)**:
  - **Drawing Engine**을 도입하여 기본 도형(Primitive Shape)인 `Box`, `TextBox`, `Line`, `Dot` 등을 제공.
  - 사용자는 기본 도형을 **Viewport** 위에 자유롭게 배치·조합하여 라벨, 스티커, 보고서 헤더 등 목적에 맞는 **응용 도형(Composite Shape)**을 만들 수 있음.
  - 이를 통해 데이터 가공뿐 아니라 **보고서 외형 디자인·인쇄 레이아웃**까지 사용자가 자유롭게 커스터마이징하는 올인원 에디터로 발전.
- **책임 범위**:
  - 기존 하드코딩 파이프라인(`ProcessBomForExport` 등)을 **궁극적으로 VME로 완전 대체**할 예정. 즉시 레거시 로직을 제거.
  - 매크로 실행 중 오류 발생 시, 사용자에게 어떤 블록에서 문제가 생겼는지 명확하게 피드백.

### 2.7. Performance (Multi-core Computing)

- **개요**: 실무 환경에서 발생하는 거대한 양의 문서(수십~수백 개)를 한 번에 즉각적으로 인식하고 가공할 수 있도록 돕는 병렬 처리 아키텍처 지원.
- **특징**: 스레드 및 코어 자원을 최대한 활용하여 백그라운드에서 데이터를 분산 처리함으로써 UI의 멈춤 현상을 방지하고 작업 소요 시간을 획기적으로 줄이는 것이 목표.

### 2.8. StickerLabel → Drawing Engine 흡수 예정

- **현재**: 라벨 데이터(모델명, W/O, 수량 등) → A4 그리드 레이아웃 → PDF 라벨 인쇄.
- **향후**: VME의 **Drawing Engine** 기반으로 재구현. 사용자가 기본 도형(`Box`, `TextBox`, `Line`, `Dot`)을 조합하여 라벨 양식을 직접 디자인하고, Viewport에서 실시간 미리보기 후 PDF 출력하는 구조로 전환.
- 기존 `StickerLabelService`의 하드코딩 레이아웃은 Drawing Engine 완성 시 대체됨.

### 2.9. AI Agent Testability (자동 검증 인프라)

- **목적**: AI 에이전트가 LARS의 기능을 자동으로 실행·조회·검증할 수 있는 인터페이스를 제공합니다. 터미널(pwsh)에서 명령 한 줄로 ViewModel 상태 조회, 매크로 실행, 데이터 검증, 스크린샷 캡처가 가능합니다.
- **구성 요소**:
  1. **내장 Debug HTTP API** (`#if DEBUG` 전용)
     - LARS.exe 실행 시 `localhost:19840`에 경량 HTTP 서버 활성화
     - 엔드포인트: ViewModel 상태 조회, 매크로 실행, DataGrid 데이터 JSON 반환, 스크린샷 PNG 캡처
     - AI 에이전트가 `curl`/`Invoke-WebRequest`로 호출하여 앱 상태를 실시간 검증
  2. **Headless ViewModel 테스트** (`TestSet/` 콘솔 프로젝트)
     - UI 없이 ViewModel/Service 레벨에서 데이터 가공 로직을 직접 실행·검증
     - `dotnet run`으로 실행 → 결과를 JSON/콘솔 출력 → AI 에이전트가 파싱
- **원칙**:
  - Debug API는 `#if DEBUG` 가드로 릴리스 빌드에 절대 포함되지 않음
  - 프로덕션 코드에 테스트 전용 의존성을 추가하지 않음
  - 테스트 결과는 `TestSet/VerificationOutput/`에 저장

---

## 3. 기술 스택

| 영역 | 기술 | 비고 |
|------|------|------|
| **언어** | C# (.NET 8) | |
| **UI 프레임워크** | WPF | MVVM 패턴 |
| **MVVM 도구** | CommunityToolkit.Mvvm | RelayCommand, ObservableProperty |
| **DI** | Microsoft.Extensions.DependencyInjection | |
| **Excel 읽기** | ClosedXML | 읽기 전용, 파일 변경 없음 |
| **PDF 출력** | PdfSharpCore | A4 기준 레이아웃 |
| **설정 저장** | JSON (`%AppData%/LARS/settings.json`) | SettingsService |
| **Feeder 저장** | JSON (`%AppData%/LARS/feeders.json`) | FeederService |

---

## 4. 아키텍처 (Domain Driven Structure)

향후 다중 도메인(AutoReport, Ai 등) 지원을 위해 기능적 목적에 맞게 폴더와 네임스페이스를 관리합니다.

```text
LARS/
├── Core/                ← 공통 기반 시스템 (의존성: Utils, Config 등)
├── Models/              ← 도메인 개체 (ItemUnit, VME Node, Ai 모델 등)
├── Services/            ← 비즈니스 로직
│   ├── AutoReport/      ← 보고서/VME 종속 서비스 (BomService, MacroRunner 등)
│   └── Ai/              ← 인공지능 통제 종속 서비스 (LlmService, Calling 등)
├── ViewModels/          ← MVVM ViewModel
│   ├── MainViewModel    ← 최상위 도메인 내비게이션 (AutoReport ↔ Ai 전환)
│   ├── AutoReport/      ← AutoReport 하위 뷰모델들
│   └── Ai/              ← Ai 제어 하위 뷰모델들
├── Views/               ← WPF XAML
│   ├── MainWindow
│   ├── AutoReport/
│   └── Ai/
├── Converters/          ← WPF 값 변환기
├── Utils/               ← 유틸리티 헬퍼
├── Themes/              ← 테마/리소스 딕셔너리
├── VBA/                 ← VBA 원본 소스 (참조용)
└── .agent/
    ├── Identity.md      ← 에이전트 행동 강령
    ├── Work/            ← 범용 공통 업무 가이드
    └── LARS_Project/    ← LARS 종속 기획 및 리뷰 문서 (이 파일 포함)
```

### 4.1. 핵심 원칙

- 서비스는 **순수 C#**, VBA/COM 의존성 없음
- 모든 I/O: **async/await + Task.Run**
- UI와 로직 분리: **MVVM** (ViewModel → Service → Model)
- Excel Shape 드로잉(VBA Painter/StickerLabel) → **WPF DataGrid/Canvas + PDF 렌더링**으로 대체

---

## 5. 원본 참조

- **VBA 원본 코드**: `VBA/Original.md` (6,739줄)
- **VBA 분석 리뷰**: `.agent/Work/VBA_Review.md`
- **C# 코드 리뷰**: `.agent/Work/Csharp_Review.md`
- **이관 개발 계획**: `.agent/Work/Migration_Plan.md`

---

## 6. 개발 진행 현황

| 단계 | 상태 | 내용 |
|------|------|------|
| Sprint 0~9 | ✅ 완료 | BOM/DailyPlan/PartList 기본 기능, PDF 출력, 설정/Feeder, ItemCounter, MultiDoc, StickerLabel |
| Sprint 10 | 🔴 대기 | PartList 핵심 가공 파이프라인 (AR_1 전체) |
| Sprint 11 | 대기 | ItemCounter 파싱 정밀 검증 |
| Sprint 12 | 대기 | DailyPlan 가공 + ModelGrouping |
| Sprint 13 | 대기 | 파일 유효성 검증 + BOM Level 필터 |
| Sprint 14 | 대기 | VBA↔C# 전체 대조 검증 |

> 상세 Sprint 내용: [Migration_Plan.md](file:///d:/Workshop/LARS/.agent/Work/Migration_Plan.md)

---

## 7. 유지보수 프로세스

### 7.1. 빌드 후 정합성 검증

- **매 빌드 완료 시**, 에이전트는 이 기획서(LARS_Project.md)와 실제 소스코드의 정합성을 자동으로 검증합니다.
- 기획서에 명시되지 않은 코드가 발견되면 사용자에게 보고하고, 불필요한 경우 과감히 삭제하여 프로젝트를 최적 상태로 유지합니다.

### 7.2. 코드 리뷰 자동 갱신

- **매 빌드 완료 시**, 에이전트는 프로젝트 전체를 분석하여 [Csharp_Review.md](file:///d:/Workshop/LARS/.agent/Work/Csharp_Review.md) 파일을 최신 상태로 갱신합니다.
- 리뷰 항목: 파일별 역할 요약, 코드 품질, 개선 제안 등.

### 7.3. 아키텍처 동기화

- 소스 폴더 구조가 변경될 때마다, 이 문서의 §4 아키텍처 트리를 업데이트하여 문서와 코드가 항상 1:1 대응되도록 합니다.
