"""管理后台：数据看板、帖子巡检、回复审核、用户管理、AI 日志。"""
from datetime import datetime, timedelta

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from .. import online
from ..database import get_db
from ..deps import get_admin
from ..models import AiLog, Assessment, Post, Reply, User
from ..schemas import ModerateIn, RecallIn, StatusIn, UserOut
from ..security import hash_password

router = APIRouter(prefix="/api/admin", tags=["admin"], dependencies=[Depends(get_admin)])


def _iso(dt):
    return dt.isoformat() if dt else None


@router.get("/stats")
def stats(db: Session = Depends(get_db)):
    today = datetime.now() - timedelta(hours=24)

    def count(model, *where):
        stmt = select(func.count()).select_from(model)
        for w in where:
            stmt = stmt.where(w)
        return db.scalar(stmt) or 0

    return {
        "users": count(User),
        "posts": count(Post),
        "replies": count(Reply),
        "assessments": count(Assessment),
        "ai_logs": count(AiLog),
        "pending_posts": count(Post, Post.status == 0),
        "crisis_posts": count(Post, Post.crisis.is_(True), Post.status != 2),
        "pending_replies": count(Reply, Reply.status == 0, Reply.is_ai.is_(False)),
        "rejected_replies": count(Reply, Reply.status == 2),
        "new_posts_24h": count(Post, Post.created_at >= today),
        "new_users_24h": count(User, User.created_at >= today),
        "online": online.online_count(),
    }


@router.get("/posts")
def list_posts(status: int | None = None, db: Session = Depends(get_db)):
    stmt = select(Post).order_by(Post.created_at.desc())
    if status is not None:
        stmt = stmt.where(Post.status == status)
    rows = []
    for p in db.scalars(stmt.limit(200)).all():
        author = None
        if not p.is_anonymous and p.user_id:
            u = db.get(User, p.user_id)
            author = u.username if u else None
        rows.append(
            {
                "id": p.id,
                "content": p.content,
                "board": p.board,
                "status": p.status,
                "crisis": p.crisis,
                "is_anonymous": p.is_anonymous,
                "author": author,
                "review_note": p.review_note,
                "reply_count": len(p.replies),
                "created_at": _iso(p.created_at),
            }
        )
    return rows


@router.post("/posts/{post_id}/moderate")
def moderate_post(post_id: int, body: ModerateIn, db: Session = Depends(get_db)):
    post = db.get(Post, post_id)
    if not post:
        raise HTTPException(status_code=404, detail="帖子不存在")
    if body.action == "approve":
        post.status = 1
    elif body.action == "reject":
        post.status = 2
    else:
        raise HTTPException(status_code=400, detail="非法操作")
    post.review_note = body.note[:255]
    db.commit()
    return {"ok": True}


@router.get("/replies")
def list_replies(status: int | None = None, db: Session = Depends(get_db)):
    stmt = select(Reply).order_by(Reply.created_at.desc())
    if status is not None:
        stmt = stmt.where(Reply.status == status)
    rows = []
    for r in db.scalars(stmt.limit(300)).all():
        post = db.get(Post, r.post_id)
        author = None
        if not r.is_anonymous and r.user_id:
            u = db.get(User, r.user_id)
            author = u.username if u else None
        rows.append(
            {
                "id": r.id,
                "post_id": r.post_id,
                "post_excerpt": (post.content[:80] if post else "") + ("…" if post and len(post.content) > 80 else ""),
                "content": r.content,
                "status": r.status,
                "is_ai": r.is_ai,
                "crisis": r.crisis,
                "is_anonymous": r.is_anonymous,
                "author": author,
                "review_note": r.review_note,
                "recalled": bool(r.recalled),
                "recall_reason": r.recall_reason or "",
                "created_at": _iso(r.created_at),
            }
        )
    return rows


@router.post("/replies/{reply_id}/moderate")
def moderate_reply(reply_id: int, body: ModerateIn, db: Session = Depends(get_db)):
    reply = db.get(Reply, reply_id)
    if not reply:
        raise HTTPException(status_code=404, detail="回复不存在")
    if body.action == "approve":
        reply.status = 1
    elif body.action == "reject":
        reply.status = 2
    else:
        raise HTTPException(status_code=400, detail="非法操作")
    reply.review_note = body.note[:255]
    db.commit()
    return {"ok": True}


@router.post("/replies/{reply_id}/recall")
def recall_reply(reply_id: int, body: RecallIn, db: Session = Depends(get_db)):
    """撤回 AI 回复：对所有用户即时隐藏正文，保留内容与原因以备审计，可恢复。"""
    reply = db.get(Reply, reply_id)
    if not reply:
        raise HTTPException(status_code=404, detail="回复不存在")
    if not reply.is_ai:
        raise HTTPException(status_code=400, detail="仅支持撤回 AI 回复；人类回复请使用驳回")
    reason = body.reason.strip()
    if not reason:
        raise HTTPException(status_code=400, detail="请填写撤回原因")
    reply.recalled = True
    reply.recall_reason = reason[:255]
    db.commit()
    return {"ok": True}


@router.post("/replies/{reply_id}/restore")
def restore_reply(reply_id: int, db: Session = Depends(get_db)):
    """恢复被撤回的 AI 回复，重新公开展示。"""
    reply = db.get(Reply, reply_id)
    if not reply:
        raise HTTPException(status_code=404, detail="回复不存在")
    if not reply.is_ai:
        raise HTTPException(status_code=400, detail="仅支持恢复 AI 回复")
    reply.recalled = False
    reply.recall_reason = ""
    db.commit()
    return {"ok": True}


@router.get("/users", response_model=list[UserOut])
def list_users(db: Session = Depends(get_db)):
    return list(db.scalars(select(User).order_by(User.created_at.desc())).all())


@router.post("/users/{user_id}/status")
def set_user_status(user_id: int, body: StatusIn, admin: User = Depends(get_admin), db: Session = Depends(get_db)):
    if body.status not in ("active", "disabled"):
        raise HTTPException(status_code=400, detail="非法状态")
    user = db.get(User, user_id)
    if not user:
        raise HTTPException(status_code=404, detail="用户不存在")
    if user.id == admin.id and body.status == "disabled":
        raise HTTPException(status_code=400, detail="不能停用当前登录的管理员")
    user.status = body.status
    db.commit()
    return {"ok": True}


@router.post("/users/{user_id}/badge")
def set_user_badge(
    user_id: int,
    body: dict,
    admin: User = Depends(get_admin),
    db: Session = Depends(get_db),
):
    """设置用户标识。badge 为空字符串表示清除标识。"""
    user = db.get(User, user_id)
    if not user:
        raise HTTPException(status_code=404, detail="用户不存在")
    badge = (body.get("badge") or "").strip()[:64]
    user.badge = badge
    db.commit()
    return {"ok": True, "badge": badge}


@router.post("/users/{user_id}/role")
def set_user_role(
    user_id: int,
    body: dict,
    admin: User = Depends(get_admin),
    db: Session = Depends(get_db),
):
    """设置/取消管理员角色。action: promote / demote"""
    user = db.get(User, user_id)
    if not user:
        raise HTTPException(status_code=404, detail="用户不存在")
    action = body.get("action")
    if action == "promote":
        if user.role == "admin":
            raise HTTPException(status_code=400, detail="该用户已是管理员")
        user.role = "admin"
    elif action == "demote":
        if user.role != "admin":
            raise HTTPException(status_code=400, detail="该用户不是管理员")
        if user.id == admin.id:
            raise HTTPException(status_code=400, detail="不能取消自己的管理员权限")
        # 确保至少保留一个管理员
        admin_count = db.scalar(select(func.count()).select_from(User).where(User.role == "admin"))
        if admin_count <= 1:
            raise HTTPException(status_code=400, detail="系统至少需要保留一个管理员")
        user.role = "user"
    else:
        raise HTTPException(status_code=400, detail="非法操作")
    db.commit()
    return {"ok": True, "role": user.role}


@router.post("/users/{user_id}/reset-password")
def reset_user_password(
    user_id: int,
    body: dict,
    admin: User = Depends(get_admin),
    db: Session = Depends(get_db),
):
    """管理员重置用户密码。"""
    user = db.get(User, user_id)
    if not user:
        raise HTTPException(status_code=404, detail="用户不存在")
    new_password = (body.get("password") or "").strip()
    if len(new_password) < 6:
        raise HTTPException(status_code=400, detail="密码至少 6 位")
    user.password_hash = hash_password(new_password)
    db.commit()
    return {"ok": True}


@router.get("/ai-logs")
def list_ai_logs(db: Session = Depends(get_db)):
    # 左连接 replies：forum 日志需带出回复的撤回状态，便于在本页直接撤回/恢复
    rows = db.execute(
        select(AiLog, Reply)
        .outerjoin(Reply, AiLog.reply_id == Reply.id)
        .order_by(AiLog.created_at.desc())
        .limit(200)
    ).all()
    out = []
    for log, reply in rows:
        out.append(
            {
                "id": log.id,
                "module": log.module,
                "engine": log.engine,
                "prompt": log.prompt[:300],
                "response": log.response[:600],
                "created_at": _iso(log.created_at),
                "reply_id": reply.id if reply else None,
                "post_id": reply.post_id if reply else None,
                "reply_status": reply.status if reply else None,
                "recalled": bool(reply.recalled) if reply else False,
                "recall_reason": (reply.recall_reason or "") if reply else "",
            }
        )
    return out
