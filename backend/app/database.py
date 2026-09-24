"""数据访问层。SQLite/MySQL 仅通过 DATABASE_URL 切换。"""
from sqlalchemy import create_engine, inspect, text
from sqlalchemy.orm import DeclarativeBase, sessionmaker

from .config import IS_SQLITE, settings

connect_args = {"check_same_thread": False} if IS_SQLITE else {}
engine = create_engine(settings.DATABASE_URL, connect_args=connect_args, pool_pre_ping=not IS_SQLITE)
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False)


class Base(DeclarativeBase):
    pass


def migrate_schema() -> None:
    """为已存在的库补充新增列（create_all 不会修改既有表）。幂等，SQLite/MySQL 通用。"""
    inspector = inspect(engine)
    alters = []
    if "replies" in inspector.get_table_names():
        columns = {c["name"] for c in inspector.get_columns("replies")}
        if "recalled" not in columns:
            alters.append("ALTER TABLE replies ADD COLUMN recalled BOOLEAN NOT NULL DEFAULT 0")
        if "recall_reason" not in columns:
            alters.append("ALTER TABLE replies ADD COLUMN recall_reason VARCHAR(255) DEFAULT ''")
    if "posts" in inspector.get_table_names():
        post_columns = {c["name"] for c in inspector.get_columns("posts")}
        if "board" not in post_columns:
            alters.append("ALTER TABLE posts ADD COLUMN board VARCHAR(16) NOT NULL DEFAULT 'mood'")
        if "view_count" not in post_columns:
            alters.append("ALTER TABLE posts ADD COLUMN view_count INTEGER NOT NULL DEFAULT 0")
    if "users" in inspector.get_table_names():
        user_columns = {c["name"] for c in inspector.get_columns("users")}
        if "email" not in user_columns:
            alters.append("ALTER TABLE users ADD COLUMN email VARCHAR(255)")
        if "avatar" not in user_columns:
            alters.append("ALTER TABLE users ADD COLUMN avatar VARCHAR(255) NOT NULL DEFAULT ''")
        if "badge" not in user_columns:
            alters.append("ALTER TABLE users ADD COLUMN badge VARCHAR(64) NOT NULL DEFAULT ''")
        if "phone" not in user_columns:
            alters.append("ALTER TABLE users ADD COLUMN phone VARCHAR(20)")
    if "posts" in inspector.get_table_names():
        post_columns = {c["name"] for c in inspector.get_columns("posts")}
        if "images" not in post_columns:
            alters.append("ALTER TABLE posts ADD COLUMN images TEXT DEFAULT ''")
    if "replies" in inspector.get_table_names():
        reply_columns = {c["name"] for c in inspector.get_columns("replies")}
        if "images" not in reply_columns:
            alters.append("ALTER TABLE replies ADD COLUMN images TEXT DEFAULT ''")
    if "ai_logs" in inspector.get_table_names():
        ai_log_columns = {c["name"] for c in inspector.get_columns("ai_logs")}
        if "reply_id" not in ai_log_columns:
            alters.append("ALTER TABLE ai_logs ADD COLUMN reply_id INTEGER")
    if alters:
        with engine.begin() as conn:
            for sql in alters:
                conn.execute(text(sql))
    # 老库补 email 唯一索引（存量行均为 NULL，不会冲突）。新库由 create_all 自动建立。
    if "users" in inspector.get_table_names():
        index_names = {ix["name"] for ix in inspector.get_indexes("users")}
        if "ix_users_email" not in index_names:
            with engine.begin() as conn:
                conn.execute(text("CREATE UNIQUE INDEX ix_users_email ON users (email)"))


def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
