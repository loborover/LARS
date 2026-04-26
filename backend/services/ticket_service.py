from datetime import datetime
from typing import Optional
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from models.ticket import Ticket

async def create_ticket(
    session: AsyncSession,
    title: str,
    description: str = "",
    priority: str = "normal",
    category: Optional[str] = None,
    related_item_id: Optional[int] = None,
    related_model_id: Optional[int] = None,
    assigned_to: Optional[int] = None,
    created_by_agent: Optional[str] = None,
) -> Ticket:
    ticket = Ticket(
        title=title,
        description=description,
        priority=priority,
        status="open",
        category=category,
        related_item_id=related_item_id,
        related_model_id=related_model_id,
        assigned_to=assigned_to,
        created_by_agent=created_by_agent,
    )
    session.add(ticket)
    await session.commit()
    await session.refresh(ticket)
    return ticket

async def list_tickets(
    session: AsyncSession,
    status: Optional[str] = None,
    priority: Optional[str] = None,
    category: Optional[str] = None,
    limit: int = 50,
) -> list[Ticket]:
    stmt = select(Ticket).order_by(Ticket.created_at.desc()).limit(limit)
    if status:
        stmt = stmt.where(Ticket.status == status)
    if priority:
        stmt = stmt.where(Ticket.priority == priority)
    if category:
        stmt = stmt.where(Ticket.category == category)
    result = await session.execute(stmt)
    return result.scalars().all()

async def update_ticket(
    session: AsyncSession,
    ticket_id: int,
    status: Optional[str] = None,
    assigned_to: Optional[int] = None,
    description: Optional[str] = None,
) -> Optional[Ticket]:
    stmt = select(Ticket).where(Ticket.id == ticket_id)
    result = await session.execute(stmt)
    ticket = result.scalar_one_or_none()
    if not ticket:
        return None
    if status:
        ticket.status = status
        if status == "resolved":
            ticket.resolved_at = datetime.utcnow()
    if assigned_to is not None:
        ticket.assigned_to = assigned_to
    if description is not None:
        ticket.description = description
    ticket.updated_at = datetime.utcnow()
    session.add(ticket)
    await session.commit()
    await session.refresh(ticket)
    return ticket
