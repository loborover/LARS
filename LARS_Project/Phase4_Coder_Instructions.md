# Phase 4 Coder Instructions

> 작성자: Chief (AI Agent)
> 작성일: 2026-04-26
> 대상: Coder (Gemini Pro 3.1)
> 기준 문서: `LARS_Project/New_LARS_Project.md` (v3)
> 선행 완료: Phase 1, 2, 3, 3.5

---

## 0. 지시 배경 및 사용자 리뷰 요약

Phase 3.5까지의 구현이 완료된 상태에서 사용자(Owner)가 실사용 관점의 리뷰를 제출하였다.
본 Phase 4는 해당 리뷰 피드백 3건을 반영하여 프론트엔드 UI 구조 개편 및 백엔드 로직 수정을 수행한다.

### 사용자 리뷰 원문

1. **Multi-file Import**: 현재 Import가 단일 파일 하나씩만 업로드 가능하다. 여러 개의 파일을 한 번에 업로드하는 기능이 필요하다.
2. **ItemMaster(IT) Import 제거**: IT는 BOMDB를 조회하여 PartNumber를 key로 merge된 결과물이므로 별도 Import할 이유가 없다. Import 대상에서 제외할 것. BOMDB에 기재된 데이터에서 PartNumber를 key로 하여 merge된 table이 IT이다. 보통 중복되지 않으므로 PartNumber, Vendor, Description 등의 열로 구성된 테이블로 만들면 된다.
3. **AutoReport 탭 구조 도입**: BOM, DailyPlan, PartList, ItemMaster, PSI 등을 `AutoReport`라는 큰 탭 아래 작은 탭으로 귀속시킬 것. DP, PL 등의 약어 대신 Full Name을 사용하여 일반 사용자에게 기능을 명확히 전달할 것.

---

## 1. Task 4-A: Multi-file Import 지원

### 1.1 목적

현재 Import 파이프라인은 단일 파일만 업로드 가능하다. 사용자가 여러 파일(예: BOM 파일 5개, DP 파일 3개)을 한 번에 드래그 앤 드롭하여 일괄 처리할 수 있도록 확장한다.

### 1.2 백엔드 변경

#### 1.2.1 `backend/api/routes/import_pipeline.py`

- 기존 `POST /import/upload` 엔드포인트를 수정하거나 새 엔드포인트 `POST /import/upload-multi`를 추가한다.
- 다중 파일을 수신한다:
  ```python
  @router.post("/upload-multi")
  async def upload_multi_files(
      files: list[UploadFile] = File(...),
      target_table: str = Form(...),
      current_user: User = Depends(get_current_user),
      session: AsyncSession = Depends(get_session)
  ):
  ```
- 각 파일마다 별도의 `ImportBatch` 레코드를 생성한다.
- 응답으로 생성된 batch_id 목록을 반환한다:
  ```json
  {
    "batches": [
      { "batch_id": 101, "filename": "BOM_A.xlsx", "status": "pending" },
      { "batch_id": 102, "filename": "BOM_B.xlsx", "status": "pending" }
    ]
  }
  ```

#### 1.2.2 `POST /import/preview-multi`

- batch_id 배열을 받아 각 batch의 preview를 일괄 반환하는 엔드포인트를 추가한다.
- 응답 형식:
  ```json
  {
    "previews": [
      { "batch_id": 101, "total_rows": 200, "valid_rows": 198, "invalid_rows": 2, "preview": [...] },
      { "batch_id": 102, "total_rows": 150, "valid_rows": 150, "invalid_rows": 0, "preview": [...] }
    ]
  }
  ```

#### 1.2.3 `POST /import/batches/process-multi`

- batch_id 배열을 받아 순차적으로 각 batch를 처리한다.
- 하나의 batch가 실패해도 나머지는 계속 처리한다.
- 응답으로 각 batch의 처리 결과를 반환한다.

#### 1.2.4 기존 단일 업로드 엔드포인트 유지

- `POST /import/upload`, `GET /import/preview/{batch_id}`, `POST /import/batches/{batch_id}/process`는 하위 호환성을 위해 유지한다.

### 1.3 프론트엔드 변경

#### 1.3.1 `ImportPage.tsx` 리팩토링

- `useDropzone`의 `maxFiles` 제한을 제거한다 (현재 `maxFiles: 1`).
- 선택된 파일 목록을 표형태로 표시한다 (파일명, 크기, 상태).
- `handleUpload`에서 `/import/upload-multi` 엔드포인트를 호출한다.
- Step 2(미리보기)에서 탭 또는 아코디언 형태로 각 파일의 preview를 표시한다.
- Step 3(처리 결과)에서 각 파일별 성공/실패 상태를 보여준다.
- 개별 파일 삭제 버튼을 제공한다 (업로드 전 파일 목록에서 개별 제거 가능).

### 1.4 스키마 변경

- 새 응답 스키마가 필요하다면 `schemas/import_batch.py`에 추가한다:
  ```python
  class MultiUploadResponse(BaseModel):
      batches: list[BatchUploadResult]

  class BatchUploadResult(BaseModel):
      batch_id: int
      filename: str
      status: str

  class MultiPreviewResponse(BaseModel):
      previews: list[PreviewResponse]

  class MultiProcessResponse(BaseModel):
      results: list[BatchRead]
  ```

### 1.5 검증 기준

- 3개 이상의 BOM 엑셀 파일을 동시에 드래그 앤 드롭하여 업로드한다.
- 각 파일의 preview가 개별적으로 표시되는지 확인한다.
- 일괄 처리 후 각 파일의 성공/실패 상태가 구분되어 표시되는지 확인한다.
- 단일 파일 업로드 기존 엔드포인트(`POST /import/upload`)가 여전히 정상 동작하는지 확인한다.

---

## 2. Task 4-B: ItemMaster(IT) Import 제거 및 BOMDB 파생 로직 구현

### 2.1 목적

ItemMaster(IT)는 독립적으로 Import되는 데이터가 아니다.
BOMDB(bom_items 테이블)에 이미 적재된 데이터에서 `part_number`를 key로 중복을 제거하고 merge하여 자동으로 생성되는 파생 테이블이다.
따라서 IT의 Import 기능을 제거하고, BOM Import 시 자동으로 IT를 갱신하는 로직으로 전환한다.

### 2.2 백엔드 변경

#### 2.2.1 `backend/services/item_master_service.py` — 새 함수 추가

- `rebuild_from_bom()` 함수를 추가한다:
  ```python
  async def rebuild_from_bom(session: AsyncSession) -> int:
      """
      bom_items 테이블에서 part_number를 key로 중복 제거하여
      item_master 테이블을 갱신(upsert)한다.
      
      merge 기준:
      - part_number (UNIQUE KEY)
      - description: bom_items에서 해당 part_number의 첫 번째 값
      - vendor_raw: bom_items에서 해당 part_number의 첫 번째 값
      - level: bom_items에서 해당 part_number의 첫 번째 값
      
      기존 item_master에 존재하는 part_number는 UPDATE,
      새로운 part_number는 INSERT한다.
      bom_items에 더 이상 존재하지 않는 part_number는 is_active = FALSE 처리한다.
      """
  ```
- SQL 쿼리 예시 (Polars 사용):
  ```python
  # 1. bom_items에서 고유 part_number 추출
  stmt = select(
      BomItem.part_number,
      BomItem.description,
      BomItem.vendor_raw,
      BomItem.level
  ).distinct(BomItem.part_number)
  
  # 2. 결과를 Polars DataFrame으로 변환
  # 3. part_number 기준으로 중복 제거 (first 값 유지)
  # 4. item_master 테이블과 upsert
  ```

#### 2.2.2 BOM Import 후 자동 IT 갱신 트리거

- `backend/api/routes/import_pipeline.py`의 BOM 처리 블록에서 BOM Import 성공 후 `item_master_service.rebuild_from_bom()`을 호출한다:
  ```python
  if batch.target_table == "bom":
      df = bom_parser.parse(file_path)
      val_res = validator.validate_bom(df)
      if not val_res.is_valid:
          raise Exception("Validation failed")
      inserted = await bom_service.import_from_df(session, df, batch.id)
      batch.records_inserted = inserted
      
      # IT 자동 갱신
      await item_master_service.rebuild_from_bom(session)
  ```

#### 2.2.3 Import 대상에서 `item_master` 제거

- `import_pipeline.py`의 `upload_file()` 함수에서 `target_table` 유효값 목록에서 `"item_master"`를 제거한다:
  ```python
  # 변경 전
  if target_table not in ["bom", "daily_plan", "item_master"]:
  
  # 변경 후
  if target_table not in ["bom", "daily_plan"]:
  ```

#### 2.2.4 기존 `import_from_df()` 유지

- `item_master_service.py`의 `import_from_df()` 함수는 삭제하지 않고 유지한다.
  향후 특수한 수동 Import 필요 시 재활용할 수 있도록 `_legacy_import_from_df`로 이름을 변경하고 주석을 남긴다.

#### 2.2.5 IT API 엔드포인트 유지

- `GET /items` (목록 조회), `GET /items/{id}` (상세), `PUT /items/{id}` (수정) API는 그대로 유지한다.
- `POST /items/import` 엔드포인트가 존재한다면 제거하거나, HTTP 410 Gone으로 응답하도록 변경한다.
- IT 데이터의 수동 CRUD(개별 품목 추가, 수정, 비활성화)는 유지한다.

### 2.3 프론트엔드 변경

#### 2.3.1 `ImportPage.tsx`

- Import 대상 선택 라디오 버튼에서 `IT 품목(item_master)` 옵션을 제거한다:
  ```tsx
  // 제거할 항목:
  // <label>
  //   <input type="radio" value="item_master" ... />
  //   IT 품목
  // </label>
  ```

#### 2.3.2 `ItemMasterPage.tsx`

- IT 페이지 상단에 안내 배너를 추가한다:
  ```
  ℹ️ ItemMaster는 BOM 데이터에서 자동으로 생성됩니다.
  BOM을 Import하면 ItemMaster가 자동으로 갱신됩니다.
  ```

### 2.4 검증 기준

- BOM 파일을 Import한 후, `GET /api/v1/items`로 ItemMaster 목록을 조회하여 BOM에 포함된 모든 고유 part_number가 IT에 반영되었는지 확인한다.
- Import 페이지에서 `IT 품목` 라디오 버튼이 더 이상 표시되지 않는지 확인한다.
- bom_items에서 삭제(또는 없어진) part_number의 item_master 레코드가 `is_active = FALSE`로 변경되는지 확인한다.

---

## 3. Task 4-C: AutoReport 탭 구조 도입 및 메뉴 재편

### 3.1 목적

현재 사이드바에 BOM, DP, PL, IT 품목, PSI 등이 개별 메뉴로 흩어져 있다.
이들을 **AutoReport**라는 상위 그룹 아래에 귀속시켜 일반 사용자가 관련 기능의 연관성을 명확히 인지하도록 한다.
또한 약어(DP, PL, IT 등)를 Full Name으로 변경하여 가독성을 높인다.

### 3.2 명칭 매핑

| 현재 약어/명칭 | 변경 후 Full Name | 라우트 경로 (변경 없음) |
|---|---|---|
| BOM | BOM (자재명세서) | `/bom` |
| DP | Daily Plan (일일생산계획) | `/dp` |
| PL | Part List (소요자재목록) | `/pl` |
| IT 품목 | Item Master (품목마스터) | `/items` |
| PSI | PSI (수급현황) | `/psi` |

### 3.3 프론트엔드 변경

#### 3.3.1 `AppLayout.tsx` — 사이드바 메뉴 구조 재편

- **AutoReport** 그룹을 접이식(collapsible) 메뉴로 구현한다.
- 클릭 시 하위 메뉴가 펼쳐지는 아코디언 방식을 사용한다.
- 기본 상태는 펼쳐진(expanded) 상태로 한다.
- 구현 예시:
  ```tsx
  {/* Sidebar Navigation */}
  <Link to="/dashboard">Dashboard</Link>
  
  {/* AutoReport Group */}
  <div>
    <button onClick={toggleAutoReport}>
      AutoReport {isAutoReportOpen ? '▼' : '▶'}
    </button>
    {isAutoReportOpen && (
      <div className="pl-4">
        <Link to="/bom">BOM (자재명세서)</Link>
        <Link to="/dp">Daily Plan (일일생산계획)</Link>
        <Link to="/pl">Part List (소요자재목록)</Link>
        <Link to="/items">Item Master (품목마스터)</Link>
        <Link to="/psi">PSI (수급현황)</Link>
      </div>
    )}
  </div>
  
  <Link to="/efficiency">물류효율표</Link>
  <Link to="/wip">표준재공표</Link>
  <Link to="/import">Import</Link>
  <Link to="/ai">AI 어시스턴트</Link>
  <Link to="/tickets">티켓 관리</Link>
  ```

#### 3.3.2 모바일 하단 탭바 업데이트

- 하단 탭바에서도 약어 대신 축약된 Full Name을 사용한다.
- 공간이 제한적이므로 아이콘 + 짧은 한국어 명칭 조합을 사용한다:
  ```tsx
  <Link to="/dashboard">홈</Link>
  <Link to="/bom">BOM</Link>
  <Link to="/dp">생산계획</Link>
  <Link to="/items">품목</Link>
  <Link to="/psi">수급</Link>
  ```

#### 3.3.3 각 페이지 제목 업데이트

각 페이지 컴포넌트의 `<h1>` 제목을 Full Name으로 변경한다:

| 파일 | 현재 제목 | 변경 후 제목 |
|---|---|---|
| `DailyPlanPage.tsx` | 확인 필요 | `Daily Plan (일일생산계획)` |
| `PartListPage.tsx` | 확인 필요 | `Part List (소요자재목록)` |
| `ItemMasterPage.tsx` | 확인 필요 | `Item Master (품목마스터)` |
| `PSIPage.tsx` | 확인 필요 | `PSI (수급현황)` |
| `BOMListPage.tsx` | 확인 필요 | `BOM (자재명세서)` |

> **참고:** 각 파일의 실제 `<h1>` 태그 텍스트를 확인한 뒤 정확히 변경할 것.

### 3.4 라우팅 변경 없음

- URL 경로(`/bom`, `/dp`, `/pl`, `/items`, `/psi`)는 변경하지 않는다.
- `App.tsx`의 `<Route>` 정의는 그대로 유지한다.
- 변경 범위는 사이드바 메뉴 구조와 각 페이지의 표시명에 한정한다.

### 3.5 검증 기준

- 사이드바에서 `AutoReport` 그룹이 아코디언 형태로 표시되고, 클릭 시 5개 하위 메뉴가 펼쳐지고 접히는지 확인한다.
- 각 하위 메뉴 클릭 시 올바른 페이지로 이동하는지 확인한다.
- 각 페이지의 `<h1>` 제목이 Full Name으로 표시되는지 확인한다.
- 모바일 하단 탭바에서 약어 대신 한국어 명칭이 표시되는지 확인한다.

---

## 4. Task 실행 순서

| 순서 | Task | 의존성 |
|---|---|---|
| 1 | Task 4-B (IT Import 제거 + BOMDB 파생 로직) | 없음 |
| 2 | Task 4-A (Multi-file Import) | Task 4-B 완료 후 (Import 대상 변경 반영) |
| 3 | Task 4-C (AutoReport 탭 구조 + 명칭 변경) | 없음 (4-A, 4-B와 독립적이나 최종 UI 정합성을 위해 마지막 수행 권장) |

---

## 5. 전체 검증 시퀀스

Phase 4의 모든 Task 완료 후 아래 시퀀스를 순서대로 수행한다:

1. **로그인**: `POST /api/v1/auth/login` → 토큰 획득
2. **BOM Multi-file Import**: 3개 이상의 BOM 엑셀 파일을 Import 페이지에서 동시 업로드 → 각 파일 preview 확인 → 일괄 처리
3. **IT 자동 갱신 확인**: `GET /api/v1/items` → Import된 BOM의 모든 고유 part_number가 ItemMaster에 반영되었는지 확인
4. **IT Import 비활성화 확인**: Import 페이지에서 `IT 품목` 라디오 버튼이 없는지 확인
5. **사이드바 AutoReport 확인**: 사이드바에서 AutoReport 그룹이 아코디언으로 동작하고 5개 하위 메뉴가 Full Name으로 표시되는지 확인
6. **각 페이지 제목 확인**: BOM, Daily Plan, Part List, Item Master, PSI 각 페이지의 `<h1>` 제목이 Full Name인지 확인
7. **TypeScript 검증**: `.WebUI`에서 `npx tsc --noEmit` → 오류 0건 확인
8. **Python 문법 검증**: 수정된 모든 `.py` 파일에 대해 `py_compile` → 오류 0건 확인

---

## 6. 코딩 규칙 (기존과 동일)

- Polars 전용 데이터 처리 (Pandas 사용 금지)
- SQLModel ORM 기반 DB 조작
- Pydantic schema를 통한 request/response 타입 정의
- 기존 파일의 주석 및 문서는 보존
- git 작업 금지 (사용자 전담)
- 변경된 파일 목록을 `Phase4_Coder_Report.md`에 명시

---

*이 문서는 `LARS_Project/` 아래에서 관리됩니다. Chief 작성 — 2026-04-26.*
