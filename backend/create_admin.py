import asyncio
from sqlmodel import select
from core.database import async_session
from models.user import User
from core.security import hash_password

async def create_admin():
    async with async_session() as session:
        statement = select(User).where(User.email == "admin@lars.local")
        result = await session.execute(statement)
        user = result.scalar_one_or_none()
        
        if user:
            print("이미 존재합니다")
            return

        admin_user = User(
            email="admin@lars.local",
            display_name="Admin",
            role="admin",
            hashed_pw=hash_password("admin1234")
        )
        session.add(admin_user)
        await session.commit()
        print("Admin 사용자 생성 완료")

if __name__ == "__main__":
    asyncio.run(create_admin())
