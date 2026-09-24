"""FastAPI 入口：建表 + 种子数据 + 路由注册。"""
import os
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse

from .config import settings
from .database import Base, engine, migrate_schema
from .routers import admin, assessment, auth, common, forum, upload
from .seed import seed_data


@asynccontextmanager
async def lifespan(app: FastAPI):
    Base.metadata.create_all(bind=engine)
    migrate_schema()
    seed_data()
    yield


app = FastAPI(title=settings.APP_NAME, version="1.0.0", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(common.router)
app.include_router(auth.router)
app.include_router(forum.router)
app.include_router(assessment.router)
app.include_router(admin.router)
app.include_router(upload.router)


@app.get("/api/health", tags=["common"])
def health():
    return {"status": "ok", "app": settings.APP_NAME}


# ---- 生产环境：由后端直接托管前端构建产物（SPA），实现单容器部署 ----
# 必须在所有 API 路由之后注册兜底路由；静态目录不存在（本地 vite 开发）时自动跳过。
_static_dir = os.path.abspath(settings.STATIC_DIR)
_upload_dir = os.path.abspath(settings.UPLOAD_DIR)
if os.path.isdir(_static_dir):
    @app.get("/{full_path:path}", include_in_schema=False)
    def spa_entry(full_path: str):
        if full_path.startswith("api/"):
            raise HTTPException(status_code=404)
        # 上传文件（头像、帖子图片）
        if full_path.startswith("uploads/"):
            candidate = os.path.normpath(os.path.join(_upload_dir, full_path[8:]))
            if candidate.startswith(_upload_dir) and os.path.isfile(candidate):
                return FileResponse(candidate)
            raise HTTPException(status_code=404)
        # 真实静态文件（favicon、hashed assets 等，带文件指纹可放心缓存）
        candidate = os.path.normpath(os.path.join(_static_dir, full_path))
        if full_path and candidate.startswith(_static_dir) and os.path.isfile(candidate):
            if full_path.startswith("assets/"):
                return FileResponse(
                    candidate,
                    headers={"Cache-Control": "public, max-age=31536000, immutable"},
                )
            return FileResponse(candidate)
        # 其余路径一律回退 index.html，交给前端路由（/forum/123 等）。
        # index.html 禁止启发式缓存：否则新版本部署后老用户可能继续加载旧 chunk。
        return FileResponse(
            os.path.join(_static_dir, "index.html"),
            headers={"Cache-Control": "no-cache, must-revalidate"},
        )
