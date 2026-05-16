# Phase 17 Coder Report — BOM Substitute Fix + BOM Amount View 완료

## 1. 개요
이번 페이즈에서는 BOM 트리의 `level=-1` 대체품(Substitute) 렌더링 버그를 수정하여 올바른 계층 구조를 보여주도록 개선하였습니다. 또한, BOM 계층 구조를 전개하여 부품별 실소요량을 계산하는 `Amount View` 기능을 새로 구현하고, 이를 프론트엔드 UI에 통합하였습니다.

## 2. 작업 상세

### 2.1 대체품(Substitute) 렌더링 수정 (Task 17-A)
- `buildTree` 알고리즘 전면 교체: `level=-1` 인 노드(대체품)를 만나면 스택 조작을 생략하고, `path` 문자열 처리(`.S` 제거)를 통해 곧바로 원본(Primary) 노드의 `substitutes` 배열에 할당하도록 변경하였습니다.
- `TreeNode` 및 `TreeRow` 수정: 원본 부품과 대체품이 올바르게 묶이도록 계층 처리 및 UI(노란색 배경, "대체" 뱃지, 들여쓰기 1레벨 추가)를 적용했습니다.

### 2.2 BOM Amount 산출 로직 구축 (Task 17-B)
- `BomAmountItem` 및 `BomAmountResponse` 스키마 추가 (backend/schemas/bom.py).
- `get_bom_amount` 로직 구현: `bom_service.py` 에 추가하여 BOM의 전체 계층 구조를 탐색하며, `accumulated_qty = item.qty * parent.qty * grandparent.qty * ...` 공식에 따라 누적 소요량을 산출합니다. (대체품 및 최상위 루트 노드는 산출에서 제외 처리)
- 신규 API 라우트 등록: `GET /api/v1/bom/amount/{model_number:path}`.

### 2.3 프론트엔드 Amount View 전환 기능 연동 (Task 17-C)
- `BOMAmountView.tsx` 신규 컴포넌트 추가: 계산된 소요량 리스트를 `part_number` 기준으로 플랫하게 보여주고, 필터링 및 `Grand Total`을 지원합니다.
- `BOMDetailPage.tsx` 수정: 상단 토글 버튼을 도입하여 "Tree View"와 "Amount View" 모드를 자유롭게 전환할 수 있습니다. 각 모드에 맞춰 API 호출이 분기되도록 `useQuery` 설정을 조정하였습니다.

## 3. 검증 결과
- **백엔드**: `/api/v1/bom/amount/CBGJ3023D.ABDELNA` 등의 조회 호출 시 정상적으로 중복 품번 합산과 `occurrence_count` 가 처리되는 것을 콘솔 테스트로 확인 완료.
- **프론트엔드**: TypeScript 에러 `0건`, Vite 최적화 모드 빌드 성공 및 런타임 오류 없음.
- **UI/UX**: 
  - 트리 뷰에서 더이상 대체품(S) 노드가 하위 노드를 가로채지 않고 본 부품 직하단에 렌더링됨.
  - Amount View 탭 전환 시 새로운 테이블 UI에 정확히 합산된 수량이 렌더링되고 Grand Total이 출력됨.

## 4. 특이사항
- 모델 식별자인 `model_code`와 `suffix` 의 조합(`model_number`) 처리를 `bom_service` 조회 단에서 한 번 더 꼼꼼하게 검증 및 예외 처리하도록 구성하였습니다.
- 별도의 DB 스키마 수정이 없어 `Alembic` 마이그레이션 절차는 생략하였습니다.
