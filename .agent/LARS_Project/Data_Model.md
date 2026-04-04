# LARS 데이터 모델 초안

> 마지막 갱신: 2026-04-05

## 1. 핵심 엔티티

### 공통/권한
- `User`
- `Organization`
- `Membership`
- `Worksite`
- `Line`

### 작업/파일
- `ReportJob`
- `SourceFile`
- `ReportArtifact`
- `JobEvent`

### 도메인 데이터
- `BomRecord`
- `DailyPlanRecord`
- `PartListRecord`
- `ItemCounterRecord`
- `Feeder`
- `ModelInfo`

### 메타데이터/감사
- `MetadataNote`
- `MetadataTag`
- `AuditLog`

## 2. 핵심 관계

- 한 `Organization`은 여러 `User`를 가진다.
- 한 `User`는 여러 `Organization`에 속할 수 있으므로 연결은 `Membership`으로 관리한다.
- 한 `ReportJob`은 하나의 `Organization`에 귀속된다.
- 한 `ReportJob`은 여러 `SourceFile`을 입력으로 가진다.
- 한 `ReportJob`은 여러 `ReportArtifact`를 출력으로 가진다.
- `BomRecord`, `DailyPlanRecord`, `PartListRecord`, `ItemCounterRecord`는 각각 특정 작업 결과에 귀속된다.
- `MetadataNote`와 `MetadataTag`는 사용자 또는 AI가 기록할 수 있다.
- 모든 변경은 `AuditLog`에 남긴다.

## 3. ReportJob 필수 필드

- `id`
- `job_type`
- `status`
- `requested_by`
- `organization_id`
- `created_at`
- `started_at`
- `finished_at`
- `error_message`
- `input_summary`
- `output_summary`
- `retry_count`
- `processor_version`

## 4. ReportJob 상태값 기준

- `queued`
- `validating`
- `processing`
- `succeeded`
- `failed`
- `cancelled`

## 5. SourceFile 필수 필드

- `id`
- `job_id`
- `original_name`
- `storage_path`
- `file_type`
- `size_bytes`
- `checksum`
- `uploaded_by`
- `uploaded_at`
- `validation_status`
- `detected_line`
- `detected_work_date`

## 6. ReportArtifact 필수 필드

- `id`
- `job_id`
- `artifact_type`
- `storage_path`
- `mime_type`
- `created_at`
- `preview_metadata`
- `artifact_version`

## 7. 도메인 레코드 공통 원칙

- 각 도메인 레코드는 `job_id` 또는 `artifact_id`를 통해 생성 출처를 추적할 수 있어야 합니다.
- `라인`, `날짜`, `모델`, `파트`, `벤더`, `수량`은 쿼리 가능한 컬럼으로 분해합니다.
- 원본 셀 값이 필요한 경우를 대비해 `raw_payload` 또는 동등한 보존 필드를 둡니다.
- 검증 실패 또는 보정 이력은 별도 이벤트 또는 로그로 추적합니다.

## 8. AI 대응을 위한 메타데이터 원칙

- 파일 이름만 저장하지 않고 구조화된 필드를 저장합니다.
- AI가 쓴 요약/태그/노트는 별도 테이블에 저장합니다.
- 사람이 수정한 메타데이터와 AI가 제안한 메타데이터를 구분합니다.
- AI가 기록한 메타데이터는 승인 여부와 승인자를 추적할 수 있어야 합니다.
