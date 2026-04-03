# LARS 플랫폼 아키텍처

> 마지막 갱신: 2026-04-04

## 1. 전체 구조

```text
LARS Platform
├── Web App (Next.js)
├── API Service (FastAPI)
├── Worker Service (Celery)
├── Redis
├── PostgreSQL
├── File Storage
└── AI / Metadata Extensions
```

## 2. 역할 분리

### Web App
- 로그인
- 업로드
- 작업 상태 조회
- 결과 미리보기 및 다운로드
- 대시보드
- AI 채팅 UI

### API Service
- 인증/권한
- 업로드 접수
- 메타데이터 CRUD
- 작업 생성
- 검색 API
- AI 연동용 read/write API

### Worker Service
- BOM/DailyPlan/PartList 파싱
- ItemCounter 계산
- MultiDocument 배치 처리
- PDF 생성
- 후처리 및 인덱싱

### PostgreSQL
- 사용자/조직/권한
- 파일 메타데이터
- 작업 상태
- 결과 아티팩트
- 검색 가능한 구조화 데이터
- 감사 로그

### Redis
- 작업 큐
- 비동기 상태 공유
- 캐시

### File Storage
- 원본 파일
- 가공 산출물
- PDF
- 로그 첨부

## 3. 아키텍처 원칙

- 웹 요청은 짧고 빠르게 끝난다.
- 무거운 계산은 Worker로 보낸다.
- 모든 핵심 산출물은 DB와 저장소에 분리 저장한다.
- AI는 API를 통해 metadata와 아티팩트를 읽고 쓴다.

## 4. 권장 배포 구조

### 초기 배포
- Reverse Proxy: Nginx
- `web`
- `api`
- `worker`
- `redis`
- `postgres`
- `storage`

### 향후 확장
- AI service 분리
- 검색 인덱스 추가
- object storage 분리
- observability 스택 추가

## 5. 비기능 요구사항

- 최소 50명 동시 접속
- 조직/협력사 구분 권한
- 감사 로그
- 백업 가능 구조
- 서비스 중단 없이 작업 상태 추적 가능
