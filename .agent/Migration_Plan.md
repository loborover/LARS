# LARS Migration Plan (Master)

> **운영 규칙**: 각 Sprint를 별도 세션에서 실행. 완료 시 ✅, 미완료 ❌, 부분완료 ⚠️ 표시.  
> **마지막 검증**: 2026-02-24

---

## 현재 구현 상태 (검증 완료)

| 기능 | 메서드 | 상태 | 파일 |
|------|--------|------|------|
| BOM 컬럼 필터 + 타이틀 추출 | `ProcessBomForExport` | ✅ 구현됨 | `ReportServices.cs` |
| BOM 전용 PDF (열너비 비율) | `ExportBomToPdf` | ✅ 구현됨 | `PdfExportService.cs` |
| DailyPlan 날짜/라인 메타 파싱 | `ReadMetaFromFile` | ✅ 구현됨 | `ReportServices.cs` |
| DailyPlan 메타 DTO | `DailyPlanMetadata` | ✅ 구현됨 | `ReportServices.cs` |
| PartList 자재 셀 정규화 | `NormalizeCellValue` | ✅ 구현됨 | `ReportServices.cs` |
| Feeder 기반 컬럼 필터 | `FilterByFeeder` | ✅ 구현됨 | `ReportServices.cs` |
| 전체 async/await 전환 | 10개 RelayCommand | ✅ 구현됨 | `MainViewModel.cs` |
| DailyPlan PDF 전용 | `ExportDailyPlanToPdf` | ✅ 구현됨 | `PdfExportService.cs` |
| 공통 PDF (열너비 비율 적용) | `ExportWithColumnRatios` | ✅ 구현됨 | `PdfExportService.cs` |
| PartList PDF | `ExportPlPdf` (async) | ✅ 구현됨 | `MainViewModel.cs` |
| Feeder JSON 저장/로드 | `FeederService` | ✅ 구현됨 | `FeederService.cs` |
| 설정 경로 자동 폴더 생성 | `DirectoryManager` | ✅ 구현됨 | `DirectoryManager.cs` |
| **NormalizeCellValue ViewModel 연결** | `NormalizePartListAsync` | ✅ 구현됨 | `MainViewModel.cs` |
| **FilterByFeeder ViewModel 연결** | `ApplyFeederFilterAsync` | ✅ 구현됨 | `MainViewModel.cs` |
| **원본 복원** | `ResetToRaw` | ✅ 구현됨 | `MainViewModel.cs` |
| **설정 경로 영속성 (재시작 유지)** | `SettingsService` | ✅ 구현됨 | `SettingsService.cs` |
| **ProgressBar 실제 연동** | `IProgress<double>` | ✅ 구현됨 | `MainViewModel.cs` |

---

## Sprint 0 (완료) ✅ — 분석 문서

- [x] VBA 전체 파일 읽기 및 워크플로우 분석
- [x] `VBA_Review.md` 작성
- [x] `Migration_Plan.md` 초안 작성

---

## Sprint 1 (완료) ✅ — BOM 가공 + PDF

**목표**: BOM 파일 로드 시 자동으로 7컬럼 필터링 + 모델명 타이틀 추출 + 전용 PDF 출력

- [x] `BomReportService.ProcessBomForExport()` 구현
- [x] `BomDataResult.Title` 필드 추가  
- [x] `PdfExportService.ExportBomToPdf()` 구현 (열너비 비율 적용)
- [x] `PdfExportService.ExportWithColumnRatios()` 공통 엔진 구현
- [x] `MainViewModel.LoadBomDataAsync()` 연결 + 폴백 로직

**검증 방법**: BOM xlsx 파일 열기 → 7컬럼만 표시 → PDF 저장 시 파일명에 모델명 반영

---

## Sprint 2 (완료) ✅ — DailyPlan 메타 파싱

**목표**: 셀에서 직접 날짜/라인 읽기 (파일명 의존도 제거)

- [x] `DailyPlanMetadata` DTO 구현
- [x] `DailyPlanService.ReadMetaFromFile()` 구현
- [x] `MainViewModel.OpenDailyPlanFileAsync()` 연결 (DpInfoText에 날짜 표시)

**검증 방법**: DailyPlan xlsx 열기 → InfoText에 "5월-28일 | LOT 3개 | C11" 형식 확인

---

## Sprint 3 (완료) ✅ — PartList 자재 정규화 + Feeder 필터

- [x] `PartListService.NormalizeCellValue()` 구현 (Burner 매핑 포함)
- [x] `PartListService.FilterByFeeder()` 구현
- [x] ViewModel 연결 완료 (Sprint 4에서 처리)

---

## Sprint 4 (완료) ✅ — PartList View 연결

- [x] `NormalizePartListAsync` 명령 추가 (행/열 전체 정규화)
- [x] `ApplyFeederFilterAsync` 명령 추가 (Feeder 컬럼 필터)
- [x] `ResetToRaw` 명령 추가 (원본 복원, `_rawPlData` 보존)
- [x] XAML PartList 탭 버튼 3개 추가 (🔧 정규화 / 🔩 Feeder 필터 / ↩ 원본)

---

## Sprint 5 (완료) ✅ — 설정 경로 영속성

- [x] `Services/SettingsService.cs` 신규 작성 (`AppSettings` record)
- [x] `%AppData%/LARS/settings.json` 읽기/쓰기
- [x] `App.xaml.cs` DI 등록 + OnStartup 자동 복원 + OnExit 저장

---

## Sprint 6 (완료) ✅ — ProgressBar 연동

**목표**: 파일 스캔 중 진행률 표시

**작업 목록**:
- [x] 1. `IProgress<double>` 인터페이스 패턴으로 서비스에 주입 (`BomReportService`, `DailyPlanService`, `PartListService`)
- [x] 2. `ScanBomFilesAsync`, `ScanDailyPlanFilesAsync`, `ScanPartListFilesAsync`에 진행률 콜백 추가
- [x] 3. XAML ProgressBar에 `Value="{Binding Progress}"` 연결 (자동 갱신)

---

## Sprint 7 (완료) ✅ — ItemCounter 날짜별 집계

**목표**: DailyPlan 스케줄 기반 날짜별 자재 수량 집계

**작업 목록**:
- [x] 1. `ItemCounterService.RunPipelineWithDates()` 구현
   - `(DateTime, LotCount)` 쌍 리스트 입력
   - 날짜별 `itemUnit.Count(date)` 집계
- [x] 2. `ItemCounterDataTable` 동적 컬럼 (날짜 헤더) 생성
- [x] 3. DailyPlan + PartList 동시 로드된 경우 자동 연동 기능 추가

---

## Sprint 8 (완료) ✅ — BD_MultiDocuments (핵심 자동화)

**목표**: DailyPlan ↔ PartList 날짜+라인 키로 파일 자동 매핑

> VBA `BD_MultiDocuments.bas` + `FillListView_Intersection()` 이관

**작업 목록**:
- [x] 1. `Services/MultiDocService.cs` 신규 작성
   - 키: `yyyy-MM-dd|C##` (날짜 + 라인번호)
   - DailyPlan 파일 목록 → 키 생성
   - PartList 파일 목록 → 교차 매핑
- [x] 2. MainWindow에 `MultiDocuments 탭` 신규 추가
- [x] 3. ListView: 날짜, 라인, DailyPlan 경로, PartList 경로 표시
- [x] 4. 체크박스 선택 → 일괄 처리 (스캔 → 정규화 → 피더필터 → PDF)

---

## Sprint 9 (완료) ✅ — StickerLabel 인쇄

**목표**: VBA StickerLabel.cls 이관

**작업 목록**:
1. ✅ `Models/StickerLabelInfo.cs` — 스티커 라벨 데이터 모델 + 설정 record
2. ✅ `Services/StickerLabelService.cs` — PdfSharpCore 기반 A4 그리드 라벨 렌더링
3. ✅ `Views/StickerLabelDialog.xaml` + `.cs` — 별도 Dialog (크기/열 설정 + PDF 저장)
4. ✅ `MainWindow.xaml` — `🏷️ StickerLabel` 탭 추가 (미리보기 DataGrid + 설정 패널)
5. ✅ `MainViewModel.cs` — `RefreshStickerLabelsCommand` / `OpenStickerLabelDialogCommand` 추가
6. ✅ `App.xaml.cs` — `StickerLabelService` DI 등록

**추가 수정 (기존 버그)**:
- ✅ `ReportServices.cs` — `IXLCell.MergeArea` → `IsMerged() + MergedRange()` API 수정
- ✅ `MainViewModel.cs` — `FileMetadata.FilePath` → `FullPath` 수정
- ✅ `PdfExportService.cs` — `ExportWithColumnRatios` `private` → `public` 수정

---

## 아키텍처 원칙 (전체 공통)

- 서비스는 **순수 C#**, VBA/COM 의존성 없음
- Excel 읽기: **ClosedXML** (파일 변경 없음, 읽기 전용)
- PDF 출력: **PdfSharpCore**
- UI: **WPF + CommunityToolkit.Mvvm**
- 모든 I/O: **async/await + Task.Run**
- DI: **Microsoft.Extensions.DependencyInjection**
