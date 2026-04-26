import json
from datetime import date, datetime
from typing import Optional
from sqlalchemy.ext.asyncio import AsyncSession
from services import psi_service, bom_service, ticket_service, daily_plan_service

# Tool 스키마 정의 (LLM에게 전달할 함수 명세)
TOOL_SCHEMAS = [
    {
        "name": "query_psi",
        "description": "PSI(수급 현황)에서 부족 항목을 조회합니다. 특정 날짜를 기준으로 shortage_qty < 0인 품목 목록을 반환합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "as_of_date": {
                    "type": "string",
                    "description": "조회 기준 날짜 (YYYY-MM-DD). 기본값은 오늘."
                }
            }
        }
    },
    {
        "name": "get_bom_tree",
        "description": "특정 모델의 BOM(자재명세서) 정보를 조회합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "model_code": {"type": "string", "description": "조회할 모델 코드"}
            },
            "required": ["model_code"]
        }
    },
    {
        "name": "get_dp_summary",
        "description": "특정 날짜의 일일 생산계획(DP) 요약을 조회합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "plan_date": {"type": "string", "description": "조회할 날짜 (YYYY-MM-DD)"}
            },
            "required": ["plan_date"]
        }
    },
    {
        "name": "create_ticket",
        "description": "업무 티켓을 생성합니다. 수급 부족, 긴급 자재 요청 등 현안 등록에 사용합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "title": {"type": "string", "description": "티켓 제목"},
                "description": {"type": "string", "description": "상세 내용"},
                "priority": {
                    "type": "string",
                    "enum": ["low", "normal", "high", "urgent"],
                    "description": "우선순위"
                },
                "category": {"type": "string", "description": "카테고리 (예: shortage, quality, logistics)"}
            },
            "required": ["title", "description"]
        }
    },
    {
        "name": "list_tickets",
        "description": "현재 열린 티켓 목록을 조회합니다.",
        "parameters": {
            "type": "object",
            "properties": {
                "status": {
                    "type": "string",
                    "enum": ["open", "in_progress", "resolved", "all"],
                    "description": "필터할 상태"
                }
            }
        }
    },
]

async def execute_tool(
    tool_name: str,
    tool_args: dict,
    session: AsyncSession
) -> str:
    """Tool 이름과 인자를 받아 실행하고 JSON 문자열로 결과 반환"""
    try:
        if tool_name == "query_psi":
            as_of_date = date.fromisoformat(tool_args.get("as_of_date", str(date.today())))
            result = await psi_service.get_shortage_summary(session, as_of_date)
            if not result:
                return json.dumps({"message": "부족 항목이 없습니다."}, ensure_ascii=False)
            return json.dumps([{
                "part_number": r["part_number"],
                "description": r["description"],
                "date": str(r["psi_date"]),
                "required_qty": r["required_qty"],
                "available_qty": r["available_qty"],
                "shortage_qty": r["shortage_qty"]
            } for r in result], ensure_ascii=False)

        elif tool_name == "get_bom_tree":
            model_code = tool_args["model_code"]
            tree = await bom_service.get_bom_tree(session, model_code)
            if not tree:
                return json.dumps({"error": f"모델 {model_code}을 찾을 수 없습니다."}, ensure_ascii=False)
            return json.dumps({
                "model_code": tree.model.model_code,
                "item_count": len(tree.items),
                "items": [{"level": i.level, "part_number": i.part_number, "description": i.description, "qty": i.qty} for i in tree.items[:20]]
            }, ensure_ascii=False)

        elif tool_name == "get_dp_summary":
            plan_date = date.fromisoformat(tool_args["plan_date"])
            plans = await daily_plan_service.list_plans(session, date_from=plan_date, date_to=plan_date)
            if not plans:
                return json.dumps({"message": f"{plan_date} 날짜의 생산계획이 없습니다."}, ensure_ascii=False)
            return json.dumps(plans, ensure_ascii=False, default=str)

        elif tool_name == "create_ticket":
            ticket = await ticket_service.create_ticket(
                session,
                title=tool_args["title"],
                description=tool_args.get("description", ""),
                priority=tool_args.get("priority", "normal"),
                category=tool_args.get("category"),
                created_by_agent="LARS-Agent"
            )
            return json.dumps({"created_ticket_id": ticket.id, "title": ticket.title}, ensure_ascii=False)

        elif tool_name == "list_tickets":
            status = tool_args.get("status", "open")
            tickets = await ticket_service.list_tickets(session, status=status if status != "all" else None)
            return json.dumps([{
                "id": t.id, "title": t.title, "status": t.status,
                "priority": t.priority, "created_at": str(t.created_at)
            } for t in tickets[:10]], ensure_ascii=False)

        else:
            return json.dumps({"error": f"알 수 없는 Tool: {tool_name}"})
    except Exception as e:
        return json.dumps({"error": str(e)}, ensure_ascii=False)
