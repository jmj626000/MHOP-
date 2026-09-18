"""注册 / 登录 / 当前用户。匿名访问不需要任何令牌。"""
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..database import get_db
from ..deps import get_current_user
from ..models import User
from ..schemas import LoginIn, RegisterIn, TokenOut, UserOut
from ..security import create_token, hash_password, verify_password

router = APIRouter(prefix="/api/auth", tags=["auth"])


@router.post("/register", response_model=TokenOut)
def register(body: RegisterIn, db: Session = Depends(get_db)):
    username = body.username.strip()
    if db.scalar(select(User).where(User.username == username)):
        raise HTTPException(status_code=400, detail="用户名已存在")
    user = User(username=username, password_hash=hash_password(body.password), role="user")
    db.add(user)
    db.commit()
    db.refresh(user)
    return TokenOut(access_token=create_token(user.id, user.role), user=UserOut.model_validate(user))


@router.post("/login", response_model=TokenOut)
def login(body: LoginIn, db: Session = Depends(get_db)):
    user = db.scalar(select(User).where(User.username == body.username.strip()))
    if not user or not verify_password(body.password, user.password_hash):
        raise HTTPException(status_code=401, detail="用户名或密码错误")
    if user.status != "active":
        raise HTTPException(status_code=403, detail="账号已被停用")
    return TokenOut(access_token=create_token(user.id, user.role), user=UserOut.model_validate(user))


@router.get("/me", response_model=UserOut)
def me(user: User = Depends(get_current_user)):
    return user
