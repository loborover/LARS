from typing import Optional, List
import polars as pl
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.bom import BomModel, BomItem
from schemas.bom import BomTreeResponse, BomModelRead, BomItemRead, ReverseResult

async def get_bom_tree(session: AsyncSession, model_number: str) -> Optional[BomTreeResponse]:
    # Find model (handle model_number: model_code.suffix)
    if "." in model_number:
        model_code, suffix = model_number.split(".", 1)
    else:
        model_code, suffix = model_number, ""
        
    stmt = select(BomModel).where(BomModel.model_code == model_code, BomModel.suffix == suffix)
    result = await session.execute(stmt)
    model = result.scalar_one_or_none()
    
    if not model:
        return None
        
    # Get all items for the model
    stmt_items = select(BomItem).where(BomItem.model_id == model.id).order_by(BomItem.sort_order)
    result_items = await session.execute(stmt_items)
    items = result_items.scalars().all()
    
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
            suffix=model.suffix,
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
        suffix=model.suffix,
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
        from sqlalchemy import or_
        stmt = stmt.where(or_(
            BomModel.model_code.contains(search),
            BomModel.suffix.contains(search)
        ))
    
    result = await session.execute(stmt)
    models = result.scalars().all()
    
    return [BomModelRead(
        id=model.id,
        model_code=model.model_code,
        suffix=model.suffix,
        description=model.description,
        version=model.version
    ) for model in models]

async def import_from_df(session: AsyncSession, df: pl.DataFrame, batch_id: int) -> int:
    """
    BOM DataFrame을 DB에 upsert.
    PK를 보존하기 위해 delete+insert 대신 개별 upsert 수행.
    """
    from sqlalchemy import delete
    
    # suffix 컬럼이 없으면 빈 문자열로 채움
    if "suffix" not in df.columns:
        df = df.with_columns(pl.lit("").alias("suffix"))
        
    model_keys = df.select(["model_code", "suffix"]).unique().to_dicts()
    total_upserted = 0

    for key in model_keys:
        mc = key["model_code"]
        sf = key["suffix"] or ""

        # BomModel upsert
        stmt = select(BomModel).where(BomModel.model_code == mc, BomModel.suffix == sf)
        res = await session.execute(stmt)
        bom_model = res.scalar_one_or_none()

        if not bom_model:
            bom_model = BomModel(model_code=mc, suffix=sf, import_batch_id=batch_id)
            session.add(bom_model)
            await session.flush()
            await session.refresh(bom_model)
        else:
            bom_model.import_batch_id = batch_id
            await session.flush()

        model_df = df.filter((pl.col("model_code") == mc) & (pl.col("suffix") == sf))

        # 기존 items를 {sort_order: BomItem} 딕셔너리로 인덱싱
        existing_stmt = select(BomItem).where(BomItem.model_id == bom_model.id)
        existing_res = await session.execute(existing_stmt)
        existing_items: dict[int, BomItem] = {
            item.sort_order: item for item in existing_res.scalars().all()
        }

        incoming_sort_orders = set()
        for row in model_df.iter_rows(named=True):
            so = row["sort_order"]
            incoming_sort_orders.add(so)
            if so in existing_items:
                # UPDATE: PK 유지
                item = existing_items[so]
                item.level = row["level"]
                item.part_number = row["part_number"]
                item.description = row["description"]
                item.qty = row["qty"]
                item.uom = row["uom"]
                item.vendor_raw = row["vendor_raw"]
                item.supply_type = row["supply_type"]
                item.path = row["path"]
                item.import_batch_id = batch_id
            else:
                # INSERT
                session.add(BomItem(
                    model_id=bom_model.id,
                    level=row["level"],
                    part_number=row["part_number"],
                    description=row["description"],
                    qty=row["qty"],
                    uom=row["uom"],
                    vendor_raw=row["vendor_raw"],
                    supply_type=row["supply_type"],
                    path=row["path"],
                    sort_order=so,
                    import_batch_id=batch_id,
                ))
            total_upserted += 1

        # 삭제된 rows 정리 (import에 없는 sort_order 제거)
        obsolete = set(existing_items.keys()) - incoming_sort_orders
        if obsolete:
            await session.execute(
                delete(BomItem).where(
                    BomItem.model_id == bom_model.id,
                    BomItem.sort_order.in_(list(obsolete))
                )
            )

    await session.commit()
    return total_upserted

async def get_bom_amount(session: AsyncSession, model_number: str) -> Optional["BomAmountResponse"]:
    """
    BOM의 계층적 소요량을 산출한다.
    각 item의 accumulated_qty = item.qty × parent.qty × grandparent.qty × ... (루트 제외)
    동일 part_number는 합산하여 반환.
    """
    from schemas.bom import BomAmountItem, BomAmountResponse, BomModelRead

    # 1. 모델 조회
    if "." in model_number:
        model_code, suffix = model_number.split(".", 1)
    else:
        model_code, suffix = model_number, ""

    stmt = select(BomModel).where(BomModel.model_code == model_code, BomModel.suffix == suffix)
    res = await session.execute(stmt)
    model = res.scalar_one_or_none()
    if not model:
        return None

    # 2. 전체 BomItem 조회 (sort_order 기준 정렬)
    stmt_items = select(BomItem).where(BomItem.model_id == model.id).order_by(BomItem.sort_order)
    res_items = await session.execute(stmt_items)
    items = res_items.scalars().all()

    # 3. path → qty 딕셔너리 구성 (level >= 0인 항목만, 대체품 level=-1 제외)
    path_to_qty: dict[str, float] = {}
    path_to_item: dict[str, BomItem] = {}
    for item in items:
        if item.level >= 0:
            path_to_qty[item.path] = item.qty
            path_to_item[item.path] = item

    # 4. 각 아이템의 accumulated_qty 계산 (level > 0만, 루트=0 및 대체품=-1 제외)
    # part_number → {total_qty, occurrence_count, metadata}
    aggregated: dict[str, dict] = {}

    for item in items:
        if item.level <= 0:
            continue  # 루트(0) 및 대체품(-1) 건너뜀

        path_parts = item.path.split('.')
        accumulated = item.qty

        # 조상 경로를 따라 올라가며 qty를 곱함
        # path_parts = ["0", "1", "2", "3"] 이면
        # 조상: "0.1" (i=2), "0.1.2" (i=3) → range(2, len(path_parts))
        # "0" (루트, i=1)은 제외 (qty=1이므로 곱해도 무방하나 명시적으로 제외)
        for i in range(2, len(path_parts)):
            ancestor_path = '.'.join(path_parts[:i])
            ancestor_qty = path_to_qty.get(ancestor_path, 1.0)
            accumulated *= ancestor_qty

        pn = item.part_number
        if pn not in aggregated:
            aggregated[pn] = {
                "total_qty": 0.0,
                "occurrence_count": 0,
                "description": item.description,
                "uom": item.uom,
                "vendor_raw": item.vendor_raw,
                "supply_type": item.supply_type,
            }
        aggregated[pn]["total_qty"] += accumulated
        aggregated[pn]["occurrence_count"] += 1

    # 5. 결과 정렬 (total_qty 내림차순)
    result_items = [
        BomAmountItem(
            part_number=pn,
            description=data["description"],
            uom=data["uom"],
            total_qty=round(data["total_qty"], 6),
            vendor_raw=data["vendor_raw"],
            supply_type=data["supply_type"],
            occurrence_count=data["occurrence_count"],
        )
        for pn, data in aggregated.items()
    ]
    result_items.sort(key=lambda x: x.total_qty, reverse=True)

    return BomAmountResponse(
        model=BomModelRead(
            id=model.id,
            model_code=model.model_code,
            suffix=model.suffix,
            description=model.description,
            version=model.version,
        ),
        items=result_items,
    )
