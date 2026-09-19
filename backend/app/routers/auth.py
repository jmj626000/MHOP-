"""注册 / 登录 / 当前用户。匿名访问不需要任何令牌。"""
import re
import secrets

from fastapi import APIRouter, Depends, HTTPException, Request
from sqlalchemy import select
from sqlalchemy.orm import Session

from .. import email_code
from ..database import get_db
from ..deps import get_current_user
from ..models import User
from ..schemas import (
    EmailCodeIn,
    EmailLoginIn,
    LoginIn,
    RegisterIn,
    TokenOut,
    UserOut,
)
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


# ---- 邮箱验证码登录 ----

@router.post("/email-code")
def send_email_code(body: EmailCodeIn, request: Request):
    email = body.email.strip().lower()
    if not email_code.valid_email(email):
        raise HTTPException(status_code=400, detail="邮箱格式不正确")
    ip = request.client.host if request.client else "unknown"
    if not email_code.check_ip_rate(ip):
        raise HTTPException(status_code=429, detail="请求过于频繁，请稍后再试")
    code, retry_after = email_code.issue_code(email)
    if code is None:
        raise HTTPException(status_code=429, detail=f"发送太频繁，请 {retry_after} 秒后重试")
    try:
        delivered = email_code.deliver(email, code)
    except Exception:
        raise HTTPException(status_code=502, detail="验证码邮件发送失败，请稍后重试或联系管理员")
    resp = {"sent": True, "ttl": email_code.CODE_TTL, "resend_after": email_code.RESEND_INTERVAL}
    if not delivered:
        # 仅未配置 SMTP 的开发环境返回验证码，方便本机联调；生产环境不返回
        resp["dev_mode"] = True
        resp["dev_code"] = code
    return resp


@router.post("/login-email", response_model=TokenOut)
def login_by_email(body: EmailLoginIn, db: Session = Depends(get_db)):
    email = body.email.strip().lower()
    if not email_code.valid_email(email):
        raise HTTPException(status_code=400, detail="邮箱格式不正确")
    if not email_code.verify_code(email, body.code):
        raise HTTPException(status_code=400, detail="验证码错误或已过期")

    user = db.scalar(select(User).where(User.email == email))
    is_new = False
    if not user:
        # 邮箱首次登录：自动注册（随机不可用密码，账号走邮箱登录，后续可在设置中补密码）
        base = re.sub(r"[^a-zA-Z0-9_\u4e00-\u9fa5]", "", email.split("@")[0]) or "user"
        for _ in range(10):
            username = f"{base[:24]}_{secrets.randbelow(10000):04d}"
            if not db.scalar(select(User).where(User.username == username)):
                break
        else:
            raise HTTPException(status_code=500, detail="注册失败，请稍后重试")
        user = User(
            username=username,
            password_hash=hash_password(secrets.token_urlsafe(24)),
            email=email,
            role="user",
        )
        db.add(user)
        db.commit()
        db.refresh(user)
        is_new = True
    if user.status != "active":
        raise HTTPException(status_code=403, detail="账号已被停用")
    return TokenOut(
        access_token=create_token(user.id, user.role),
        new_account=is_new,
        user=UserOut.model_validate(user),
    )


@router.get("/me", response_model=UserOut)
def me(user: User = Depends(get_current_user)):
    return user
