import httpx
import os
from fastapi import APIRouter, Request
from fastapi.responses import JSONResponse

router = APIRouter()
OLLAMA_URL = os.getenv("OLLAMA_URL", "http://localhost:11434")

@router.post("/chat/completions")
async def chat_completions(request: Request):
    """Ollama /v1/chat/completions 투명 프록시"""
    body = await request.json()
    async with httpx.AsyncClient(timeout=300.0) as client:
        resp = await client.post(
            f"{OLLAMA_URL}/v1/chat/completions",
            json=body,
        )
        return JSONResponse(content=resp.json(), status_code=resp.status_code)
