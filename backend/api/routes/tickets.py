from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel
from sqlalchemy.ext.asyncio import AsyncSession
from typing import Optional
from core.database import get_session
from core.deps import get_current_user, require_role
from services import ticket_service

router = APIRouter(dependencies=[Depends(get_current_user)])

class TicketCreate(BaseModel):
    title: str
    description: str = ""
    priority: str = "normal"
    category: Optional[str] = None

class TicketUpdate(BaseModel):
    status: Optional[str] = None
    assigned_to: Optional[int] = None
    description: Optional[str] = None

@router.get("")
async def list_tickets(
    status: Optional[str] = Query(None),
    priority: Optional[str] = Query(None),
    session: AsyncSession = Depends(get_session)
):
    tickets = await ticket_service.list_tickets(session, status=status, priority=priority)
    return [
        {
            "id": t.id, "title": t.title, "description": t.description,
            "status": t.status, "priority": t.priority, "category": t.category,
            "created_by_agent": t.created_by_agent,
            "created_at": str(t.created_at), "updated_at": str(t.updated_at),
            "resolved_at": str(t.resolved_at) if t.resolved_at else None,
        }
        for t in tickets
    ]

@router.post("")
async def create_ticket(
    body: TicketCreate,
    session: AsyncSession = Depends(get_session)
):
    ticket = await ticket_service.create_ticket(
        session, title=body.title, description=body.description,
        priority=body.priority, category=body.category
    )
    return {"id": ticket.id, "title": ticket.title, "status": ticket.status}

@router.put("/{ticket_id}")
async def update_ticket(
    ticket_id: int,
    body: TicketUpdate,
    session: AsyncSession = Depends(get_session)
):
    ticket = await ticket_service.update_ticket(
        session, ticket_id=ticket_id,
        status=body.status, assigned_to=body.assigned_to, description=body.description
    )
    if not ticket:
        raise HTTPException(status_code=404, detail="Ticket not found")
    return {"id": ticket.id, "status": ticket.status}
