# LARS 서버 시동 가이드

> 작성일: 2026-04-27
> 작성: Chief (AI Agent)
> 대상: LARS 운영자 / 개발자
> 작업 디렉토리 기준: `/test/LARS/`

---

## 시스템 구성 개요

```
[브라우저]
    │ :3000
    ▼
[Vite Dev Server]  ← .WebUI/
    │ Proxy /api → :8000
    │ Proxy /ws  → :8000
    ▼
[FastAPI Backend]  ← backend/
    │
    ├── PostgreSQL :5433  ← Docker
    ├── Redis      :6379  ← Docker
    └── AI Service :8088  ← AI PC (192.168.0.100) [선택]
```

---

## 1단계 — 인프라 기동 (Docker)

PostgreSQL 16(pgvector) + Redis를 Docker로 기동한다.

```bash
cd /test/LARS
docker compose up -d
```

기동 확인:

```bash
docker compose ps
# 상태: postgres → Up, redis → Up
```

> **포트 안내**
> - PostgreSQL: 호스트 **5433** → 컨테이너 5432
> - Redis: 호스트 **6379** → 컨테이너 6379
>
> DB 연결 정보 (backend/.env 기준):
> `DATABASE_URL=postgresql+asyncpg://lars:lars_secret@172.17.0.1:5433/lars_db`

---

## 2단계 — DB 마이그레이션

> **최초 1회만 실행.** 이미 마이그레이션이 적용된 상태라면 생략해도 된다.

```bash
cd /test/LARS/backend
source venv/bin/activate
alembic upgrade head
```

성공 시 출력 예시:
```
INFO  [alembic.runtime.migration] Running upgrade -> 773a5b8ef6e1, 001_initial_schema
INFO  [alembic.runtime.migration] Running upgrade 773a5b8ef6e1 -> ..., 002_add_daily_qty_json
```

현재 적용된 마이그레이션 버전 확인:
```bash
alembic current
```

---

## 3단계 — Admin 계정 생성

> **최초 1회만 실행.** 이미 계정이 존재하면 "이미 존재합니다" 출력 후 종료된다.

```bash
cd /test/LARS/backend
source venv/bin/activate
python create_admin.py
```

생성되는 기본 계정:
- **이메일:** `admin@lars.local`
- **비밀번호:** `admin1234`
- **역할:** `admin`

---

## 4단계 — 백엔드 서버 기동

```bash
cd /test/LARS/backend
source venv/bin/activate
uvicorn main:app --host 0.0.0.0 --port 8000
```

정상 기동 로그 확인:
```
[LARS] DB 연결 성공
[LARS] PSI 모니터 스케줄러 시작 (interval=15분, tz=Asia/Seoul)
[LARS] AI 모드: internal
INFO:     Application startup complete.
```

개발 모드(코드 변경 시 자동 재로드):
```bash
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

헬스 체크:
```bash
curl http://localhost:8000/health
# 응답: {"status": "ok", "ai_mode": "internal"}
```

---

## 5단계 — 프론트엔드 서버 기동

```bash
cd /test/LARS/.WebUI
npm run dev
```

정상 기동 시:
```
  VITE vX.X.X  ready in ... ms

  ➜  Local:   http://localhost:3000/
  ➜  Network: http://0.0.0.0:3000/
```

브라우저 접속:
- 로컬: `http://localhost:3000`
- 원격: `http://<서버IP>:3000`

> **Vite Proxy 설명**
> 브라우저에서 `/api/v1/*` 요청은 Vite가 자동으로 `http://localhost:8000`으로 중계한다.
> 원격 PC에서 접속해도 IP를 별도로 설정할 필요 없다.

---

## 6단계 (선택) — AI Service 기동 (AI PC 전용)

> `AI_MODE=internal`로 설정된 경우, AI PC(192.168.0.100)에서 별도로 실행해야 한다.
> `AI_MODE=disabled` 또는 `AI_MODE=local`이면 이 단계는 불필요하다.

**AI PC에서 실행:**

```bash
cd /path/to/LARS/lars_ai_service
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8088
```

또는 Docker로 실행:

```bash
cd /test/LARS
docker compose -f docker-compose.ai.yml up -d
```

헬스 체크:
```bash
curl http://192.168.0.100:8088/health
# 응답: {"status": "ok", "service": "lars-ai"}
```

---

## 로그인 확인

브라우저에서 `http://<서버IP>:3000` 접속 후:
- 이메일: `admin@lars.local`
- 비밀번호: `admin1234`

로그인 성공 시 `/dashboard`로 이동한다.

---

## AI 모드 전환 방법

`backend/.env` 파일의 `AI_MODE` 값을 변경하고 백엔드를 재시작한다.

| 모드 | 설명 | 필요 조건 |
|---|---|---|
| `disabled` | AI 기능 전체 비활성화 | 없음 |
| `local` | 동일 머신 Ollama 직접 호출 | Ollama 설치 + 모델 다운로드 |
| `internal` | 내부망 LARS AI Service 호출 | AI PC에서 lars_ai_service 실행 중 |
| `cloud` | 외부 Cloud API(Google/OpenAI 등) | CLOUD_LLM_API_KEY 설정 |

현재 운영 모드: **`internal`** (AI_SERVICE_URL=http://192.168.0.100:8088)

Admin 페이지(`/admin`) → AI 서비스 설정 탭 → "연결 테스트" 버튼으로 연결 상태 확인 가능.

---

## 전체 시동 순서 요약

```bash
# [터미널 1] 인프라
cd /test/LARS && docker compose up -d

# [터미널 2] 백엔드
cd /test/LARS/backend && source venv/bin/activate && uvicorn main:app --host 0.0.0.0 --port 8000

# [터미널 3] 프론트엔드
cd /test/LARS/.WebUI && npm run dev

# [AI PC] AI Service (AI_MODE=internal인 경우만)
cd /path/to/lars_ai_service && uvicorn main:app --host 0.0.0.0 --port 8088
```

---

## 종료 방법

```bash
# 프론트엔드 / 백엔드: 실행 중인 터미널에서 Ctrl+C

# Docker 컨테이너 중지
cd /test/LARS && docker compose stop

# Docker 컨테이너 + 볼륨 전체 삭제 (DB 데이터도 삭제됨 — 주의)
docker compose down -v
```

---

## 트러블슈팅

### DB 연결 실패
```
[LARS] DB 연결 실패: ...
```
- Docker가 실행 중인지 확인: `docker compose ps`
- `backend/.env`의 `DATABASE_URL`에서 IP와 포트(5433) 확인
- 컨테이너 기동 후 약 5초 대기 후 재시도

### Alembic 마이그레이션 실패
```
FAILED: Target database is not up to date.
```
- PostgreSQL이 정상 기동 상태인지 확인
- `alembic current`로 현재 적용 버전 확인 후 `alembic upgrade head` 재실행

### 프론트엔드 API 통신 실패 (Network Error)
- 백엔드 서버(`:8000`)가 실행 중인지 확인
- `curl http://localhost:8000/health` 응답 확인
- `vite.config.ts`의 proxy 설정이 `target: 'http://localhost:8000'`으로 되어 있는지 확인

### AI 기능 503 오류
- `backend/.env`의 `AI_MODE` 확인
- `AI_MODE=internal`인 경우 AI PC(192.168.0.100:8088) 실행 상태 확인
- `AI_MODE=disabled`로 변경 후 백엔드 재시작하면 AI 없이 운영 가능
