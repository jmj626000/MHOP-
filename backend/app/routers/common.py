"""公共接口：热线信息、在线心跳。"""
from fastapi import APIRouter
from pydantic import BaseModel

from .. import online
from ..config import HOTLINES
from ..schemas import HotlineOut

router = APIRouter(prefix="/api", tags=["common"])


class HeartbeatIn(BaseModel):
    key: str  # 匿名访客ID（前端生成的随机ID，不含个人信息）


@router.get("/hotlines", response_model=list[HotlineOut])
def hotlines():
    return HOTLINES


@router.post("/online/heartbeat")
def heartbeat(body: HeartbeatIn):
    return {"online": online.heartbeat(body.key[:64] or "anon")}


@router.get("/online/count")
def count():
    return {"online": online.online_count()}
