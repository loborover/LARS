# UI 구조 개편 및 기능 추가 (UI Restructuring & Feature Addition)

## 개요 (Overview)
사용자의 요청에 따라 프로젝트의 기본 언어를 한국어로 설정하고, 주요 데이터 뷰어(BOM, PartList, DailyPlan)를 통합 관리할 수 있는 상위 탭 구조를 도입했습니다. 또한, 각 뷰어에 일관된 파일 가져오기 및 가공 기능을 추가하고, 전역 설정을 위한 메뉴를 신설했습니다.

## 변경 사항 (Changes)

### 1. 상위 탭 구조 도입 (Top-level Tab Structure)
- **`DataViewerControl.cs` 신규 생성**: `TabControl`을 사용하여 BOM, PartList, DailyPlan 뷰어를 하나의 화면에서 탭으로 전환하며 볼 수 있도록 통합했습니다.
- **`MainForm.cs` 수정**: 사이드바 메뉴를 간소화하여 개별 뷰어 버튼을 제거하고, `데이터 뷰어 (Viewers)` 버튼 하나로 통합했습니다. 이를 통해 화면 공간을 효율적으로 사용하고 연관 기능을 그룹화했습니다.

### 2. 표준화된 UI 및 기능 추가 (Standardized UI & Features)
BOM, PartList, DailyPlan 뷰어 모두에 동일한 양식의 버튼과 기능을 적용했습니다.

- **파일 가져오기 (Import Raw)**:
  - 사용자가 수동으로 Raw 파일(.xlsx)을 선택하여 시스템(지정된 폴더)으로 가져오는 기능을 구현했습니다.
  - 파일 선택 시 자동으로 해당 모듈의 작업 폴더(BOM, PartList, DailyPlan)로 복사되고 목록이 갱신됩니다.
- **가공 실행 (Process Target)**:
  - 선택된 파일을 기반으로 가공 로직을 실행하는 버튼을 배치했습니다.
  - BOM: 단일 파일 로드 후 가공 (기존 로직 유지)
  - DailyPlan: 목록에서 선택된 파일 가공 (기존 로직 유지)
  - PartList: 목록에서 선택된 파일 가공 (UI 구현 완료, 로직 연동 준비)

### 3. 설정 메뉴 추가 (Settings Tab)
- **`SettingsControl.cs` 신규 생성**: 애플리케이션 전역 설정을 관리할 수 있는 화면을 추가했습니다.
- **기능**:
  - 기본 언어 설정 (현재 한국어 기본)
  - 데이터 소스 경로 설정

### 4. 한국어 적용 (Korean Language Support)
- 모든 버튼 텍스트, 메시지 박스 알림, 폼 타이틀 등을 한국어로 작성하여 사용자가 직관적으로 이해할 수 있도록 했습니다.

## 검증 (Verification)

### 수동 검증 단계
1. **메인 화면 진입**: 애플리케이션 실행 시 초기 화면이 '데이터 뷰어' 탭으로 표시되는지 확인.
2. **탭 전환**: 상단의 BOM, PartList, DailyPlan 탭을 클릭하여 각 화면이 정상적으로 로드되는지 확인.
3. **파일 가져오기 테스트**:
   - 각 탭에서 '파일 가져오기' 버튼 클릭.
   - 엑셀 파일 선택 후 "성공" 메시지 확인 및 목록 갱신 확인.
4. **가공 실행 테스트**:
   - 목록에서 항목 선택 후 '가공 실행' 버튼 클릭.
   - 처리 완료 메시지 또는 "구현 중" 메시지 확인.
5. **설정 저장**:
   - 사이드바의 '설정' 버튼 클릭.
   - 경로 변경 등 입력 후 '설정 저장' 버튼 클릭 시 저장 성공 메시지 확인.

### 파일 구조 변경 확인
- `Forms/DataViewerControl.cs` (New)
- `Forms/SettingsControl.cs` (New)
- `Forms/MainForm.cs` (Modified)
- `Forms/BomViewerControl.cs`, `PartListControl.cs`, `DailyPlanControl.cs` (Modified)
