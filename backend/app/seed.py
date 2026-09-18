"""初始化种子数据：管理员账号 + 一条引导帖。"""
from sqlalchemy import select

from .database import SessionLocal
from .models import Post, Reply, User
from .security import hash_password

WELCOME_POST = (
    "最近压力很大，夜里总是睡不着，白天也提不起精神，不知道该怎么办……"
    "第一次来这里，想问问大家都是怎么熬过低谷期的？"
)

WELCOME_AI_REPLY = (
    "谢谢你愿意把这份疲惫说出来。失眠和情绪低落叠加时，人会格外消耗，先别苛责自己。\n\n"
    "可以先尝试两件小事：\n"
    "· 睡前1小时离开手机，做5分钟缓慢呼吸（吸气4秒-屏息7秒-呼气8秒），帮身体先放松；\n"
    "· 白天安排一次10-20分钟的户外走动，自然光对睡眠节律很有帮助。\n\n"
    "如果这种状态已经持续两周以上，或开始影响吃饭、工作和社交，建议到正规医院心理科做一次评估，"
    "这不是软弱，而是认真照顾自己。我们一直在这里，你愿意多说一点也可以。"
)


def seed_data() -> None:
    db = SessionLocal()
    try:
        admin = db.scalar(select(User).where(User.username == "admin"))
        if not admin:
            db.add(
                User(
                    username="admin",
                    password_hash=hash_password("admin123"),
                    role="admin",
                    status="active",
                )
            )
        if db.scalar(select(Post).limit(1)) is None:
            post = Post(user_id=None, is_anonymous=True, content=WELCOME_POST,
                        board="stress", status=1, crisis=False)
            db.add(post)
            db.flush()
            db.add(
                Reply(
                    post_id=post.id,
                    user_id=None,
                    is_anonymous=True,
                    content=WELCOME_AI_REPLY,
                    status=1,
                    is_ai=True,
                )
            )
        db.commit()
    finally:
        db.close()
