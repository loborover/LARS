# 레거시 VBA 비즈니스 로직 리뷰

> 마지막 갱신: 2026-04-04
> 이 문서는 VBA 원본에서 서버 이관 시 보존해야 할 비즈니스 로직을 요약합니다.

## 1. 핵심 이관 대상

- `BA_BOM_Viewer.bas`
- `BB_DailyPlan_Viewer.bas`
- `BC_PartListItem_Viewer.bas`
- `BD_MultiDocuments.bas`
- `CA_itemCounter.bas`
- `BCA_PLIV_Feeder.bas`
- `TimeKeeper.bas`
- `Utillity.bas`
- `Printer.bas`
- `itemUnit.cls`
- `itemGroup.cls`
- `ModelInfo.cls`
- `ProductModel2.cls`
- `D_LOT.cls`
- `D_Maps.cls`
- `FeederUnit.cls`

## 2. 서버 이관 원칙

- `.frm`는 UI 참고자료일 뿐 핵심 이관 대상이 아닙니다.
- Excel Shape 기반 출력은 웹 UI와 PDF로 대체합니다.
- VBA의 큰 함수는 서버에서 작은 서비스/함수로 재분해합니다.

## 3. 반드시 보존해야 하는 업무 규칙

- 파일 판별 규칙
- 날짜/라인 추출 규칙
- PartList 정규화 규칙
- ItemCounter 분해/병합 규칙
- MultiDocument 매칭 규칙
- PDF 출력 결과 규칙

## 4. 다음 작업

- 각 VBA 모듈의 입력/출력 계약 정리
- Golden Sample 정의
- 서버 도메인별 테스트 시나리오 작성
