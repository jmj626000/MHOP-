"""数据库模型，对应需求设计中的五张核心表。"""
from datetime import datetime

from sqlalchemy import (
    Boolean,
    Column,
    DateTime,
    ForeignKey,
    Index,
    Integer,
    String,
    Text,
    UniqueConstraint,
)
from sqlalchemy.orm import relationship

from .database import Base


def _now():
    # 全库时间约定：naive 本地时间，前端按本地时区解析
    return datetime.now()


class User(Base):
    __tablename__ = "users"

    id = Column(Integer, primary_key=True)
    username = Column(String(64), unique=True, nullable=False, index=True)
    password_hash = Column(String(255), nullable=False)
    email = Column(String(255), unique=True, nullable=True, index=True)  # 邮箱验证码登录；可空
    role = Column(String(16), default="user", nullable=False)  # admin / user
    status = Column(String(16), default="active", nullable=False)  # active / disabled
    avatar = Column(String(255), default="", nullable=False)  # 头像 URL 路径
    badge = Column(String(64), default="", nullable=False)  # 管理员设置的用户标识（如"认证咨询师""志愿者"等）
    created_at = Column(DateTime, default=_now)


class Post(Base):
    __tablename__ = "posts"

    id = Column(Integer, primary_key=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=True, index=True)  # NULL=纯匿名
    is_anonymous = Column(Boolean, default=True, nullable=False)
    content = Column(Text, nullable=False)
    board = Column(String(16), default="mood", nullable=False, index=True)  # 见 boards.py
    images = Column(Text, default="")  # JSON 数组，帖子附图 URL 列表
    # 0=待巡检 1=正常 2=违规(隐藏)
    status = Column(Integer, default=0, nullable=False, index=True)
    crisis = Column(Boolean, default=False, nullable=False)  # 内容含自伤/自杀信号
    view_count = Column(Integer, default=0, nullable=False)
    review_note = Column(String(255), default="")
    created_at = Column(DateTime, default=_now, index=True)

    replies = relationship("Reply", back_populates="post", cascade="all, delete-orphan")


class Reply(Base):
    __tablename__ = "replies"

    id = Column(Integer, primary_key=True)
    post_id = Column(Integer, ForeignKey("posts.id"), nullable=False, index=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=True)
    is_anonymous = Column(Boolean, default=True, nullable=False)
    content = Column(Text, nullable=False)
    images = Column(Text, default="")  # JSON 数组，回复附图 URL 列表
    # 0=待系统审核 1=审核通过 2=驳回
    status = Column(Integer, default=0, nullable=False, index=True)
    is_ai = Column(Boolean, default=False, nullable=False)
    crisis = Column(Boolean, default=False, nullable=False)
    review_note = Column(String(255), default="")
    # 管理员撤回（仅用于 AI 回复）：撤回后公开接口不返回正文，内容保留以备审计
    recalled = Column(Boolean, default=False, nullable=False, index=True)
    recall_reason = Column(String(255), default="")
    created_at = Column(DateTime, default=_now)

    post = relationship("Post", back_populates="replies")


class Like(Base):
    """点赞（帖子与回复统一，target_type: post / reply），仅登录用户。"""
    __tablename__ = "likes"
    __table_args__ = (
        UniqueConstraint("user_id", "target_type", "target_id", name="uq_like_user_target"),
        Index("ix_like_target", "target_type", "target_id"),
    )

    id = Column(Integer, primary_key=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=False, index=True)
    target_type = Column(String(8), nullable=False)  # post / reply
    target_id = Column(Integer, nullable=False)
    created_at = Column(DateTime, default=_now)


class Assessment(Base):
    __tablename__ = "assessments"

    id = Column(Integer, primary_key=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=True, index=True)
    assessment_type = Column(String(32), nullable=False)  # phq9 / gad7 / free
    input_data = Column(Text, default="")  # JSON 序列化的作答
    ai_result = Column(Text, default="")
    score = Column(Integer, nullable=True)
    level = Column(String(32), default="")
    created_at = Column(DateTime, default=_now)


class AiLog(Base):
    __tablename__ = "ai_logs"

    id = Column(Integer, primary_key=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=True, index=True)
    session_id = Column(String(64), default="", index=True)
    module = Column(String(32), nullable=False)  # forum / assessment
    prompt = Column(Text, default="")
    response = Column(Text, default="")
    engine = Column(String(32), default="local")  # llm / local
    created_at = Column(DateTime, default=_now)
