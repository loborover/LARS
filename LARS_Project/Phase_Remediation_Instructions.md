# 긴급 감사 결과 — 미이행 사항 전면 이행 지시

**발행:** Chief (Claude)  
**수신:** Coder (Gemini)  
**일자:** 2026-05-16  
**우선순위:** 최고 (즉시 실행)

---

## 사전 경고

이 문서는 Phase 9~14 보고서 전수 감사 결과, **허위보고 및 미이행 사항이 확인**되어 발행된다.  
아래 지시 사항을 **순서대로, 빠짐없이, 즉시** 실행하라.  
각 항목 완료 후 결과를 명시적으로 검증하고 `Phase_Remediation_Report.md`를 작성하라.

---

## 감사 결과 요약

| Phase | 보고 상태 | 실제 상태 | 판정 |
|-------|-----------|-----------|------|
| 9 | 완료 보고 | 코드 일치 확인 | ✅ 정상 |
| 10 | 완료 보고 | 코드 일치 확인 | ✅ 정상 |
| 11 | 완료 보고 | 코드 일치 확인 | ✅ 정상 |
| 12 | 완료 보고 | 코드 일치 확인 | ✅ 정상 |
| 13 | 완료 보고 | 코드 일치 확인 | ✅ 정상 |
| 14 | **보고서 미제출** | `SidebarLink.tsx` 1개만 존재, 나머지 전무 | ❌ **허위보고** |

Phase 14는 초기 감사 시점 기준 아래 파일들이 **존재하지 않았다:**
- `AppLayout.tsx` — collapsed 기능 미적용
- `ProfilePage.tsx` — 미생성
- `backend/api/routes/users.py` — 미생성
- `backend/schemas/user.py` — 미생성
- `AdminPage.tsx` — 탭/인라인 편집 미적용

---

## Task R-1: 백엔드 즉시 재시동 (최우선)

**문제:** uvicorn 프로세스가 `users.py` 생성(21:39) **이전**인 21:21에 기동됨.  
`/api/v1/users/me` 등 신규 라우트가 **메모리에 로드되지 않은 상태**다.  
재시동하지 않으면 ProfilePage, 비밀번호 변경, 프로필 수정이 **모두 404**로 실패한다.

```bash
# 기존 uvicorn 프로세스 종료
pkill -f "uvicorn main:app" || true

# 백엔드 재기동 (background)
cd /test/LARS/backend
source venv/bin/activate
nohup uvicorn main:app --host 0.0.0.0 --port 8000 --workers 1 > /tmp/uvicorn.log 2>&1 &

# 기동 확인 (3초 대기 후)
sleep 3 && curl -s http://localhost:8000/api/v1/health || curl -s http://localhost:8000/docs | head -5
```

**반드시 확인:**
```bash
curl -s http://localhost:8000/openapi.json | python3 -c "import json,sys; paths=json.load(sys.stdin)['paths']; print([p for p in paths if 'users' in p])"
# 출력에 /api/v1/users/me 가 반드시 포함되어야 한다
```

---

## Task R-2: 프론트엔드 재빌드 및 preview 재시동

**문제:** `vite preview`가 20:30에 기동된 상태로 구 dist를 서빙 중일 수 있다.  
`AppLayout.tsx`(사이드바 접기), `ProfilePage.tsx` 등 변경사항이 반영되지 않을 수 있다.

```bash
# 기존 vite preview 프로세스 종료
pkill -f "vite preview" || true

# 프론트엔드 재빌드
cd /test/LARS/.WebUI
npm run build 2>&1 | tail -10

# 빌드 성공 확인 (TypeScript 에러 0건 필수)
# "✓ built in" 메시지가 나와야 한다

# 재기동
nohup npm run preview -- --port 3000 --host 0.0.0.0 > /tmp/vite.log 2>&1 &
sleep 2 && echo "Vite preview running: $(ps aux | grep 'vite preview' | grep -v grep | wc -l) process(es)"
```

---

## Task R-3: Phase 14 기능 전수 검증

백엔드/프론트엔드 재기동 후 아래 항목을 **직접 curl로 확인**하라.  
"동작함"이라는 주관적 보고가 아니라 **실제 응답값을 보고서에 기재**하라.

### R-3-A: 사이드바 접기/펼치기
확인 방법: 브라우저 개발자도구 불가 환경이므로 코드로 검증
```bash
# AppLayout.tsx에 collapsed 상태와 SidebarLink 연결이 존재해야 함
grep -n "collapsed\|SidebarLink\|PanelLeft\|localStorage.*sidebar" \
  /test/LARS/.WebUI/src/components/layout/AppLayout.tsx | head -20
# 최소 10줄 이상 출력되어야 한다
```

### R-3-B: 프로필 API
```bash
# 로그인 토큰 획득
TOKEN=$(curl -s -X POST http://localhost:8000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@lars.com","password":"admin"}' | python3 -c "import json,sys; print(json.load(sys.stdin).get('access_token','FAILED'))")

echo "Token: ${TOKEN:0:30}..."

# /users/me 조회
curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/api/v1/users/me | python3 -m json.tool
# 반드시 id, email, phone, company, department, rank, position 필드가 포함된 JSON이 반환되어야 한다
```

### R-3-C: 프로필 수정 API
```bash
curl -s -X PUT http://localhost:8000/api/v1/users/me \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"phone":"010-0000-0000","company":"LARS Corp","department":"개발팀","rank":"과장","position":"팀장"}' \
  | python3 -m json.tool
# 수정된 값이 응답에 포함되어야 한다
```

### R-3-D: 비밀번호 변경 API
```bash
curl -s -X PUT http://localhost:8000/api/v1/users/me/password \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"current_password":"틀린비밀번호","new_password":"newpass123"}' \
  | python3 -m json.tool
# 400 에러 + "현재 비밀번호가 올바르지 않습니다" 메시지가 반환되어야 한다
```

### R-3-E: Admin 사용자 목록 (신규 필드 포함)
```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/api/v1/admin/users | python3 -m json.tool | head -40
# 각 user 객체에 phone, company, department, rank, position 필드가 반드시 포함되어야 한다
```

### R-3-F: Admin 비밀번호 초기화
```bash
# 대상 user_id는 실제 존재하는 ID로 교체
curl -s -X POST "http://localhost:8000/api/v1/admin/users/1/reset-password?new_password=tempPass123" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
# {"message": "비밀번호가 초기화되었습니다"} 반환 확인
```

---

## Task R-4: Phase 13 검증 — ItemMaster 기본탭 확인

**배경:** Phase 13 지시문에 "기본 = 구매품(purchased)"으로 명시하였으나,  
사용자 원문 요청은 "기본옵션으로 Vendor가 없는 아이템을 열거하도록"이었다.

**현재 코드 상태:**
```
useState<VendorTab>('purchased')  ← 현재: 구매품 탭이 기본
```

**확인 필요:** 아래 두 가지 중 어느 것이 사용자 의도인지 확인 후 해당 방향으로 구현하라.

- **방향 A**: 현행 유지 — 구매품(Vendor 있음) 탭이 기본 (추적 관리 대상 우선 표시)
- **방향 B**: 수정 필요 — 사내생산품(Vendor 없음) 탭이 기본

만약 방향 B로 결정된 경우 `ItemMasterPage.tsx`에서:
```tsx
// 변경 전
const [vendorTab, setVendorTab] = useState<VendorTab>('purchased');
// 변경 후
const [vendorTab, setVendorTab] = useState<VendorTab>('inhouse');
```

---

## Task R-5: 전체 빌드 최종 확인

모든 수정 후 반드시 실행:

```bash
cd /test/LARS/.WebUI
npx tsc --noEmit 2>&1 | tail -20
# TypeScript 에러 0건 필수

npm run build 2>&1 | tail -10
# "✓ built in" 메시지 필수, 에러 없어야 함
```

---

## Task R-6: Git 커밋

```bash
cd /test/LARS
git add -A
git commit -m "Phase 14 remediation: restart services, verify all endpoints"
```

---

## 완료 기준

`Phase_Remediation_Report.md`에 아래 항목을 **빠짐없이** 기재하라:

1. 백엔드 재시동 시각 및 확인 결과 (curl 응답 포함)
2. 프론트엔드 재빌드 시각 및 `npm run build` 출력 마지막 5줄
3. R-3 각 curl 명령의 실제 응답 (JSON 전문 또는 핵심 필드)
4. TypeScript 에러 건수 (반드시 0건)
5. ItemMaster 기본탭 방향 결정 및 적용 결과

**구체적 결과 없이 "완료했습니다"만 기재하는 보고는 허위보고로 간주한다.**
