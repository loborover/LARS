from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession
from typing import List, Dict, Any
from sqlmodel import select
from core.database import get_session
from core.deps import get_current_user
from models.bom import BomModel
from models.item_master import ItemMaster
from models.psi import PsiRecord
from models.import_batch import ImportBatch

router = APIRouter()

@router.get("/summary", dependencies=[Depends(get_current_user)])
async def get_summary(session: AsyncSession = Depends(get_session)) -> Dict[str, Any]:
    from sqlalchemy import func
    
    # 1. Total Bom Models
    stmt_bom = select(func.count(BomModel.id))
    res_bom = await session.execute(stmt_bom)
    total_bom_models = res_bom.scalar_one()
    
    # 2. Total IT Items
    stmt_it = select(func.count(ItemMaster.id))
    res_it = await session.execute(stmt_it)
    total_it_items = res_it.scalar_one()
    
    # 3. Shortage count
    stmt_shortage = select(PsiRecord).where(
        (func.coalesce(PsiRecord.available_qty, 0.0) - PsiRecord.required_qty) < 0
    )
    res_shortage = await session.execute(stmt_shortage)
    shortage_count = len(res_shortage.all())
    
    # 4. Recent imports
    stmt_imports = select(ImportBatch).order_by(ImportBatch.id.desc()).limit(5)
    res_imports = await session.execute(stmt_imports)
    recent = res_imports.scalars().all()
    recent_imports = [
        {
            "source_name": r.source_name,
            "target_table": r.target_table,
            "status": r.status,
            "finished_at": r.finished_at
        } for r in recent
    ]
    
    return {
        "total_bom_models": total_bom_models,
        "total_it_items": total_it_items,
        "shortage_count": shortage_count,
        "recent_imports": recent_imports
    }
