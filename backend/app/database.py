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
    if "replies" not in inspector.get_table_names():
        return
    columns = {c["name"] for c in inspector.get_columns("replies")}
    alters = []
    if "recalled" not in columns:
        alters.append("ALTER TABLE replies ADD COLUMN recalled BOOLEAN NOT NULL DEFAULT 0")
    if "recall_reason" not in columns:
        alters.append("ALTER TABLE replies ADD COLUMN recall_reason VARCHAR(255) DEFAULT ''")
    post_columns = {c["name"] for c in inspector.get_columns("posts")}
    if "board" not in post_columns:
        alters.append("ALTER TABLE posts ADD COLUMN board VARCHAR(16) NOT NULL DEFAULT 'mood'")
    if "view_count" not in post_columns:
        alters.append("ALTER TABLE posts ADD COLUMN view_count INTEGER NOT NULL DEFAULT 0")
    if alters:
        with engine.begin() as conn:
            for sql in alters:
                conn.execute(text(sql))


def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
