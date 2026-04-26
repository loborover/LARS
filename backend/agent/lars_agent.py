import json
import re
from sqlalchemy.ext.asyncio import AsyncSession
from llm.base import LLMProvider
from agent.tools import TOOL_SCHEMAS, execute_tool

LARS_SYSTEM_PROMPT = """당신은 LARS(Logistics Agent & Reporting System)의 AI 어시스턴트입니다.
물류, 자재 수급, BOM, 생산계획 관련 질문에 답변합니다.
답변은 항상 한국어로 합니다. 전문적이고 간결하게 답변하세요.

사용 가능한 데이터:
- BOM (자재명세서): 모델별 부품 구성
- DP (일일 생산계획): 라인별 생산 수량
- PSI (수급 현황): 부품 필요량/보유량/부족분
- IT (추적 품목): 관리 대상 부품 마스터
- Ticket: 업무 현안 티켓

Tool을 사용할 때는 다음 JSON 형식으로 응답하세요:
<tool_call>{"name": "tool_name", "arguments": {...}}</tool_call>

Tool 결과를 받으면 한국어로 자연스럽게 해석하여 사용자에게 전달하세요."""

async def run(
    user_message: str,
    session: AsyncSession,
    llm: LLMProvider,
    conversation_history: list[dict] | None = None
) -> str:
    """
    사용자 메시지를 받아 LLM + Tool 조합으로 응답 생성.
    단일 턴: Tool 1회 호출 후 최종 응답 반환 (multi-hop은 Phase 4).
    """
    messages = list(conversation_history or [])
    messages.append({"role": "user", "content": user_message})

    # 1차 LLM 호출 (Tool 사용 여부 판단)
    first_response = await llm.chat(
        messages=messages,
        system=LARS_SYSTEM_PROMPT,
        max_tokens=1024,
    )

    # Tool 호출 추출
    tool_pattern = re.compile(r"<tool_call>(.*?)</tool_call>", re.DOTALL)
    match = tool_pattern.search(first_response)

    if not match:
        return first_response

    # Tool 실행
    try:
        tool_data = json.loads(match.group(1).strip())
        tool_name = tool_data["name"]
        tool_args = tool_data.get("arguments", {})
    except (json.JSONDecodeError, KeyError):
        return first_response

    tool_result = await execute_tool(tool_name, tool_args, session)

    # 2차 LLM 호출 (Tool 결과 기반 최종 응답)
    messages.append({"role": "assistant", "content": first_response})
    messages.append({
        "role": "user",
        "content": f"[Tool '{tool_name}' 실행 결과]\n{tool_result}\n\n위 결과를 바탕으로 사용자에게 한국어로 답변해주세요."
    })

    final_response = await llm.chat(
        messages=messages,
        system=LARS_SYSTEM_PROMPT,
        max_tokens=1024,
    )

    # <tool_call> 태그 제거 후 최종 응답 반환
    return tool_pattern.sub("", final_response).strip()
