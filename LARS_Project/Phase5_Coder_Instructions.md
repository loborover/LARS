# Phase 5 Coder Instructions — PSI 전면 재설계

> 작성일: 2026-05-16
> 작성자: Chief
> 대상: Coder (Gemini)
> 참조 파일: `/test/AutoReport/Expeditor_Public (version 2).xlsb`
> 기준 문서: `LARS_Project/New_LARS_Project.md`, `LARS_Project/LARS_Consolidated_Report.md`

---

## 배경 및 목적

실제 업무에서 사용하는 `Expeditor_Public.xlsb`의 PSI 시트 구조를 분석한 결과, 현재 LARS PSI와 실제 업무 PSI 사이에 핵심 기능 Gap이 확인되었습니다. 이 지시서는 실제 업무 방식에 맞춰 PSI 기능을 전면 재설계하는 작업입니다.

---

## 분석된 실제 PSI 구조

### PSI 시트 — 1품목당 4행 블록
```
행1: 순번 | 품번       | 품명     | 계획수량(재고) | D-Day | D+1 | ... | D+30
행2:    - | Level      | 1차협력사 | 재고수량       | (week 번호)
행3:    - | TechSpec   | 2차협력사 | 불량수량       | (serial 날짜)
행4:    - | SupplyType | UOM      | -              | [날짜별 소요수량 actual data]
```

### PSI_Base 시트 (피벗 원본)
- 필터: Expeditor(담당자), SupplyType, Level, Line, Model.Suffix
- 컬럼: `Vendor | LowerVendor | Description | PartNumber | [날짜들...]`
- 값: `Real_Qty` (BOM × DP 계산 소요량)

### itemMaster 시트 추가 필드
- `Expeditor`: 담당자 이름
- `Picked`: 담당자가 팔로업 마킹한 품목 여부 (Boolean)
- `LowerVendor`: 2차 협력사

### itemUsed 시트
- 품번이 어떤 최상위 모델(Top Material)에 사용되는지 역추적
- Required Qty, Demand, Parent Material 포함

---

## Task 목록

---

### Task 5-A: DB 스키마 확장 (Alembic 003)

**목표:** PSI 재설계에 필요한 신규 필드를 DB에 추가한다.

#### item_master 테이블 컬럼 추가
```sql
ALTER TABLE item_master ADD COLUMN lower_vendor_raw TEXT;         -- 2차 협력사 텍스트
ALTER TABLE item_master ADD COLUMN inventory_qty NUMERIC(12,4) DEFAULT 0;  -- 재고수량
ALTER TABLE item_master ADD COLUMN defect_qty    NUMERIC(12,4) DEFAULT 0;  -- 불량수량
ALTER TABLE item_master ADD COLUMN is_picked     BOOLEAN NOT NULL DEFAULT FALSE;  -- 담당자 팔로업 마킹
```

**핵심 로직:**
- `tracking_user_id`가 이미 존재하므로 Expeditor 기능은 이 FK를 활용
- `lower_vendor_raw`는 2차 협력사 텍스트 (vendor 테이블 FK 불필요, raw 텍스트로 충분)
- `inventory_qty`, `defect_qty`는 담당자가 직접 입력하는 값 (PSI 인라인 편집)
- `is_picked`는 담당자가 팔로업 마킹하는 boolean

#### Alembic 마이그레이션 파일 생성
- 파일명: `alembic/versions/003_item_master_psi_fields.py`
- `upgrade()`: 위 4개 컬럼 추가
- `downgrade()`: 4개 컬럼 제거

---

### Task 5-B: 백엔드 — PSI API 재설계

**목표:** 실제 업무 PSI 구조에 맞는 API를 구현한다.

#### 5-B-1: schemas/item_master.py 업데이트
기존 `ItemMasterRead`에 신규 필드 추가:
```python
lower_vendor_raw: str | None
inventory_qty: float
defect_qty: float
is_picked: bool
```

#### 5-B-2: schemas/psi.py 재설계

신규 스키마 `PsiRowFull` 정의:
```python
class PsiRowFull(BaseModel):
    # 품목 정보
    item_id: int
    part_number: str
    description: str
    level: int
    supply_type: str | None
    uom: str
    vendor_raw: str | None        # 1차 협력사
    lower_vendor_raw: str | None  # 2차 협력사
    tech_spec: str | None         # Technical Spec (bom_items.description 활용)
    
    # 재고/불량 (담당자 입력값)
    inventory_qty: float
    defect_qty: float
    is_picked: bool
    
    # 날짜별 소요량 (D-Day ~ D+30, 31일)
    # key: "D+0", "D+1", ..., "D+30"
    # value: required_qty (float)
    daily_demand: dict[str, float]
    
    # 날짜 메타 (프론트엔드 헤더 렌더링용)
    date_headers: list[dict]  # [{"label": "D-Day", "date": "2026-05-16", "week": 20}, ...]
    
    expeditor_name: str | None
```

신규 스키마 `PsiFilterParams`:
```python
class PsiFilterParams(BaseModel):
    expeditor_user_id: int | None  # None = 전체
    supply_type: str | None        # "Assembly Pull" | "Supplier" | "Phantom" | None
    level: int | None
    model_code: str | None         # Model.Suffix 필터
    date_from: date                # D-Day 기준일 (default: today)
```

#### 5-B-3: services/psi_service.py 핵심 로직

**`build_psi_full_matrix()` 함수 알고리즘:**

```
입력: PsiFilterParams
출력: list[PsiRowFull]

1. item_master에서 필터 조건에 맞는 품목 조회
   - expeditor_user_id가 있으면 tracking_user_id = expeditor_user_id
   - supply_type 필터 적용 (bom_items.supply_type 기준)
   - level 필터 적용

2. date_from 기준 D-Day ~ D+30 날짜 배열 생성 (31개)
   - 각 날짜의 ISO week 번호 계산
   - date_headers 목록 구성

3. 품목별 daily_demand 계산
   - part_list_snapshots에서 해당 part_number의 날짜별 required_qty SUM
   - model_code 필터가 있으면 해당 모델의 lot_id 기준으로 필터
   - Polars groupby([part_number, snapshot_date]).agg(pl.sum("required_qty"))

4. psi_records에서 available_qty 조회 (기존 로직 유지)

5. PsiRowFull 조립 후 반환
```

#### 5-B-4: api/routes/psi.py 신규 엔드포인트

```
GET  /api/v1/psi/matrix          → build_psi_full_matrix() 반환
PUT  /api/v1/psi/item/{item_id}/inventory
     body: { inventory_qty, defect_qty }
     → item_master.inventory_qty, defect_qty 업데이트

PATCH /api/v1/psi/item/{item_id}/pick
     body: { is_picked: bool }
     → item_master.is_picked 토글

GET  /api/v1/psi/models          → 현재 DP에 존재하는 model_code 목록 반환
     (Model.Suffix 필터 드롭다운용)
```

기존 `GET /api/v1/psi` 엔드포인트는 하위 호환성을 위해 유지한다.

---

### Task 5-C: 프론트엔드 — PSI 페이지 전면 재설계

**목표:** 실제 업무 PSI 화면에 맞는 새로운 UI를 구현한다.

#### 5-C-1: 필터 패널 (PSIPage 상단)

다음 필터를 가로 배치:
- **담당자(Expeditor)** 드롭다운: 사용자 목록 (전체 / 개인)
- **SupplyType** 드롭다운: 전체 / Assembly Pull / Supplier / Phantom
- **모델** 드롭다운: `/api/v1/psi/models` 에서 로드
- **기준일(D-Day)** 날짜 선택기 (default: 오늘)

#### 5-C-2: PSI 테이블 컴포넌트 (`PSIMatrixFull.tsx`)

**헤더 구조 (2행):**
```
행1: [품번] [품명] [공급사] [재고] | W20 (3칸) | W21 (7칸) | ...  ← 주차 병합셀
행2: [품번] [품명] [공급사] [재고] | D-Day | D+1 | D+2 | ...      ← 날짜 헤더
```

**바디 구조 — 품목당 2행:**
```
행A (메인): 품번 | 품명 | 1차협력사 | 재고수량(인라인편집) | [날짜별 소요량]
행B (서브): Level/SupplyType | TechSpec | 2차협력사 | 불량수량(인라인편집) | [동일 날짜, 색상 다름]
```

> 엑셀의 4행 블록을 웹 UI에서는 2행으로 압축한다.
> 행A = 핵심 식별 정보 + 재고수량
> 행B = 기술 메타 + 불량수량 (배경색 구분)

**셀 색상 규칙:**
- 소요량 > 0 이고 재고 부족: 빨간 배경
- 소요량 = 0: 회색/빈칸
- 소요량 > 0 이고 재고 충분: 초록 배경
- is_picked = true: 행 전체 좌측에 파란 마킹 인디케이터

**인라인 편집:**
- `재고수량` 셀 클릭 → 숫자 input → blur 시 `PUT /psi/item/{id}/inventory` 호출
- `불량수량` 셀 동일 방식
- `is_picked` → 행 좌측 체크박스 클릭 → `PATCH /psi/item/{id}/pick` 호출

**고정 컬럼:**
- 품번/품명/공급사/재고 컬럼은 좌측 fixed sticky (가로 스크롤 시 고정)
- 날짜 컬럼 31개는 가로 스크롤

#### 5-C-3: 타입 정의 업데이트 (types/api.ts)

`PsiRowFull` 인터페이스 추가:
```typescript
interface DateHeader {
  label: string;   // "D-Day", "D+1", ...
  date: string;    // "2026-05-16"
  week: number;    // ISO week number
}

interface PsiRowFull {
  item_id: number;
  part_number: string;
  description: string;
  level: number;
  supply_type: string | null;
  uom: string;
  vendor_raw: string | null;
  lower_vendor_raw: string | null;
  tech_spec: string | null;
  inventory_qty: number;
  defect_qty: number;
  is_picked: boolean;
  daily_demand: Record<string, number>;  // "D+0": qty, "D+1": qty, ...
  date_headers: DateHeader[];
  expeditor_name: string | null;
}
```

---

### Task 5-D: Alembic 마이그레이션 실행 및 검증

```bash
cd /test/LARS/backend
source venv/bin/activate
alembic upgrade head
alembic current  # 003이 head여야 함
```

---

### Task 5-E: 통합 검증

다음 시나리오를 순서대로 검증한다:

1. `GET /api/v1/psi/matrix` 호출 → `PsiRowFull` 배열 반환 확인
2. `GET /api/v1/psi/models` 호출 → 모델 코드 목록 반환 확인
3. 프론트엔드 PSI 페이지 접속 → 2행 블록 테이블 렌더링 확인
4. 재고수량 인라인 편집 → DB 반영 확인
5. is_picked 토글 → 행 마킹 시각적 변화 확인
6. 담당자 필터 선택 → 해당 담당자 품목만 표시 확인
7. TypeScript 오류 0건 확인: `npx tsc --noEmit`
8. Python 문법 확인: `python3 -m py_compile backend/services/psi_service.py`

---

## 구현 시 주의사항

1. **Polars 전용** — DataFrame 연산에 Pandas 사용 금지
2. **기존 PSI 엔드포인트 유지** — `GET /api/v1/psi` 하위 호환성 보장
3. **`daily_demand` 키 형식** — `"D+0"`, `"D+1"`, ..., `"D+30"` 문자열 (D-Day = D+0)
4. **날짜 계산** — `date_from` 기준 +0 ~ +30일, 주말 포함 달력 기준 (영업일 제외 안 함)
5. **프론트엔드 빌드** — 작업 완료 후 반드시 `npm run build` 실행 후 결과 보고

---

## 완료 보고 형식

작업 완료 후 `LARS_Project/Phase5_Coder_Report.md`를 작성하여 제출한다.
보고서에는 완료 항목, 검증 결과, 특이사항, 수정된 파일 목록을 포함한다.
