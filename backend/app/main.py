"""FastAPI 入口：建表 + 种子数据 + 路由注册。"""
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from .config import settings
from .database import Base, engine, migrate_schema
from .routers import admin, assessment, auth, common, forum
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


@app.get("/api/health", tags=["common"])
def health():
    return {"status": "ok", "app": settings.APP_NAME}
