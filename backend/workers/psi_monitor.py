import json
from datetime import date
from sqlalchemy.ext.asyncio import AsyncSession
from core.database import get_session_context
from services import psi_service, ticket_service

async def check_psi_shortages(ws_manager=None):
    """
    PSI 부족 항목 체크 → 부족 항목 존재 시 Ticket 자동 생성 + WebSocket 브로드캐스트.
    APScheduler에서 15분마다 호출됨.
    """
    today = date.today()
    print(f"[PSI Monitor] 체크 시작: {today}")

    async with get_session_context() as session:
        shortages = await psi_service.get_shortage_summary(session, as_of_date=today)
        if not shortages:
            print("[PSI Monitor] 부족 항목 없음")
            return

        print(f"[PSI Monitor] {len(shortages)}건 부족 항목 감지")

        # 심각한 부족(shortage_qty < -10)만 Ticket 자동 생성
        critical = [s for s in shortages if s.get("shortage_qty", 0) < -10]
        for item in critical:
            title = f"[자동] {item['part_number']} 수급 부족 경보"
            desc = (
                f"품번: {item['part_number']}\n"
                f"품명: {item.get('description', '-')}\n"
                f"날짜: {item['psi_date']}\n"
                f"필요: {item['required_qty']}, 보유: {item.get('available_qty', 0)}, "
                f"부족: {item['shortage_qty']}"
            )
            await ticket_service.create_ticket(
                session,
                title=title,
                description=desc,
                priority="urgent",
                category="shortage",
                created_by_agent="psi-monitor",
            )

        # WebSocket 브로드캐스트
        if ws_manager:
            await ws_manager.broadcast({
                "type": "psi_shortage_alert",
                "shortage_count": len(shortages),
                "critical_count": len(critical),
                "checked_at": str(today),
            })
