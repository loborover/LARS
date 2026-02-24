# 이슈 트래커

## 열린 이슈 (Open)

### ISS-001: RAW 파일 스캔 경로 지정 시 스캔 안 됨 🔴
- **등록일**: 2026-02-24
- **증상**: Source 경로를 지정해도 파일이 스캔되지 않는 현상
- **영향 범위**: BOM / DailyPlan / PartList 전체 스캔 기능
- **조사 대상**:
  - [ ] `DirectoryManager` 경로 설정 및 폴더 생성 로직
  - [ ] `SettingsService` 경로 저장/복원 정상 여부
  - [ ] `FileSearcher.FindFiles()` 검색 필터 조건
  - [ ] `ScanBomFiles` / `ScanDailyPlanFiles` / `ScanPartListFiles` 호출 흐름
  - [ ] UI에서 경로 변경 시 서비스에 제때 반영되는지
  - [ ] 파일 확장자 필터 (`.xlsx`) / 임시파일 (`~$`) 제외 조건
- **우선순위**: P1 (스캔이 안 되면 이후 가공/출력 불가)
- **상태**: 🟡 조사 예정

---

## 닫힌 이슈 (Closed)

(없음)
