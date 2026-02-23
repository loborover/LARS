# LARS VBA 코드베이스 워크플로우 리뷰

> 작성일: 2026-02-24  
> 분석 대상: `/VBA/Modules/` (18개), `/VBA/Classes/` (11개), `/VBA/Forms/` (5개 .frm)

---

## 1. 전체 시스템 아키텍처

```
┌─────────────────────────────────────────────────────────────────┐
│                  AutoReportHandler (Form: 메인 UI)               │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  [BOM탭]  [DailyPlan탭]  [PartList탭]  [Feeder탭]       │   │
│  └──────────────────────────────────────────────────────────┘   │
└────────────┬────────────┬────────────┬──────────┬───────────────┘
             │            │            │          │
      BA_BOM_Viewer  BB_DP_Viewer  BC_PL_Viewer  BCA_PLIV_Feeder
             │            │            │          │
             └────────────┴─────┬───────┘          │
                               │                    │
                         CA_itemCounter         Feeder.json
                               │
                    ┌──────────┴──────────┐
                  itemUnit.cls        itemGroup.cls
```

### 핵심 의존성 흐름
```
Z_Directory (경로 관리)
    └──> 모든 Viewer (파일 탐색)
         └──> ExcelReaderService (파일 읽기)
              └──> 각 Viewer (데이터 가공)
                   └──> Printer (PDF/인쇄)
```

---

## 2. 모듈별 역할 분석

### 2.1 Modules (비즈니스 로직)

| 파일 | 역할 | C# 마이그레이션 상태 |
|------|------|---------------------|
| `AA_Updater.bas` | VBA 코드 업데이트 / Git 동기화 유틸 | ❌ 불필요 (C# 빌드로 대체) |
| `AB_C_PL2DP.bas` | PartList → DailyPlan 변환 브릿지 | 🔶 부분 필요 |
| `BA_BOM_Viewer.bas` | BOM 파일 읽기, 컬럼 필터, PDF 출력 | ✅ 기본 구현됨 |
| `BB_DailyPlan_Viewer.bas` | DailyPlan 읽기, 날짜/모델 그루핑, PDF | ✅ 기본 구현됨 |
| `BCA_PLIV_Feeder.bas` | Feeder 관리 (등록/삭제/정렬) | ✅ 기본 구현됨 |
| `BCB_PLIV_Focus.bas` | PartList 열 포커스 헬퍼 | ❌ 불필요 (Excel UI 전용) |
| `BC_PartListItem_Viewer.bas` | PartList 읽기, 자재 파싱, 병합, PDF | ✅ 기본 구현됨 |
| `BD_MultiDocuments.bas` | DailyPlan + PartList 교차 파일 처리 | ⬜ 미구현 |
| `CA_itemCounter.bas` | PartList → itemUnit 파싱 → 병합 → 집계 | ✅ 기본 구현됨 |
| `Cleaner.bas` | 임시 파일/시트 정리 | ❌ 불필요 |
| `Fillter.bas` | 컬럼 자동 필터링 유틸 | 🔶 내부 유틸리티 |
| `Git_Kit.bas` | Git 커밋 자동화 | ❌ 불필요 |
| `LinkToDB.bas` | DB 연결 (미완성 stub) | ❌ 불필요 |
| `Printer.bas` | PageSetup 정의 + 인쇄/PDF 출력 | ✅ 기본 구현됨 |
| `TimeKeeper.bas` | 작업 시간 추적 및 로그 | ⬜ 미구현 (선택사항) |
| `Utillity.bas` | 공통 헬퍼 함수 모음 (파일탐색, 문자열, ListView 등) | ✅ 기본 구현됨 |
| `Z_Directory.bas` | 폴더 구조 정의 (BOM/DailyPlan/PartList/Feeder/Output) | ✅ 구현됨 |
| `AA_Test.bas` | 테스트 코드 | ❌ 불필요 |

### 2.2 Classes (데이터 모델)

| 파일 | 역할 | C# 마이그레이션 상태 |
|------|------|---------------------|
| `itemUnit.cls` | 단일 자재: NickName, Vendor, PartNumber, QTY, 날짜별 Count | ✅ `ItemUnit.cs` 구현됨 |
| `itemGroup.cls` | itemUnit 집합: ID_Hash 기반 병합, 집계 | ✅ `ItemGroup.cs` 구현됨 |
| `ModelInfo.cls` | 모델명 파싱: Type/Spec/Color/Suffix 분해 | ✅ `ModelInfo.cs` 구현됨 |
| `D_LOT.cls` | LOT 단위 (시작행/끝행, StartRow/EndRow) | ✅ `Lot.cs` 구현됨 |
| `D_Maps.cls` | LOT 그룹 관리 (SubGroup/MainGroup) | ✅ `LotGroup.cs` 구현됨 (부분) |
| `FeederUnit.cls` | Feeder 단위 (Name, itemBox) | ✅ `FeederUnit.cs` 구현됨 |
| `ProductModel2.cls` | 제품 모델 정보 확장 | 🔶 `ProductModel.cs` 부분 |
| `InventoryCart.cls` | 재고 카트 (미사용/미완성 stub) | ❌ 불필요 |
| `ObjPivotAxis.cls` | Pivot 좌표 관리 (Excel 전용) | ❌ 불필요 |
| `Painter.cls` | 셀 스타일 + 도형 그리기 (Excel UI 전용) | ❌ 불필요 |
| `StickerLabel.cls` | 스티커 라벨 생성 (Excel 전용) | ❌ 불필요 (장기 과제) |

### 2.3 Forms

| 파일 | 역할 | 참고 |
|------|------|------|
| `AutoReportHandler.frm` | 메인 UI 폼 (ListView 기반) | WPF MainWindow로 대체됨 |
| `BCCUF.frm` | PartList column user form | 기능 파악 필요 |
| `Cleaner_Handler.frm` | 임시파일 정리 UI | 불필요 |
| `Git_Con.frm` | Git 연동 UI | 불필요 |
| `Tool_PL2DP.frm` | PartList→DailyPlan 변환 도구 UI | BD 탭 구현 시 필요 |

---

## 3. 핵심 워크플로우 상세 분석

### 3.1 BOM Viewer 워크플로우 (BA_BOM_Viewer.bas)

```
[스캔 버튼 클릭]
  1. Z_Directory.Source 경로에서 "@CVZ" 텍스트를 포함한 .xlsx 파일 탐색
  2. 각 파일을 별도 Excel.Application으로 열어 Cells(2,3)에서 모델명 추출
     → Title = ws.Cells(2,3).Value ("LSGL6335F.A@CVZ..." → "@" 앞까지 추출)
  3. ListView에 [모델명, 파일경로, PrintStatus, PDFStatus] 표시
  4. 체크박스 일괄 선택

[Print/PDF 버튼 클릭]
  1. 체크된 항목만 처리
  2. AutoReport_BOM 실행:
     a. SetUsingColumns → ["Lvl", "Part No", "Description", "Qty", "UOM", "Maker", "Supply Type"]
     b. FncSetPR → 위 컬럼 범위의 데이터 영역 계산
     c. 타이틀 행 병합 + AutoTitle 호출 (Lvl=0인 행에서 Part No값 읽어 타이틀 생성)
     d. AutoFilltering_BOM → 컬럼 헤더 위 나머지 열 삭제/숨김
     e. Interior_Set_BOM → 테두리, 열 너비 설정 [2.7, 20, 30, 3, 2.5, 16, 13]
  3. PageSetup (A4 세로, FitToPagesWide=1, 우측 헤더에 날짜/시간/페이지)
  4. 인쇄 또는 PDF 저장 (SaveFilesWithCustomDirectory)
```

**⚠️ VBA 특이 사항:**
- 각 BOM 파일을 열 때 **새 Excel.Application 인스턴스**를 생성 (숨김 모드) → 느리지만 안전
- 파일명에서 모델명을 읽는 게 아니라 **셀 내용**에서 파싱
- `Title` 전역 변수를 여러 Private Sub가 공유 (사이드 이펙트 위험)

---

### 3.2 DailyPlan Viewer 워크플로우 (BB_DailyPlan_Viewer.bas)

```
[스캔 버튼 클릭]
  1. Z_Directory.DailyPlan 경로에서 "DailyPlan" 파일 탐색
  2. GetDailyPlanWhen: 각 파일에서 날짜 추출
     → Row 2에서 "월"로 끝나는 셀(병합셀)을 찾아 가장 작은 날짜값 추출
     → Title = "X월-Y일", wLine (생산 라인)
  3. ListView에 [날짜, 라인, 경로, PDFStatus] 표시

[Report 버튼 클릭]
  1. AutoReport_DailyPlan 실행:
     a. SetUsingColumns_DP → 표시할 컬럼 정의
     b. FncSetPR_DP → 데이터 범위 계산
     c. AR_2_ModelGrouping → 모델 그루핑:
        - "품목번호" 컬럼 기준으로 모델 변경 지점 감지
        - 모델 변경마다 LOT 경계 설정 (D_Maps에 SubLot 추가)
        - Painter.Stamp_it_Auto → 그룹 경계에 선 그리기 (Excel 도형)
     d. 날짜 컬럼 포맷: DecodeDate → "d.aaa" 형식 (주말은 배경色 구분)
     e. DatePartLining → 주 경계마다 이중선 표시
     f. MakeBlock → 같은 모델 행들에 테두리 블록
  2. PageSetup → A4 세로, 좌우여백 0
  3. PDF 저장
```

**⚠️ VBA 특이 사항:**
- `Painter.cls`가 Excel 도형(Shape)으로 모델 그루핑 경계선을 그림 → C#에서는 PDF에 직접 선 그리기로 대체 필요
- 날짜 디코딩: 숫자로 된 일자를 실제 날짜로 변환하고 요일에 따라 배경색 지정

---

### 3.3 PartList Viewer 워크플로우 (BC_PartListItem_Viewer.bas)

```
[스캔 버튼 클릭]
  1. Z_Directory.PartList 경로에서 "PartList" 파일 탐색 (MMYYYY 패턴)

[Report 버튼 클릭]
  1. AutoReport_PL 실행:
     a. AR_2_ModelGrouping → 모델별 그루핑 (DP와 동일 로직)
     b. Re_Categorizing_PL → 자재 셀 값 정규화:
        - "[벤더명] 파트번호1/파트번호2(수량)" 형식으로 표준화
        - 5글자 이상 영문만은 "자사품"으로 치환
        - Burner 컬럼은 특수 매핑 적용
     c. PartCombine → 중복 벤더 컬럼 병합 (여러 컬럼의 같은 벤더를 1컬럼으로)
     d. MarkingUP_PL → 모델 경계에 상단 테두리 + Painter 스탬프
     e. SortColumnByFeeder → Feeder 설정에 따라 컬럼 표시/숨김
  2. PageSetup → A4 가로, 여백 모두 0
  3. PDF/인쇄 출력
```

**⚠️ VBA 특이 사항:**
- `Re_Categorizing_PL`은 셀 값을 **직접 수정**하는 Destructive 방식
- Burner 특수 케이스 하드코딩 (도메인 지식 주의)
- `PartCombine`은 중복 컬럼 자체를 삭제하는 파괴적 조작

---

### 3.4 ItemCounter 워크플로우 (CA_itemCounter.bas)

```
[집계 실행]
  1. PartList 데이터 로드 (이미 로드된 tWS 참조)
  2. Re_Categorizing:
     - 셀 텍스트 파싱: " [" → "$["로 치환 후 "$" 기준 Split (벤더 구분)
     - "[벤더]" 추출 → ExtractBracketValue
     - 나머지 "/" 기준 Split (파트번호 구분)
     - "(수량)" 추출 → ExtractSmallBracketValue
     - itemUnit 생성: NickName, Vendor, PartNumber, QTY, 날짜별Count
  3. PL_Compressor (병합):
     - ID_Hash (NickName+Vendor+PartNumber 해시) 기준으로 중복 제거
     - 날짜별 Count 누적 (MergeCountsFrom)
  4. Writing_itemCounter_from_PL:
     - 헤더: No | NickName | Vendor | PartNumber | [날짜들...] | Total
     - "test" 시트에 결과 기록
```

---

## 4. 공통 유틸리티 패턴 (Utillity.bas)

| VBA 함수 | 역할 | C# 구현 위치 |
|----------|------|-------------|
| `FindFilesWithTextInName()` | 재귀 파일 탐색 | `Utils/FileSearcher.cs` |
| `ExtractBracketValue()` | `[값]` 추출 | `Utils/StringParser.cs` |
| `ExtractSmallBracketValue()` | `(값)` 추출 | `Utils/StringParser.cs` |
| `RemoveLineBreaks()` | 줄바꿈 제거 | `Utils/StringParser.cs` |
| `BuildKeyFromPath()` | 파일명 → `yyyy-MM-dd\|C##` 키 생성 | `Models/Common.cs` (Parse) |
| `FillListView_Intersection()` | DailyPlan + PartList 교차 매핑 | ⬜ 미구현 |
| `LenA()` | 한글/영문 혼합 문자열 픽셀 계산 | ❌ 불필요 (WPF에서 자동) |

---

## 5. 미구현/미이관 기능 목록

### 🔴 높은 우선순위
1. **BD_MultiDocuments**: DailyPlan ↔ PartList 파일 교차 매핑 (날짜+라인 키로 매칭) → LARS의 핵심 자동화 기능
2. **Feeder 영속성**: `B_Read_Feeder` / `B_Send_Feeder` → Feeder 설정을 JSON/파일로 저장/불러오기 (VBA에서도 stub만 있었음, C#에서 먼저 완성 필요)

### 🟡 중간 우선순위
3. **PartList 자재 셀 정규화** (`Re_Categorizing_PL`): 현재 C#은 단순 읽기만 함, 셀 데이터를 표준 형식으로 재가공하는 로직 부재
4. **DailyPlan 모델 그루핑 시각화**: PDF에 LOT 경계선 그리기 (PdfSharpCore로 직접 그려야 함)
5. **설정값 영속성**: BasePath 앱 재시작 후 유지

### 🟢 낮은 우선순위
6. **StickerLabel.cls**: 스티커 인쇄 기능 (별도 탭 필요)
7. **TimeKeeper.bas**: 작업 시간 로깅
8. **Tool_PL2DP**: PartList → DailyPlan 변환 도구

---

## 6. 기술 부채 및 주의사항

| 항목 | VBA 원본 | C# 개선 방향 |
|------|----------|-------------|
| **전역 `Title` 변수** | 모듈 전반에서 공유, 사이드 이펙트 위험 | 로컬 변수로 교체 ✅ |
| **새 Excel.Application 생성** | 파일별로 인스턴스 생성 (느림) | ClosedXML으로 직접 읽기 ✅ |
| **파괴적 셀 조작** | 원본 파일 직접 수정 | 별도 처리 워크시트에 복사 후 작업 필요 |
| **MSCOMCTL.OCX 의존성** | ListView/ComboBox Win32 컨트롤 | WPF DataGrid/ListBox로 대체 ✅ |
| **Painter.cls 도형** | Excel Shape 객체로 그루핑 시각화 | PDF에 직접 선/박스 그리기 |
| **On Error Resume Next** | 오류 발생 시 무시 패턴 | try-catch + 명확한 에러 메시지 ✅ |
