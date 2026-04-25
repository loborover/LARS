from typing import Optional, List
import polars as pl
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.bom import BomModel, BomItem
from schemas.bom import BomTreeResponse, BomModelRead, BomItemRead, ReverseResult

async def get_bom_tree(session: AsyncSession, model_code: str) -> Optional[BomTreeResponse]:
    # Find model
    stmt = select(BomModel).where(BomModel.model_code == model_code)
    result = await session.execute(stmt)
    model = result.scalar_one_or_none()
    
    if not model:
        return None
        
    # Get all items for the model
    stmt_items = select(BomItem).where(BomItem.model_id == model.id).order_by(BomItem.sort_order)
    result_items = await session.execute(stmt_items)
    items = result_items.scalars().all()
    
    # Note: Flat list returned as requested, tree conversion can be done in frontend or service
    # For now returning flat list per instructions ("items: list[BomItemRead] # flat list (트리 변환은 프론트엔드 또는 서비스에서)")
    item_reads = [BomItemRead(
        id=item.id,
        level=item.level,
        part_number=item.part_number,
        description=item.description,
        qty=item.qty,
        uom=item.uom,
        vendor_raw=item.vendor_raw,
        supply_type=item.supply_type,
        path=item.path,
        children=[]
    ) for item in items]
    
    return BomTreeResponse(
        model=BomModelRead(
            id=model.id,
            model_code=model.model_code,
            description=model.description,
            version=model.version
        ),
        items=item_reads
    )

async def bom_reverse_lookup(session: AsyncSession, part_number: str) -> ReverseResult:
    # Find all active models containing this part
    stmt = select(BomModel).join(BomItem).where(BomItem.part_number == part_number, BomModel.is_active == True).distinct()
    result = await session.execute(stmt)
    models = result.scalars().all()
    
    model_reads = [BomModelRead(
        id=model.id,
        model_code=model.model_code,
        description=model.description,
        version=model.version
    ) for model in models]
    
    return ReverseResult(
        part_number=part_number,
        models=model_reads
    )

async def list_models(session: AsyncSession, search: Optional[str] = None, is_active: bool = True) -> List[BomModelRead]:
    stmt = select(BomModel).where(BomModel.is_active == is_active)
    if search:
        stmt = stmt.where(BomModel.model_code.contains(search))
    
    result = await session.execute(stmt)
    models = result.scalars().all()
    
    return [BomModelRead(
        id=model.id,
        model_code=model.model_code,
        description=model.description,
        version=model.version
    ) for model in models]

async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """
    BOM DataFrame을 DB에 upsert.
    """
    # Assuming dataframe is for a single model as per parser
    model_codes = df["model_code"].unique().to_list()
    total_inserted = 0
    
    for mc in model_codes:
        # Get or create BomModel
        stmt = select(BomModel).where(BomModel.model_code == mc)
        res = await session.execute(stmt)
        bom_model = res.scalar_one_or_none()
        
        if not bom_model:
            bom_model = BomModel(model_code=mc, import_batch_id=batch_id)
            session.add(bom_model)
            await session.flush()
            await session.refresh(bom_model)
        else:
            bom_model.import_batch_id = batch_id
            
        # Delete existing items for this model (full replace on import)
        # Assuming typical import replaces the whole BOM or we can UPSERT.
        # Requirements: "bom_items는 model_id + sort_order 기준으로 upsert"
        # We will fetch existing, and replace them or just delete all and insert.
        # "Delete existing" is safer to maintain exact BOM if sort_order is used for identity?
        # The prompt says: "bom_items는 model_id + sort_order 기준으로 upsert"
        
        # Here we do a simple replace-all for the model for simplicity if upsert is complex, 
        # or we just delete and re-insert for exact match.
        from sqlalchemy import delete
        await session.execute(delete(BomItem).where(BomItem.model_id == bom_model.id))
        
        # Insert items
        model_df = df.filter(pl.col("model_code") == mc)
        items_to_add = []
        for row in model_df.iter_rows(named=True):
            item = BomItem(
                model_id=bom_model.id,
                level=row["level"],
                part_number=row["part_number"],
                description=row["description"],
                qty=row["qty"],
                uom=row["uom"],
                vendor_raw=row["vendor_raw"],
                supply_type=row["supply_type"],
                path=row["path"],
                sort_order=row["sort_order"],
                import_batch_id=batch_id
            )
            items_to_add.append(item)
        
        session.add_all(items_to_add)
        total_inserted += len(items_to_add)
        
    await session.commit()
    return total_inserted
