"""请求 / 响应 Schema。"""
from datetime import datetime

from pydantic import BaseModel, Field


class RegisterIn(BaseModel):
    username: str = Field(min_length=2, max_length=32)
    password: str = Field(min_length=6, max_length=64)


class LoginIn(BaseModel):
    username: str
    password: str


class EmailCodeIn(BaseModel):
    email: str = Field(min_length=3, max_length=254)


class EmailLoginIn(BaseModel):
    email: str = Field(min_length=3, max_length=254)
    code: str = Field(min_length=4, max_length=8)


class TokenOut(BaseModel):
    access_token: str
    token_type: str = "bearer"
    new_account: bool = False  # 邮箱验证码首次登录自动注册时为 True
    user: "UserOut"


class UserOut(BaseModel):
    id: int
    username: str
    email: str | None = None
    phone: str | None = None
    role: str
    status: str
    avatar: str = ""
    badge: str = ""
    created_at: datetime

    model_config = {"from_attributes": True}


class PhoneBindIn(BaseModel):
    phone: str = Field(min_length=11, max_length=11, pattern=r"^1[3-9]\d{9}$")


class ProfileUpdateIn(BaseModel):
    username: str = Field(min_length=2, max_length=32)
    avatar: str = Field(default="", max_length=255)


class PostIn(BaseModel):
    content: str = Field(min_length=1, max_length=2000)
    is_anonymous: bool = True
    board: str = Field(min_length=1, max_length=16)
    images: list[str] = Field(default=[], max_length=9)


class ReplyIn(BaseModel):
    content: str = Field(min_length=1, max_length=1000)
    is_anonymous: bool = True
    images: list[str] = Field(default=[], max_length=9)


class ReplyOut(BaseModel):
    id: int
    post_id: int
    content: str
    status: int
    is_ai: bool
    is_anonymous: bool
    author: str
    author_avatar: str = ""
    author_badge: str = ""
    crisis: bool
    recalled: bool = False
    recall_reason: str = ""
    like_count: int = 0
    liked: bool = False
    images: list[str] = []
    created_at: datetime


class PostOut(BaseModel):
    id: int
    content: str
    board: str = "mood"
    status: int
    crisis: bool
    is_anonymous: bool
    author: str
    author_avatar: str = ""
    author_badge: str = ""
    reply_count: int
    view_count: int = 0
    ai_replied: bool
    mine: bool = False
    like_count: int = 0
    liked: bool = False
    images: list[str] = []
    last_reply_at: datetime | None = None
    last_reply_author: str = ""
    created_at: datetime


class PostDetailOut(PostOut):
    replies: list[ReplyOut]


class AssessmentIn(BaseModel):
    assessment_type: str  # phq9 / gad7 / free
    answers: dict[str, int] = {}
    free_text: str = ""
    save_to_cloud: bool = False


class AssessmentOut(BaseModel):
    id: int | None = None
    assessment_type: str
    score: int | None = None
    level: str = ""
    level_code: str = ""
    crisis: bool = False
    ai_result: str
    saved_cloud: bool = False
    created_at: datetime | None = None


class ModerateIn(BaseModel):
    action: str  # approve / reject(remove)
    note: str = ""


class RecallIn(BaseModel):
    reason: str = Field(default="", max_length=255)  # 撤回原因（必填，留审计痕迹）


class LikeIn(BaseModel):
    target_type: str  # post / reply
    target_id: int


class StatusIn(BaseModel):
    status: str  # active / disabled


class HotlineOut(BaseModel):
    name: str
    phone: str
    tag: str
    description: str
    level: str


TokenOut.model_rebuild()
