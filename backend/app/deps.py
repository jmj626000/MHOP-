"""依赖：匿名可选鉴权 / 登录用户 / 管理员。"""
from fastapi import Depends, HTTPException, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy.orm import Session

from .database import get_db
from .models import User
from .security import decode_token

bearer = HTTPBearer(auto_error=False)


def get_user_optional(
    cred: HTTPAuthorizationCredentials | None = Depends(bearer),
    db: Session = Depends(get_db),
) -> User | None:
    if cred is None:
        return None
    payload = decode_token(cred.credentials)
    if not payload:
        return None
    user = db.get(User, int(payload["sub"]))
    if user is None or user.status != "active":
        return None
    return user


def get_current_user(user: User | None = Depends(get_user_optional)) -> User:
    if user is None:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="请先登录")
    return user


def get_admin(user: User = Depends(get_current_user)) -> User:
    if user.role != "admin":
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="需要管理员权限")
    return user
