from fastapi import APIRouter
from api.routes import auth, bom, import_pipeline, dp, pl, items, psi, efficiency, wip, dashboard, admin, ai, tickets
from api.routes.ws import router as ws_router

router = APIRouter(prefix="/api/v1")
router.include_router(auth.router, prefix="/auth", tags=["auth"])
router.include_router(bom.router, prefix="/bom", tags=["bom"])
router.include_router(import_pipeline.router, prefix="/import", tags=["import"])
router.include_router(dp.router, prefix="/dp", tags=["dp"])
router.include_router(pl.router, prefix="/pl", tags=["pl"])
router.include_router(items.router, prefix="/items", tags=["items"])
router.include_router(psi.router, prefix="/psi", tags=["psi"])
router.include_router(efficiency.router, prefix="/efficiency", tags=["efficiency"])
router.include_router(wip.router, prefix="/wip", tags=["wip"])
router.include_router(dashboard.router, prefix="/dashboard", tags=["dashboard"])
router.include_router(admin.router, prefix="/admin", tags=["admin"])
router.include_router(ai.router, tags=["ai"])
router.include_router(tickets.router, prefix="/tickets", tags=["tickets"])
