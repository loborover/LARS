from fastapi import APIRouter
from core.redis_client import get_redis
import json

router = APIRouter()

TASK_KEYS = [
    ("item_rebuild",       "itemmaster:rebuild_status",  "ItemMaster 재구성"),
    ("part_list_recompute","partlist:recompute_status",  "소요자재 재계산"),
    ("psi_recompute",      "psi:recompute_status",       "PSI 재계산"),
]

@router.get("/status")
async def get_background_status() -> list[dict]:
    redis = await get_redis()
    result = []
    for task_id, redis_key, label in TASK_KEYS:
        raw = await redis.get(redis_key)
        if raw:
            try:
                data = json.loads(raw)
                data["id"] = task_id
                data["label"] = label
            except Exception:
                data = {
                    "id": task_id,
                    "label": label,
                    "status": "idle",
                    "progress": 0,
                    "total": 0,
                    "processed": 0,
                    "started_at": None,
                    "finished_at": None,
                    "error": "Status parse error",
                }
        else:
            data = {
                "id": task_id,
                "label": label,
                "status": "idle",
                "progress": 0,
                "total": 0,
                "processed": 0,
                "started_at": None,
                "finished_at": None,
                "error": None,
            }
        result.append(data)
    return result
