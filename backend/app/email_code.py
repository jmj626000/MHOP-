"""邮箱登录验证码服务。

- 验证码存内存（单进程部署足够；多进程/多实例请换 Redis）
- 6 位数字、10 分钟有效、校验成功即失效（一次性）、最多错 5 次
- 同一邮箱 60 秒发送间隔；同一 IP 每小时最多 20 次发送
- SMTP_HOST 未配置时为开发模式：不真正发信，验证码写日志并返回给调用方
"""
import logging
import re
import secrets
import smtplib
import ssl
import threading
import time
from email.message import EmailMessage

from .config import settings

logger = logging.getLogger("mhop.mail")

CODE_TTL = 600          # 验证码有效期（秒）
RESEND_INTERVAL = 60    # 同邮箱重发间隔
MAX_ATTEMPTS = 5        # 最大错误次数
IP_HOURLY_LIMIT = 20    # 单 IP 每小时发送上限

_EMAIL_RE = re.compile(r"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$")

_lock = threading.Lock()
_codes: dict[str, tuple[str, float, int]] = {}   # email -> (code, expire_ts, attempts)
_last_sent: dict[str, float] = {}                # email -> ts
_ip_window: dict[str, list[float]] = {}          # ip -> [ts, ...]


def valid_email(email: str) -> bool:
    return bool(email) and len(email) <= 254 and bool(_EMAIL_RE.match(email))


def check_ip_rate(ip: str) -> bool:
    """同一 IP 每小时发送上限。返回 False 表示超限。"""
    now = time.time()
    with _lock:
        arr = [t for t in _ip_window.get(ip, []) if now - t < 3600]
        ok = len(arr) < IP_HOURLY_LIMIT
        if ok:
            arr.append(now)
        _ip_window[ip] = arr
        return ok


def issue_code(email: str) -> tuple[str | None, int]:
    """生成验证码。返回 (code, retry_after)；频控中时 code 为 None。"""
    now = time.time()
    with _lock:
        wait = RESEND_INTERVAL - (now - _last_sent.get(email, 0))
        if wait > 0:
            return None, int(wait) + 1
        code = f"{secrets.randbelow(1000000):06d}"
        _codes[email] = (code, now + CODE_TTL, 0)
        _last_sent[email] = now
        return code, 0


def verify_code(email: str, code: str) -> bool:
    """校验并一次性消费验证码。"""
    now = time.time()
    with _lock:
        item = _codes.get(email)
        if not item:
            return False
        saved, expire, attempts = item
        if now > expire or attempts >= MAX_ATTEMPTS:
            _codes.pop(email, None)
            return False
        if not secrets.compare_digest(saved, (code or "").strip()):
            _codes[email] = (saved, expire, attempts + 1)
            return False
        _codes.pop(email, None)
        return True


def smtp_configured() -> bool:
    return bool(settings.SMTP_HOST)


def send_code_email(to: str, code: str) -> None:
    """通过 SMTP 发送验证码邮件。失败抛异常，由路由层转成 502。"""
    app = settings.APP_NAME
    msg = EmailMessage()
    msg["Subject"] = f"【{app}】登录验证码 {code}"
    msg["From"] = settings.SMTP_FROM or settings.SMTP_USER
    msg["To"] = to

    text = (
        f"你正在登录{app}。\n\n"
        f"你的登录验证码是：{code}\n"
        f"验证码 10 分钟内有效，请勿泄露给他人。如非本人操作，请忽略本邮件。\n\n"
        f"—— {app}"
    )
    html = f"""\
<div style="max-width:480px;margin:0 auto;font-family:'Microsoft YaHei',Arial,sans-serif">
  <h2 style="color:#2f8f83;margin-bottom:8px">{app}</h2>
  <p style="color:#555">你正在登录，本次验证码为：</p>
  <div style="margin:20px 0;font-size:34px;font-weight:700;letter-spacing:8px;color:#2f8f83">{code}</div>
  <p style="color:#888;font-size:13px">验证码 10 分钟内有效，请勿泄露给他人。如非本人操作，请忽略本邮件。</p>
  <hr style="border:none;border-top:1px solid #eee;margin:24px 0">
  <p style="color:#aaa;font-size:12px">本邮件由系统自动发送，请勿回复。如遇紧急心理困扰，请拨打全国心理援助热线 12356。</p>
</div>"""
    msg.set_content(text)
    msg.add_alternative(html, subtype="html")

    ctx = ssl.create_default_context()
    if settings.SMTP_USE_SSL:
        with smtplib.SMTP_SSL(settings.SMTP_HOST, settings.SMTP_PORT, timeout=15, context=ctx) as s:
            if settings.SMTP_USER:
                s.login(settings.SMTP_USER, settings.SMTP_PASSWORD)
            s.send_message(msg)
    else:
        with smtplib.SMTP(settings.SMTP_HOST, settings.SMTP_PORT, timeout=15) as s:
            s.starttls(context=ctx)
            if settings.SMTP_USER:
                s.login(settings.SMTP_USER, settings.SMTP_PASSWORD)
            s.send_message(msg)


def deliver(email: str, code: str) -> bool:
    """发送成功返回 True；开发模式（未配置 SMTP）返回 False，验证码见日志。"""
    if not smtp_configured():
        logger.warning("[开发模式] 邮箱验证码登录：%s 的验证码为 %s（配置 SMTP 后将真正发邮件）", email, code)
        return False
    send_code_email(email, code)
    logger.info("登录验证码已发送至 %s", email)
    return True
