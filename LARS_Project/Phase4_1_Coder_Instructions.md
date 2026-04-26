# Phase 4.1 Coder Instructions — Multi-file Upload 버그 수정 및 UX 개선

> 작성자: Chief (AI Agent)
> 작성일: 2026-04-26
> 대상: Coder (Gemini Pro 3.1)
> 기준 문서: `LARS_Project/Phase4_Coder_Report.md` (Phase 4 완료 상태)
> 선행 완료: Phase 1, 2, 3, 3.5, 4

---

## 0. 배경

Phase 4에서 Multi-file Import 기능이 구현되었으나, 실제 사용 시 **Network Error**가 발생한다.
원인 분석 결과 **백엔드 import 누락 버그**가 확인되었으며, 추가로 사용자 UX 개선(Progress Bar, 완료 체크마크)이 요청되었다.

---

## 1. Task 4.1-A: Network Error 버그 수정 (Critical)

### 1.1 버그 원인

`backend/api/routes/import_pipeline.py`의 **Line 12**에서 Multi-file 전용 스키마가 import되지 않았다.

**현재 코드 (Line 12):**
```python
from schemas.import_batch import BatchRead, PreviewResponse, PreviewRow
```

`MultiUploadResponse`, `MultiPreviewResponse`, `MultiProcessResponse`, `BatchUploadResult`가 **import 목록에 누락**되어 있다.
이 4개 클래스는 `schemas/import_batch.py`에 정의되어 있으나, `import_pipeline.py`에서 사용 시점에 `NameError`가 발생하여 FastAPI가 라우트 등록 시점 또는 요청 처리 시점에 HTTP 500 → 프론트엔드에서 Network Error로 표시된다.

### 1.2 수정 내용

#### `backend/api/routes/import_pipeline.py` — Line 12

**변경 전:**
```python
from schemas.import_batch import BatchRead, PreviewResponse, PreviewRow
```

**변경 후:**
```python
from schemas.import_batch import (
    BatchRead, PreviewResponse, PreviewRow,
    BatchUploadResult, MultiUploadResponse, MultiPreviewResponse, MultiProcessResponse
)
```

### 1.3 추가 확인 — `target_table` 유효값 불일치

`import_pipeline.py` Line 27의 기존 단일 업로드 엔드포인트(`POST /upload`)에서 `target_table` 유효값에 `"item_master"`가 아직 남아 있다:

```python
# Line 27 — 변경 전:
if target_table not in ["bom", "daily_plan", "item_master"]:

# 변경 후 (Phase 4-B 지시사항 반영):
if target_table not in ["bom", "daily_plan"]:
```

> Phase 4 Report에서는 IT Import 제거를 완료했다고 보고하였으나, 단일 업로드 엔드포인트에 `"item_master"`가 여전히 남아 있다. 이것도 함께 제거할 것.

### 1.4 검증 기준

- 백엔드 서버를 재시작한 후, **서버 기동 로그에 import 관련 에러가 없는지** 확인한다.
- `POST /api/v1/import/upload-multi`로 2개 이상의 BOM 파일을 업로드하여 정상 응답(HTTP 200)이 돌아오는지 확인한다.
- 프론트엔드에서 Network Error가 더 이상 발생하지 않는지 확인한다.

---

## 2. Task 4.1-B: 파일별 업로드 Progress Bar 및 완료 체크마크 추가

### 2.1 목적

Multi-file Import 시 사용자가 각 파일의 업로드 진행 상태를 시각적으로 확인할 수 있도록 **Progress Bar**와 **완료 체크마크**를 추가한다.

### 2.2 동작 설계

업로드 과정은 3단계로 진행된다:

```
[Step 1] 파일 선택 → [업로드] 클릭
    ↓
[Step 1.5 — 신규] 업로드 진행 화면
    각 파일마다:
    ├─ 파일명
    ├─ Progress Bar (0% → 100%)
    ├─ 업로드 중: 파란색 프로그레스 바 + "업로드 중..." 텍스트
    ├─ 업로드 완료: 초록색 프로그레스 바 + ✅ 체크마크 + "완료" 텍스트
    └─ 업로드 실패: 빨간색 프로그레스 바 + ❌ + 오류 메시지
    ↓
[Step 2] 미리보기 (기존과 동일)
    ↓
[Step 3] 처리 결과 (기존과 동일)
```

### 2.3 프론트엔드 변경

#### 2.3.1 `ImportPage.tsx` — 파일별 개별 업로드 방식으로 전환

현재 방식은 모든 파일을 하나의 FormData에 묶어 `/upload-multi`로 전송한다.
이 방식은 **전체 업로드가 하나의 HTTP 요청**이므로 파일별 진행률 추적이 불가능하다.

**해결 방법:** 각 파일을 **개별적으로** `/import/upload` (기존 단일 업로드 엔드포인트)를 통해 업로드하되, `Promise.allSettled`를 사용하여 병렬로 처리한다. 각 요청마다 Axios의 `onUploadProgress`를 활용하여 파일별 진행률을 추적한다.

#### 2.3.2 상태 관리 추가

```tsx
// 파일별 업로드 상태 타입
interface FileUploadStatus {
  file: File;
  progress: number;        // 0~100
  status: 'pending' | 'uploading' | 'done' | 'error';
  batchId: number | null;
  errorMsg: string;
}

// 기존 files state를 FileUploadStatus 배열로 대체
const [fileStatuses, setFileStatuses] = useState<FileUploadStatus[]>([]);
```

#### 2.3.3 업로드 핸들러 변경

```tsx
const handleUpload = async () => {
  if (fileStatuses.length === 0) return;
  setLoading(true);
  setErrorMsg('');

  // 모든 파일을 'uploading' 상태로 전환
  setFileStatuses(prev => prev.map(fs => ({ ...fs, status: 'uploading', progress: 0 })));

  // 각 파일을 개별적으로 업로드
  const uploadPromises = fileStatuses.map((fs, index) => {
    const formData = new FormData();
    formData.append('file', fs.file);
    formData.append('target_table', targetTable);

    return apiClient.post('/import/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (progressEvent) => {
        const percent = progressEvent.total
          ? Math.round((progressEvent.loaded * 100) / progressEvent.total)
          : 0;
        setFileStatuses(prev => prev.map((item, i) =>
          i === index ? { ...item, progress: percent } : item
        ));
      }
    })
    .then(res => {
      setFileStatuses(prev => prev.map((item, i) =>
        i === index ? { ...item, status: 'done', progress: 100, batchId: res.data.batch_id } : item
      ));
      return { success: true, batchId: res.data.batch_id };
    })
    .catch(err => {
      const msg = err.response?.data?.detail || err.message || '업로드 실패';
      setFileStatuses(prev => prev.map((item, i) =>
        i === index ? { ...item, status: 'error', errorMsg: msg } : item
      ));
      return { success: false, error: msg };
    });
  });

  const results = await Promise.allSettled(uploadPromises);

  // 성공한 batch들의 ID를 수집하여 preview 단계로 진행
  const successBatchIds = fileStatuses
    .filter(fs => fs.status === 'done' && fs.batchId)
    .map(fs => fs.batchId!);

  if (successBatchIds.length > 0) {
    // preview-multi 호출 또는 개별 preview 호출
    // ...기존 preview 로직 유지...
  }

  setLoading(false);
};
```

#### 2.3.4 UI 컴포넌트 — 업로드 진행 화면

Step 1의 "다음: 미리보기" 버튼 클릭 후, 업로드가 진행되는 동안 파일 목록 영역을 진행 상태 표시로 전환한다:

```tsx
{/* 업로드 진행 상태 표시 */}
{fileStatuses.map((fs, idx) => (
  <div key={idx} className="flex items-center space-x-4 p-3 bg-gray-50 rounded">
    {/* 파일명 */}
    <div className="flex-1 min-w-0">
      <div className="text-sm font-medium truncate">{fs.file.name}</div>
      <div className="text-xs text-gray-500">{(fs.file.size / 1024).toFixed(1)} KB</div>
    </div>

    {/* Progress Bar */}
    <div className="flex-1">
      <div className="w-full bg-gray-200 rounded-full h-2.5">
        <div
          className={`h-2.5 rounded-full transition-all duration-300 ${
            fs.status === 'error' ? 'bg-red-500' :
            fs.status === 'done' ? 'bg-green-500' :
            'bg-blue-500'
          }`}
          style={{ width: `${fs.progress}%` }}
        />
      </div>
    </div>

    {/* 상태 아이콘 + 텍스트 */}
    <div className="w-24 text-right text-sm">
      {fs.status === 'pending' && <span className="text-gray-400">대기 중</span>}
      {fs.status === 'uploading' && <span className="text-blue-600">{fs.progress}%</span>}
      {fs.status === 'done' && <span className="text-green-600">✅ 완료</span>}
      {fs.status === 'error' && (
        <span className="text-red-600" title={fs.errorMsg}>❌ 실패</span>
      )}
    </div>
  </div>
))}
```

#### 2.3.5 파일 추가/제거 로직 수정

```tsx
// 파일 추가 (useDropzone의 onDrop)
const onDrop = (acceptedFiles: File[]) => {
  const newStatuses = acceptedFiles.map(file => ({
    file,
    progress: 0,
    status: 'pending' as const,
    batchId: null,
    errorMsg: ''
  }));
  setFileStatuses(prev => [...prev, ...newStatuses]);
};

// 파일 제거
const removeFile = (index: number) => {
  setFileStatuses(prev => prev.filter((_, i) => i !== index));
};
```

#### 2.3.6 Step 전환 로직

- 업로드가 모든 파일에 대해 완료(done 또는 error)된 후, 성공한 파일이 1개 이상이면 자동으로 Step 2(미리보기)로 전환한다.
- 모든 파일이 실패한 경우 에러 메시지를 표시하고 Step 1에 머문다.
- "다음: 미리보기" 버튼은 업로드가 진행 중일 때 비활성화한다.

### 2.4 검증 기준

- 3개 이상의 파일을 선택하고 업로드 시, 각 파일마다 독립적인 Progress Bar가 0%에서 100%까지 채워지는지 확인한다.
- 업로드 완료된 파일에 ✅ 체크마크와 "완료" 텍스트가 표시되는지 확인한다.
- 업로드 실패한 파일에 ❌와 오류 메시지가 표시되는지 확인한다.
- 일부 파일만 성공했을 때 성공한 파일만 Step 2(미리보기)로 진행되는지 확인한다.
- TypeScript 검증: `npx tsc --noEmit` → 오류 0건 확인.

---

## 3. Task 실행 순서

| 순서 | Task | 우선도 | 의존성 |
|---|---|---|---|
| 1 | Task 4.1-A (Import 누락 버그 수정) | **Critical** | 없음. 즉시 수정 필요 |
| 2 | Task 4.1-B (Progress Bar + 체크마크) | High | Task 4.1-A 완료 후 |

---

## 4. 전체 검증 시퀀스

1. **서버 재시작**: 백엔드 서버 재기동 후 import 관련 에러 로그가 없는지 확인
2. **단일 파일 업로드**: `POST /api/v1/import/upload`로 BOM 파일 1개 업로드 → 정상 응답 확인
3. **다중 파일 업로드**: Import 페이지에서 BOM 파일 3개를 드래그 앤 드롭
   - 각 파일별 Progress Bar가 독립적으로 동작하는지 확인
   - 모든 파일 업로드 완료 시 ✅ 체크마크 표시 확인
   - 자동으로 Step 2(미리보기) 전환 확인
4. **일괄 처리**: 미리보기 확인 후 "일괄 처리 시작" → 각 파일별 성공/실패 결과 확인
5. **TypeScript 검증**: `npx tsc --noEmit` → 오류 0건
6. **Python 문법 검증**: 수정된 `.py` 파일 `py_compile` → 오류 0건

---

## 5. 코딩 규칙 (기존과 동일)

- Polars 전용 데이터 처리 (Pandas 사용 금지)
- 기존 파일의 주석 및 문서는 보존
- git 작업 금지 (사용자 전담)
- 변경된 파일 목록을 `Phase4_1_Coder_Report.md`에 명시

---

*이 문서는 `LARS_Project/` 아래에서 관리됩니다. Chief 작성 — 2026-04-26.*
