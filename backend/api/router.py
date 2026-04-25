from fastapi import APIRouter
from api.routes import auth, bom, import_pipeline

router = APIRouter(prefix="/api/v1")
router.include_router(auth.router, prefix="/auth", tags=["auth"])
router.include_router(bom.router, prefix="/bom", tags=["bom"])
router.include_router(import_pipeline.router, prefix="/import", tags=["import"])
