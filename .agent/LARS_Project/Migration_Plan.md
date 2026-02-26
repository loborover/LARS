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

## Sprint 10 (완료) ✅ — PartList 핵심 가공 파이프라인 (P0)

**목표**: VBA `AR_1_EssentialDataExtraction`(PartList)의 전체 가공 파이프라인 검증 및 구현

> 📌 VBA에서 가장 복잡한 단일 함수. 호출 순서와 부작용(열/행 삭제)을 정확히 재현해야 함.
> 📌 참조: [VBA_Review.md §2.3](file:///d:/Workshop/LARS/.agent/Work/VBA_Review.md)

**작업 목록**:
- [x] 1. **투입시점 병합** — `MergeDateTimeColumns()` 구현
- [x] 2. **D-Day N일 트리밍** — `TrimByDayCount()` 구현
- [x] 3. **불필요 열 삭제** — `FilterEssentialColumns()` 구현
- [x] 4. **모델+Suffix 병합** — `MergeModelSuffix()` 구현
- [x] 5. **`PartCombine`** — `CombineDuplicateParts()` 구현
- [x] 6. **`DeleteDuplicateRowsInColumn`** — `RemoveDuplicateWorkOrders()` 구현
- [x] 7. **`Replacing_Parts`** — `NormalizeVendorName()` + `NormalizeAllPartColumns()` 구현
- [x] Burner 매핑 VBA 원본 기준 `[기미]`/`[피킹]`으로 수정

**검증 결과**: `dotnet build` — 0 errors / 0 warnings ✅

---

## Sprint 11 — ItemCounter 파싱 정밀 검증 (P0)

**목표**: VBA `Re_Categorizing` + `PL_Compressor` 로직의 C# 구현 정밀 검증

> 📌 참조: [VBA_Review.md §2.4](file:///d:/Workshop/LARS/.agent/Work/VBA_Review.md)

**작업 목록**:
- [ ] 1. **`Re_Categorizing` 파싱 검증** — 셀 문자열 → itemUnit 분해
   - `" [" → "$["` 치환 → `"$"` 기준 Split → Vendor별 분리
   - `ExtractBracketValue()` → 벤더명 추출
   - `"/"` 기준 파트넘버 분리, `"()"` 안의 값 → QTY
   - `Count(InputDate) = LotCounts × QTY`
- [ ] 2. **`PL_Compressor` 병합 검증** — ID_Hash(`Vendor_NickName_PartNumber`) 기준 병합
   - 동일 Hash → `MergeCountsFrom()` (날짜별 Count 합산)
- [ ] 3. **단위 테스트 작성** — 실제 PartList 셀 데이터 5개 이상 샘플로 테스트
   - 입력 예: `[기미] 4102/4202(2)/4502 [SABAF S.P.A.] 6904/7302`
   - 기대 출력: 5개 itemUnit (QTY 1,2,1,1,1)

**검증 방법**: 단위 테스트 전량 통과 + VBA 결과물과 행 수·합계값 일치.

---

## Sprint 12 (완료) ✅ — DailyPlan 가공 및 ModelGrouping (P1)

**목표**: VBA `AR_1(DailyPlan)` + `AR_2_ModelGrouping` 3단계 폴백 구현 검증

> 📌 참조: [VBA_Review.md §2.2](file:///d:/Workshop/LARS/.agent/Work/VBA_Review.md)

**작업 목록**:
- [x] 1. **DailyPlan `AR_1`** — `ProcessDailyPlanForExport()` 구현 (열 필터링 + 모델Suffix 병합)
- [x] 2. **`AR_2_ModelGrouping` 3단계 폴백** — `IsSameGroup()` 구현
   - 1차: `SpecNumber` 비교 → 2차: `TySpec` (Species≠"LS63") → 3차: `Species`
- [x] 3. **모델 열 자동 탐지** — 가공 후 열 인덱스 변동에도 정확한 그루핑
- [x] `DailyPlanDataResult` DTO 확장 (`IsProcessed`, `Rows` set 가능)

**검증 결과**: `dotnet build` — 0 errors / 0 warnings ✅

**검증 방법**: DailyPlan xlsx로 그루핑 결과 확인 — VBA 출력물과 그룹 경계 행번호 비교.

---

## Sprint 13 — 파일 스캔 데이터 유효성 + BOM Level 필터 (P2~P3)

**목표**: 파일 스캔 시 실제 데이터 포함 여부 검증 + BOM Level 필터 정밀화

> 📌 VBA는 파일을 열어서 헤더/데이터 존재 여부를 확인 후 스킵 처리
> 📌 참조: [VBA_Review.md §5.1](file:///d:/Workshop/LARS/.agent/Work/VBA_Review.md)

**작업 목록**:
- [ ] 1. **BOM 파일 유효성 검증** — `ws.Cells(2,3).Value`에 모델명 존재 확인
- [ ] 2. **DailyPlan 유효성** — Row 2 `*월` 패턴 + Row 3 수치>0 확인 → 아니면 스킵
- [ ] 3. **PartList 유효성** — Row 1 `YYYYMMDD` 헤더 확인 → 아니면 스킵
- [ ] 4. **BOM `FilterByLevel` 정밀화** — Level 문자열 매칭
   - `0`, `.1`, `..2`, `...3`, `*S*`, `*Q*` 등 계층별 필터
   - UI 체크박스와 연동 (VBA: `CB_Lvl1_BOM`, `CB_LvlAll_BOM` 등)

**검증 방법**: 비정상 파일(빈 파일, 다른 형식) 투입 시 스킵 처리 확인.

---

## Sprint 14 — C# 코드 대조 검증 및 문서 갱신 (메타)

**목표**: VBA↔C# 전체 로직 1:1 대조 + 문서 갱신

**작업 목록**:
- [ ] 1. `ReportServices.cs` 전체 메서드 vs VBA 함수 1:1 대조표 작성
- [ ] 2. 누락/불일치 항목 목록화
- [ ] 3. `Csharp_Review.md` 업데이트 — 검증 결과 반영
- [ ] 4. `Migration_Plan.md` 최종 갱신 — Sprint별 완료 상태 확정
- [ ] 5. `dotnet build` 0 errors / 0 warnings 확인

**검증 방법**: `Csharp_Review.md`에 모든 VBA 함수 대응 상태(✅/⚠️/❌) 기록 완료.

---

## VBA→C# 갭 요약 (Sprint 10~14 대응)

> `VBA_Review.md` §4.1 이관 우선순위에서 도출

| 우선순위 | VBA 함수 | Sprint | C# 현 상태 |
|---------|---------|--------|-----------|
| **P0** | `AR_1_EssentialDataExtraction` (PartList) | **10** | ❓ 미검증 |
| **P0** | `Re_Categorizing` / `PL_Compressor` | **11** | ⚠️ 부분 |
| **P1** | `AR_1` (DailyPlan) + `AR_2_ModelGrouping` | **12** | ⚠️ 부분 |
| **P1** | `GetDailyPlanWhen` / `GetPartListWhen` | **12, 13** | ⚠️ 부분 |
| **P2** | `PartCombine` + `Replacing_Parts` | **10** | ❓ 미검증 |
| **P3** | `FilterByLevel` (BOM) | **13** | ⚠️ 부분 |
| **P3** | `MergeDateTime_Flexible` | **10** | ⚠️ 부분 |

---

## 아키텍처 원칙 (전체 공통)

- 서비스는 **순수 C#**, VBA/COM 의존성 없음
- Excel 읽기: **ClosedXML** (파일 변경 없음, 읽기 전용)
- PDF 출력: **PdfSharpCore**
- UI: **WPF + CommunityToolkit.Mvvm**
- 모든 I/O: **async/await + Task.Run**
- DI: **Microsoft.Extensions.DependencyInjection**

