"""论坛互助：板块、发帖即展示（进入巡检队列）、AI 异步自动回复、人类回复先审后发、点赞。"""
from fastapi import APIRouter, BackgroundTasks, Depends, HTTPException, Query
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from .. import ai, online
from ..boards import BOARDS, BOARD_SLUGS
from ..database import SessionLocal, get_db
from ..deps import get_current_user, get_user_optional
from ..moderation import detect_crisis, hit_sensitive
from ..models import AiLog, Like, Post, Reply, User
from ..schemas import (
    LikeIn,
    PostDetailOut,
    PostIn,
    PostOut,
    ReplyIn,
    ReplyOut,
)

router = APIRouter(prefix="/api/forum", tags=["forum"])


def _author_for(obj, db: Session) -> str:
    if getattr(obj, "is_ai", False):
        return "AI 心理助手"
    if not obj.is_anonymous and obj.user_id is not None:
        u = db.get(User, obj.user_id)
        return u.username if u else "实名用户"
    return "匿名朋友"


# ---------------- 点赞辅助 ----------------

def _like_count_map(db: Session, target_type: str, ids: list[int]) -> dict[int, int]:
    if not ids:
        return {}
    rows = db.execute(
        select(Like.target_id, func.count())
        .where(Like.target_type == target_type, Like.target_id.in_(ids))
        .group_by(Like.target_id)
    ).all()
    return {tid: cnt for tid, cnt in rows}


def _liked_set(db: Session, user: User | None, target_type: str, ids: list[int]) -> set[int]:
    if not user or not ids:
        return set()
    return set(
        db.scalars(
            select(Like.target_id).where(
                Like.user_id == user.id,
                Like.target_type == target_type,
                Like.target_id.in_(ids),
            )
        ).all()
    )


def _visible_replies(post: Post) -> list[Reply]:
    return [r for r in post.replies if r.status == 1 and not r.recalled]


def _reply_out(r: Reply, db: Session, counts: dict, liked: set) -> ReplyOut:
    recalled = bool(r.recalled)
    return ReplyOut(
        id=r.id,
        post_id=r.post_id,
        content="" if recalled else r.content,  # 撤回后公开接口不下发正文
        status=r.status,
        is_ai=r.is_ai,
        is_anonymous=r.is_anonymous,
        author=_author_for(r, db),
        crisis=r.crisis,
        recalled=recalled,
        recall_reason=r.recall_reason or "",
        like_count=counts.get(r.id, 0),
        liked=r.id in liked,
        created_at=r.created_at,
    )


def _post_out(
    p: Post,
    db: Session,
    current: User | None = None,
    like_counts: dict | None = None,
    liked: set | None = None,
) -> PostOut:
    visible = _visible_replies(p)
    last = max(visible, key=lambda r: r.created_at, default=None)
    return PostOut(
        id=p.id,
        content=p.content,
        board=p.board,
        status=p.status,
        crisis=p.crisis,
        is_anonymous=p.is_anonymous,
        author=_author_for(p, db),
        reply_count=len(visible),
        view_count=p.view_count or 0,
        ai_replied=any(r.is_ai for r in visible),
        mine=bool(current and p.user_id == current.id),
        like_count=(like_counts or {}).get(p.id, 0),
        liked=p.id in (liked or set()),
        last_reply_at=last.created_at if last else None,
        last_reply_author=_author_for(last, db) if last else "",
        created_at=p.created_at,
    )


async def _generate_ai_reply(post_id: int, content: str, crisis: bool) -> None:
    """后台任务：独立 DB 会话调用 AI，回复直接以审核通过状态入库。"""
    text, engine = await ai.forum_reply(content, crisis)
    db = SessionLocal()
    try:
        db.add(
            Reply(
                post_id=post_id,
                user_id=None,
                is_anonymous=True,
                content=text,
                status=1,
                is_ai=True,
                crisis=crisis,
            )
        )
        db.add(
            AiLog(user_id=None, module="forum", prompt=content[:2000], response=text[:4000], engine=engine)
        )
        db.commit()
    finally:
        db.close()


@router.get("/boards")
def boards(db: Session = Depends(get_db)):
    """板块元数据 + 各板块可见主题数。"""
    rows = db.execute(
        select(Post.board, func.count())
        .where(Post.status != 2)
        .group_by(Post.board)
    ).all()
    counts = {slug: cnt for slug, cnt in rows}
    return [{**b, "count": counts.get(b["slug"], 0)} for b in BOARDS]


@router.get("/stats")
def public_stats(db: Session = Depends(get_db)):
    """论坛首页侧边栏公开统计。"""
    return {
        "posts": db.scalar(select(func.count()).select_from(Post).where(Post.status != 2)) or 0,
        "replies": db.scalar(select(func.count()).select_from(Reply).where(Reply.status == 1)) or 0,
        "users": db.scalar(select(func.count()).select_from(User)) or 0,
        "online": online.online_count(),
    }


@router.get("/posts", response_model=dict)
def list_posts(
    page: int = Query(1, ge=1),
    size: int = Query(10, ge=1, le=50),
    keyword: str = "",
    board: str = "",
    sort: str = "latest",  # latest=最新回复 / new=最新发布
    db: Session = Depends(get_db),
    current: User | None = Depends(get_user_optional),
):
    stmt = select(Post).where(Post.status != 2)
    if keyword.strip():
        stmt = stmt.where(Post.content.like(f"%{keyword.strip()}%"))
    if board:
        if board not in BOARD_SLUGS:
            raise HTTPException(status_code=400, detail="板块不存在")
        stmt = stmt.where(Post.board == board)

    if sort == "new":
        stmt = stmt.order_by(Post.created_at.desc())
    else:
        # 最新回复：取每帖最新一条已通过回复的时间，无回复则按发帖时间
        last_reply_subq = (
            select(func.max(Reply.created_at))
            .where(Reply.post_id == Post.id, Reply.status == 1, Reply.recalled.is_(False))
            .correlate(Post)
            .scalar_subquery()
        )
        stmt = stmt.order_by(func.coalesce(last_reply_subq, Post.created_at).desc())

    total = db.scalar(select(func.count()).select_from(stmt.subquery()))
    posts = db.scalars(stmt.limit(size).offset((page - 1) * size)).all()

    ids = [p.id for p in posts]
    counts = _like_count_map(db, "post", ids)
    liked = _liked_set(db, current, "post", ids)
    return {
        "total": total,
        "page": page,
        "size": size,
        "items": [_post_out(p, db, current, counts, liked) for p in posts],
    }


@router.post("/posts", response_model=PostOut, status_code=201)
def create_post(
    body: PostIn,
    background: BackgroundTasks,
    db: Session = Depends(get_db),
    current: User | None = Depends(get_user_optional),
):
    content = body.content.strip()
    if not content:
        raise HTTPException(status_code=400, detail="内容不能为空")
    if body.board not in BOARD_SLUGS:
        raise HTTPException(status_code=400, detail="请选择板块")
    crisis = detect_crisis(content)
    post = Post(
        user_id=current.id if current and not body.is_anonymous else None,
        is_anonymous=body.is_anonymous or current is None,
        content=content,
        board=body.board,
        status=0,  # 发帖即可见，同时进入管理员巡检队列
        crisis=crisis,
    )
    db.add(post)
    db.commit()
    db.refresh(post)
    # 异步触发 AI 自动回复，不阻塞发帖请求
    background.add_task(_generate_ai_reply, post.id, content, crisis)
    return _post_out(post, db, current)


@router.get("/posts/{post_id}", response_model=PostDetailOut)
def get_post(
    post_id: int,
    inc_view: bool = Query(False, description="首次打开时计数，轮询不计数"),
    db: Session = Depends(get_db),
    current: User | None = Depends(get_user_optional),
):
    post = db.get(Post, post_id)
    if not post or post.status == 2:
        raise HTTPException(status_code=404, detail="帖子不存在或已被移除")
    if inc_view:
        post.view_count = (post.view_count or 0) + 1
        db.commit()
    out = _post_out(
        post, db, current,
        _like_count_map(db, "post", [post.id]),
        _liked_set(db, current, "post", [post.id]),
    )
    replies = sorted(
        (r for r in post.replies if r.status == 1),  # 已撤回的 AI 回复保留占位、正文已屏蔽
        key=lambda r: (not r.is_ai, r.created_at),  # AI 回复置顶
    )
    reply_ids = [r.id for r in replies]
    counts = _like_count_map(db, "reply", reply_ids)
    liked = _liked_set(db, current, "reply", reply_ids)
    return PostDetailOut(**out.model_dump(), replies=[_reply_out(r, db, counts, liked) for r in replies])


@router.post("/posts/{post_id}/replies", response_model=ReplyOut, status_code=201)
def create_reply(
    post_id: int,
    body: ReplyIn,
    db: Session = Depends(get_db),
    current: User | None = Depends(get_user_optional),
):
    post = db.get(Post, post_id)
    if not post or post.status == 2:
        raise HTTPException(status_code=404, detail="帖子不存在或已被移除")
    content = body.content.strip()
    if not content:
        raise HTTPException(status_code=400, detail="回复内容不能为空")

    crisis = detect_crisis(content)
    words = hit_sensitive(content)
    reply = Reply(
        post_id=post_id,
        user_id=current.id if current and not body.is_anonymous else None,
        is_anonymous=body.is_anonymous or current is None,
        content=content,
        crisis=crisis,
    )
    if words:
        reply.status = 2  # 命中违规词，系统直接拦截
        reply.review_note = f"系统拦截：命中敏感词 {','.join(words)}"
    else:
        reply.status = 0  # 人类回复默认待审核，通过后才公开展示
    db.add(reply)
    db.commit()
    db.refresh(reply)
    return _reply_out(reply, db, {}, set())


@router.post("/likes/toggle")
def toggle_like(
    body: LikeIn,
    db: Session = Depends(get_db),
    current: User = Depends(get_current_user),
):
    """点赞/取消点赞，返回当前计数与我是否点赞。"""
    if body.target_type not in ("post", "reply"):
        raise HTTPException(status_code=400, detail="非法点赞对象")
    model = Post if body.target_type == "post" else Reply
    target = db.get(model, body.target_id)
    if not target or target.status == 2:
        raise HTTPException(status_code=404, detail="内容不存在或已被移除")
    if body.target_type == "reply" and (target.status != 1 or target.recalled):
        raise HTTPException(status_code=400, detail="该回复暂不可点赞")

    existing = db.scalar(
        select(Like).where(
            Like.user_id == current.id,
            Like.target_type == body.target_type,
            Like.target_id == body.target_id,
        )
    )
    liked = existing is None
    if existing:
        db.delete(existing)
    else:
        db.add(Like(user_id=current.id, target_type=body.target_type, target_id=body.target_id))
    db.commit()
    count = db.scalar(
        select(func.count()).select_from(Like).where(
            Like.target_type == body.target_type,
            Like.target_id == body.target_id,
        )
    )
    return {"liked": liked, "like_count": count or 0}


@router.get("/likes/mine")
def my_likes(
    target_type: str = Query(..., pattern="^(post|reply)$"),
    db: Session = Depends(get_db),
    current: User | None = Depends(get_user_optional),
):
    """当前登录用户已点赞的目标 id 列表（未登录返回空）。"""
    if not current:
        return {"ids": []}
    ids = db.scalars(
        select(Like.target_id).where(Like.user_id == current.id, Like.target_type == target_type)
    ).all()
    return {"ids": ids}
