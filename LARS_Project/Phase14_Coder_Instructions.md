# Phase 14 Coder Instructions — 사이드바 접기/펼치기 + 유저 관리 강화

**Role:** Coder (Gemini)  
**Date:** 2026-05-16  
**Priority:** High

---

## Task 14-A: 사이드바 아이콘 전용 모드 (접기/펼치기)

### 목표

- 펼침(기본): 현재 w-64, 아이콘 + 텍스트
- 접힘: w-16, 아이콘만, hover 시 tooltip으로 메뉴명 표시
- 상태는 `localStorage('sidebar_collapsed')` 에 저장 → 새로고침 후에도 유지
- 전환 버튼: 사이드바 상단 헤더 우측에 `◀ / ▶` 아이콘 버튼

### 14-A-1. AppLayout.tsx 수정

```tsx
import { useState } from 'react';
import { ChevronLeft, ChevronRight, PanelLeftClose, PanelLeft } from 'lucide-react';

export function AppLayout() {
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    return localStorage.getItem('sidebar_collapsed') === 'true';
  });

  const toggleSidebar = () => {
    const next = !collapsed;
    setCollapsed(next);
    localStorage.setItem('sidebar_collapsed', String(next));
  };

  // ...

  return (
    <div className="flex h-screen bg-gray-50">
      {/* Sidebar */}
      <aside className={`hidden md:flex flex-col bg-gray-900 text-white transition-all duration-200 ${
        collapsed ? 'w-16' : 'w-64'
      }`}>
        
        {/* 사이드바 헤더 */}
        <div className={`flex items-center border-b border-gray-800 shrink-0 ${
          collapsed ? 'justify-center p-3' : 'justify-between p-4'
        }`}>
          {!collapsed && <span className="text-xl font-bold truncate">LARS Platform</span>}
          <button
            onClick={toggleSidebar}
            className="p-1.5 rounded hover:bg-gray-700 text-gray-400 hover:text-white transition-colors shrink-0"
            title={collapsed ? '메뉴 펼치기' : '메뉴 접기'}
          >
            {collapsed ? <PanelLeft size={18} /> : <PanelLeftClose size={18} />}
          </button>
        </div>

        {/* 네비게이션 */}
        <nav className="flex-1 p-2 space-y-1 overflow-y-auto overflow-x-hidden">
          {/* 각 링크를 SidebarLink 컴포넌트로 교체 (아래 14-A-2 참조) */}
        </nav>

        <BackgroundMonitor />
      </aside>
      {/* ... 기존 메인 콘텐츠 유지 */}
    </div>
  );
}
```

### 14-A-2. SidebarLink 컴포넌트 생성

파일: `.WebUI/src/components/SidebarLink.tsx`

```tsx
import { Link, useLocation } from 'react-router-dom';
import { LucideIcon } from 'lucide-react';

interface Props {
  to: string;
  icon: LucideIcon;
  label: string;
  collapsed: boolean;
  indent?: boolean; // AutoReport 하위 메뉴
}

export function SidebarLink({ to, icon: Icon, label, collapsed, indent = false }: Props) {
  const { pathname } = useLocation();
  const isActive = pathname === to || pathname.startsWith(to + '/');

  return (
    <Link
      to={to}
      title={collapsed ? label : undefined}
      className={`flex items-center rounded transition-colors group relative ${
        collapsed
          ? 'justify-center p-2.5'
          : `p-2 space-x-2 ${indent ? 'pl-8' : ''}`
      } ${
        isActive
          ? 'bg-blue-600 text-white'
          : 'text-gray-400 hover:bg-gray-800 hover:text-white'
      }`}
    >
      <Icon size={18} className="shrink-0" />
      {!collapsed && <span className="text-sm truncate">{label}</span>}
      
      {/* Tooltip (접힘 모드) */}
      {collapsed && (
        <div className="absolute left-full ml-2 px-2 py-1 bg-gray-800 text-white text-xs rounded 
                        whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity z-50 pointer-events-none">
          {label}
        </div>
      )}
    </Link>
  );
}
```

### 14-A-3. AutoReport 아코디언 처리 (접힘 모드)

접힘 모드에서는 AutoReport 하위 메뉴를 토글 없이 전부 아이콘으로 표시.

```tsx
{/* AutoReport 섹션 */}
{!collapsed ? (
  /* 기존 아코디언 토글 버튼 + 하위 메뉴 */
  <div>
    <button onClick={toggleAutoReport} ...>
      <FileText size={18} /><span>AutoReport</span>
      {isAutoReportOpen ? <ChevronDown /> : <ChevronRight />}
    </button>
    {isAutoReportOpen && (
      <div className="pl-2 space-y-1">
        <SidebarLink to="/bom"   icon={FileText}     label="BOM"         collapsed={false} indent />
        <SidebarLink to="/dp"    icon={ClipboardList} label="Daily Plan"  collapsed={false} indent />
        <SidebarLink to="/pl"    icon={List}         label="Part List"   collapsed={false} indent />
        <SidebarLink to="/items" icon={Package}       label="Item Master" collapsed={false} indent />
        <SidebarLink to="/psi"   icon={RefreshCw}    label="PSI"         collapsed={false} indent />
      </div>
    )}
  </div>
) : (
  /* 접힘 모드: 구분선 + 아이콘만 표시 */
  <div className="space-y-1">
    <div className="border-t border-gray-700 my-1" />
    <SidebarLink to="/bom"   icon={FileText}     label="BOM"         collapsed={true} />
    <SidebarLink to="/dp"    icon={ClipboardList} label="Daily Plan"  collapsed={true} />
    <SidebarLink to="/pl"    icon={List}         label="Part List"   collapsed={true} />
    <SidebarLink to="/items" icon={Package}       label="Item Master" collapsed={true} />
    <SidebarLink to="/psi"   icon={RefreshCw}    label="PSI"         collapsed={true} />
    <div className="border-t border-gray-700 my-1" />
  </div>
)}
```

나머지 메뉴들도 `SidebarLink` 컴포넌트로 교체하여 collapsed 상태에 맞게 렌더링.

---

## Task 14-B: User 모델 확장

### 14-B-1. DB 모델 수정 (`backend/models/user.py`)

```python
class User(SQLModel, table=True):
    __tablename__ = "users"

    id: Optional[int] = Field(default=None, primary_key=True)
    email: str = Field(unique=True, index=True)
    display_name: str
    role: str = Field(default="viewer")
    is_active: bool = Field(default=True)
    hashed_pw: str

    # 신규 프로필 필드
    phone: Optional[str] = Field(default=None)          # 전화번호
    company: Optional[str] = Field(default=None)        # 소속사
    department: Optional[str] = Field(default=None)     # 부서
    rank: Optional[str] = Field(default=None)           # 직급 (대리, 과장, 차장 등)
    position: Optional[str] = Field(default=None)       # 직책 (팀장, 파트장 등)

    created_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
    updated_at: Optional[datetime] = Field(default_factory=datetime.utcnow)
```

### 14-B-2. Alembic 마이그레이션

```bash
cd /test/LARS/backend
source venv/bin/activate
alembic revision --autogenerate -m "user_profile_fields"
alembic upgrade head
```

생성된 migration 파일 확인: `phone`, `company`, `department`, `rank`, `position` VARCHAR 컬럼 추가 (nullable, no default).

---

## Task 14-C: 백엔드 API 확장

### 14-C-1. 공통 스키마 (`backend/schemas/user.py` 생성)

```python
from pydantic import BaseModel
from typing import Optional
from datetime import datetime

class UserProfileRead(BaseModel):
    id: int
    email: str
    display_name: str
    role: str
    is_active: bool
    phone: Optional[str] = None
    company: Optional[str] = None
    department: Optional[str] = None
    rank: Optional[str] = None
    position: Optional[str] = None
    created_at: Optional[datetime] = None

class UserProfileUpdate(BaseModel):
    display_name: Optional[str] = None
    phone: Optional[str] = None
    company: Optional[str] = None
    department: Optional[str] = None
    rank: Optional[str] = None
    position: Optional[str] = None

class UserAdminUpdate(BaseModel):
    role: Optional[str] = None
    is_active: Optional[bool] = None
    display_name: Optional[str] = None
    phone: Optional[str] = None
    company: Optional[str] = None
    department: Optional[str] = None
    rank: Optional[str] = None
    position: Optional[str] = None

class UserCreate(BaseModel):
    email: str
    display_name: str
    role: str = "viewer"
    password: str
    phone: Optional[str] = None
    company: Optional[str] = None
    department: Optional[str] = None
    rank: Optional[str] = None
    position: Optional[str] = None

class PasswordChange(BaseModel):
    current_password: str
    new_password: str
```

### 14-C-2. 내 프로필 API (`backend/api/routes/auth.py` 또는 신규 `users.py`)

신규 라우터: `backend/api/routes/users.py`

```python
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from core.database import get_session
from core.deps import get_current_user
from core.security import hash_password, verify_password
from models.user import User
from schemas.user import UserProfileRead, UserProfileUpdate, PasswordChange

router = APIRouter(prefix="/users", tags=["users"])

@router.get("/me", response_model=UserProfileRead)
async def get_my_profile(current_user: User = Depends(get_current_user)):
    return current_user

@router.put("/me", response_model=UserProfileRead)
async def update_my_profile(
    data: UserProfileUpdate,
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    for field, value in data.model_dump(exclude_none=True).items():
        setattr(current_user, field, value)
    from datetime import datetime
    current_user.updated_at = datetime.utcnow()
    session.add(current_user)
    await session.commit()
    await session.refresh(current_user)
    return current_user

@router.put("/me/password")
async def change_my_password(
    data: PasswordChange,
    current_user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session)
):
    if not verify_password(data.current_password, current_user.hashed_pw):
        raise HTTPException(status_code=400, detail="현재 비밀번호가 올바르지 않습니다")
    current_user.hashed_pw = hash_password(data.new_password)
    session.add(current_user)
    await session.commit()
    return {"message": "비밀번호가 변경되었습니다"}
```

`main.py`에 라우터 등록:
```python
from api.routes import users as users_router
app.include_router(users_router.router, prefix="/api/v1")
```

### 14-C-3. Admin API 확장 (`backend/api/routes/admin.py`)

기존 `UserCreate`, `UserUpdate` Pydantic 모델을 `schemas/user.py`의 것으로 교체.

기존 `GET /admin/users` 응답에 신규 필드 포함:
```python
return [{
    "id": u.id, "email": u.email, "display_name": u.display_name,
    "role": u.role, "is_active": u.is_active,
    "phone": u.phone, "company": u.company, "department": u.department,
    "rank": u.rank, "position": u.position,
    "created_at": u.created_at.isoformat() if u.created_at else None
} for u in users]
```

기존 `PUT /admin/users/{user_id}` 에 신규 필드 업데이트 추가:
```python
@router.put("/users/{user_id}")
async def update_user(user_id: int, data: UserAdminUpdate, ...):
    for field, value in data.model_dump(exclude_none=True).items():
        setattr(user, field, value)
    user.updated_at = datetime.utcnow()
    ...
```

Admin 비밀번호 초기화 엔드포인트 추가:
```python
@router.post("/users/{user_id}/reset-password")
async def admin_reset_password(
    user_id: int,
    new_password: str,
    session: AsyncSession = Depends(get_session)
):
    # user 조회 후 hashed_pw 교체
    ...
    return {"message": "비밀번호가 초기화되었습니다"}
```

---

## Task 14-D: 프론트엔드 — 내 프로필 페이지

### 14-D-1. 헤더 내 프로필 버튼

`AppLayout.tsx` 상단 헤더의 `{user?.display_name} ({user?.role})` 텍스트를 클릭 가능한 버튼으로 변경:

```tsx
<button
  onClick={() => navigate('/profile')}
  className="flex items-center gap-2 text-sm text-gray-600 hover:text-blue-600 transition-colors"
>
  <div className="w-8 h-8 rounded-full bg-blue-100 text-blue-700 flex items-center justify-center font-bold text-sm">
    {user?.display_name?.[0]?.toUpperCase() ?? 'U'}
  </div>
  <div className="hidden md:flex flex-col items-start">
    <span className="font-medium text-gray-800 text-sm">{user?.display_name}</span>
    <span className="text-[10px] text-gray-400">{user?.role}</span>
  </div>
</button>
```

### 14-D-2. ProfilePage 생성

파일: `.WebUI/src/pages/ProfilePage.tsx`

```tsx
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { User, Lock, Phone, Building2, Briefcase, BadgeCheck } from 'lucide-react';

export default function ProfilePage() {
  const queryClient = useQueryClient();
  const [passwordForm, setPasswordForm] = useState({ current_password: '', new_password: '', confirm: '' });
  const [pwError, setPwError] = useState('');

  const { data: profile, isLoading } = useQuery({
    queryKey: ['my-profile'],
    queryFn: async () => (await apiClient.get('/users/me')).data,
  });

  const [form, setForm] = useState<any>({});

  // profile 로드 후 form 초기화
  // useEffect: setForm(profile) when profile loads

  const updateMutation = useMutation({
    mutationFn: async (data: any) => apiClient.put('/users/me', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-profile'] });
      alert('프로필이 저장되었습니다');
    },
  });

  const passwordMutation = useMutation({
    mutationFn: async () => apiClient.put('/users/me/password', {
      current_password: passwordForm.current_password,
      new_password: passwordForm.new_password,
    }),
    onSuccess: () => {
      setPasswordForm({ current_password: '', new_password: '', confirm: '' });
      alert('비밀번호가 변경되었습니다');
    },
    onError: (e: any) => setPwError(e.response?.data?.detail ?? '오류'),
  });

  const handlePasswordSubmit = () => {
    if (passwordForm.new_password !== passwordForm.confirm) {
      setPwError('새 비밀번호가 일치하지 않습니다');
      return;
    }
    if (passwordForm.new_password.length < 6) {
      setPwError('비밀번호는 6자 이상이어야 합니다');
      return;
    }
    setPwError('');
    passwordMutation.mutate();
  };

  if (isLoading) return <div className="p-8 text-center">로딩 중...</div>;

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <h1 className="text-2xl font-bold flex items-center gap-2">
        <User size={24} className="text-blue-600" /> 내 프로필
      </h1>

      {/* 기본 정보 */}
      <div className="bg-white rounded-xl shadow-sm border p-6 space-y-4">
        <h2 className="font-bold text-gray-700 border-b pb-2">기본 정보</h2>

        <div className="grid grid-cols-2 gap-4">
          <Field label="이메일" icon={<User size={14} />} value={profile?.email} readOnly />
          <Field label="권한" icon={<BadgeCheck size={14} />} value={profile?.role} readOnly />
        </div>

        {/* 편집 가능 필드 */}
        {[
          { label: '이름', field: 'display_name', icon: <User size={14} />, placeholder: '이름을 입력하세요' },
          { label: '전화번호', field: 'phone', icon: <Phone size={14} />, placeholder: '010-0000-0000' },
          { label: '소속사', field: 'company', icon: <Building2 size={14} />, placeholder: '소속 회사명' },
          { label: '부서', field: 'department', icon: <Building2 size={14} />, placeholder: '부서명' },
          { label: '직급', field: 'rank', icon: <Briefcase size={14} />, placeholder: '대리 / 과장 / 차장 ...' },
          { label: '직책', field: 'position', icon: <Briefcase size={14} />, placeholder: '팀장 / 파트장 ...' },
        ].map(({ label, field, icon, placeholder }) => (
          <div key={field}>
            <label className="text-xs font-semibold text-gray-500 flex items-center gap-1 mb-1">
              {icon} {label}
            </label>
            <input
              type="text"
              value={form[field] ?? profile?.[field] ?? ''}
              onChange={e => setForm((prev: any) => ({ ...prev, [field]: e.target.value }))}
              placeholder={placeholder}
              className="w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
            />
          </div>
        ))}

        <button
          onClick={() => updateMutation.mutate(form)}
          disabled={updateMutation.isPending}
          className="w-full py-2 bg-blue-600 text-white rounded-lg font-semibold hover:bg-blue-700 disabled:opacity-50"
        >
          {updateMutation.isPending ? '저장 중...' : '저장'}
        </button>
      </div>

      {/* 비밀번호 변경 */}
      <div className="bg-white rounded-xl shadow-sm border p-6 space-y-4">
        <h2 className="font-bold text-gray-700 border-b pb-2 flex items-center gap-2">
          <Lock size={16} /> 비밀번호 변경
        </h2>
        {[
          { label: '현재 비밀번호', field: 'current_password', placeholder: '현재 비밀번호' },
          { label: '새 비밀번호', field: 'new_password', placeholder: '6자 이상' },
          { label: '새 비밀번호 확인', field: 'confirm', placeholder: '동일하게 입력' },
        ].map(({ label, field, placeholder }) => (
          <div key={field}>
            <label className="text-xs font-semibold text-gray-500 mb-1 block">{label}</label>
            <input
              type="password"
              value={(passwordForm as any)[field]}
              onChange={e => setPasswordForm(prev => ({ ...prev, [field]: e.target.value }))}
              placeholder={placeholder}
              className="w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
            />
          </div>
        ))}
        {pwError && <p className="text-sm text-red-500">{pwError}</p>}
        <button
          onClick={handlePasswordSubmit}
          disabled={passwordMutation.isPending}
          className="w-full py-2 bg-gray-700 text-white rounded-lg font-semibold hover:bg-gray-800 disabled:opacity-50"
        >
          {passwordMutation.isPending ? '변경 중...' : '비밀번호 변경'}
        </button>
      </div>
    </div>
  );
}

// 읽기 전용 필드 컴포넌트
function Field({ label, icon, value, readOnly }: { label: string; icon: React.ReactNode; value?: string; readOnly?: boolean }) {
  return (
    <div>
      <label className="text-xs font-semibold text-gray-500 flex items-center gap-1 mb-1">{icon} {label}</label>
      <div className="px-3 py-2 bg-gray-50 border border-gray-200 rounded-md text-sm text-gray-700">{value || '-'}</div>
    </div>
  );
}
```

### 14-D-3. 라우트 등록 (`App.tsx`)

```tsx
import ProfilePage from './pages/ProfilePage';
// ...
<Route path="/profile" element={<ProfilePage />} />
```

### 14-D-4. SidebarLink 추가: `/profile`

사이드바에 프로필 링크는 추가하지 않음 (헤더 아바타 버튼으로 접근). 사이드바에는 `/admin` 만 유지.

---

## Task 14-E: 프론트엔드 — AdminPage 강화

기존 AdminPage를 탭 구조로 개편:

```
[사용자 목록] [사용자 생성]
```

### 사용자 목록 탭

| 컬럼 | 표시 |
|------|------|
| 이름 | display_name |
| 이메일 | email |
| 소속 | company / department |
| 직급/직책 | rank / position |
| 권한 | role (배지) |
| 상태 | is_active (토글) |
| 작업 | [편집] [비번초기화] |

**인라인 편집** (편집 버튼 클릭 시 해당 행이 입력 폼으로 전환):
- `role` 드롭다운: viewer / internal / manager / admin
- `is_active` 체크박스
- 신규 필드: phone, company, department, rank, position
- [저장] [취소] 버튼

**비밀번호 초기화** (비번초기화 버튼 클릭 시):
- 새 비밀번호 입력 모달 (간단한 `prompt()` 또는 인라인 폼)
- `POST /admin/users/{id}/reset-password?new_password=...` 호출

### 사용자 생성 탭

기존 form에 신규 필드 추가:
- phone, company, department, rank, position 입력 필드
- 모두 optional (빈 칸 가능)

---

## 검증 체크리스트

### 14-A (사이드바)
- [ ] 펼침 ↔ 접힘 토글 버튼 동작
- [ ] 접힘 시 w-16, 아이콘만 표시
- [ ] 접힘 시 hover하면 tooltip 표시
- [ ] 새로고침 후에도 상태 유지 (localStorage)
- [ ] AutoReport 하위 메뉴가 접힘 모드에서 아이콘으로 표시
- [ ] 현재 활성 라우트 강조 (active state)
- [ ] 모바일 하단 탭바는 변경 없음

### 14-B, 14-C (백엔드)
- [ ] `alembic upgrade head` 오류 없음
- [ ] `GET /api/v1/users/me` — 프로필 반환
- [ ] `PUT /api/v1/users/me` — phone, company 등 업데이트
- [ ] `PUT /api/v1/users/me/password` — 현재 비번 검증 후 변경
- [ ] `GET /api/v1/admin/users` — 신규 필드 포함 반환
- [ ] `PUT /api/v1/admin/users/{id}` — 신규 필드 업데이트
- [ ] `POST /api/v1/admin/users/{id}/reset-password` — 동작

### 14-D, 14-E (프론트엔드)
- [ ] 헤더 아바타 버튼 → `/profile` 페이지 이동
- [ ] ProfilePage: 프로필 편집 및 저장
- [ ] ProfilePage: 비밀번호 변경 (현재 비번 틀리면 에러)
- [ ] AdminPage: 사용자 목록에 신규 필드 표시
- [ ] AdminPage: 인라인 편집 동작
- [ ] AdminPage: 사용자 생성 form에 신규 필드 포함
- [ ] `npm run build` TypeScript 오류 0건

---

## 완료 후

- `Phase14_Coder_Report.md` 작성
- `alembic upgrade head` 적용 확인
- `npm run build` + 백엔드/프론트엔드 재시작
- Git commit: `"Phase 14: sidebar collapse + user profile management"`
