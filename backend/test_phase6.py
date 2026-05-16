import asyncio
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine
from datetime import date
from core.config import get_settings
from services.folder_import_service import scan_and_import_folder
from services.psi_service import advance_day, one_click_solution, recompute_all

# backend/.env에서 DB URL 가져오기
DB_URL = "postgresql+asyncpg://lars:lars_secret@172.17.0.1:5433/lars_db"

async def run_tests():
    engine = create_async_engine(DB_URL)
    async_session = async_sessionmaker(engine, expire_on_commit=False)
    settings = get_settings()

    async with async_session() as session:
        print("Testing BOM Folder Import...")
        bom_res = await scan_and_import_folder(session, settings.BOMDB_PATH, "bom", 1)
        print("BOM Import Result:", bom_res)
        
        print("\nTesting DP Folder Import...")
        dp_res = await scan_and_import_folder(session, settings.DPDB_PATH, "dp", 1)
        print("DP Import Result:", dp_res)

        print("\nTesting Advance Day...")
        adv_res = await advance_day(session, date.today())
        print("Advance Day Result:", adv_res)

        print("\nTesting One Click Solution...")
        one_res = await one_click_solution(session, 1)
        print("One Click Result:", one_res)

if __name__ == "__main__":
    asyncio.run(run_tests())
