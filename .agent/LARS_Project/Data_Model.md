# LARS 데이터 모델 초안

> 마지막 갱신: 2026-04-04

## 1. 핵심 엔티티

- `User`
- `Organization`
- `Worksite`
- `Line`
- `ReportJob`
- `SourceFile`
- `ReportArtifact`
- `BomRecord`
- `DailyPlanRecord`
- `PartListRecord`
- `ItemCounterRecord`
- `Feeder`
- `ModelInfo`
- `MetadataNote`
- `AuditLog`

## 2. 핵심 관계

- 한 `Organization`은 여러 `User`를 가진다.
- 한 `ReportJob`은 여러 `SourceFile`을 입력으로 가진다.
- 한 `ReportJob`은 여러 `ReportArtifact`를 출력으로 가진다.
- `BomRecord`, `DailyPlanRecord`, `PartListRecord`, `ItemCounterRecord`는 각각 특정 작업 결과에 귀속된다.
- `MetadataNote`는 사용자 또는 AI가 기록할 수 있다.
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

## 4. SourceFile 필수 필드

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

## 5. ReportArtifact 필수 필드

- `id`
- `job_id`
- `artifact_type`
- `storage_path`
- `mime_type`
- `created_at`
- `preview_metadata`

## 6. AI 대응을 위한 메타데이터 원칙

- 파일 이름만 저장하지 않고 구조화된 필드를 저장한다.
- `라인`, `날짜`, `모델`, `파트`, `벤더`, `수량`은 쿼리 가능한 컬럼으로 분해한다.
- AI가 쓴 요약/태그/노트도 별도 테이블에 저장한다.
- 사람이 수정한 메타데이터와 AI가 제안한 메타데이터를 구분한다.
