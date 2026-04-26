from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from routes import llm, stt, tts

app = FastAPI(title="LARS AI Service")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(llm.router, prefix="/v1")
app.include_router(stt.router, prefix="/v1")
app.include_router(tts.router, prefix="/v1")

@app.get("/health")
async def health():
    return {"status": "ok", "service": "lars-ai"}
