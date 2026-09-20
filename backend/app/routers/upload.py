"""文件上传：头像、帖子/回复图片。"""
import os
import uuid

from fastapi import APIRouter, Depends, HTTPException, UploadFile, File
from PIL import Image
from io import BytesIO

from ..config import settings
from ..deps import get_current_user

router = APIRouter(prefix="/api/upload", tags=["upload"])

ALLOWED_TYPES = {"image/jpeg", "image/png", "image/webp", "image/gif"}
MAX_SIZE = 5 * 1024 * 1024  # 5MB
THUMB_MAX = 800  # 帖子图片最大宽度
AVATAR_SIZE = 200  # 头像正方形尺寸

_upload_root = os.path.abspath(settings.UPLOAD_DIR)


def _ensure_dirs():
    for sub in ("avatars", "posts"):
        os.makedirs(os.path.join(_upload_root, sub), exist_ok=True)


_ensure_dirs()


def _save_image(data: bytes, sub_dir: str, max_width: int | None, square: bool = False) -> str:
    """保存图片，可选压缩/裁剪。返回 URL 路径 /uploads/sub_dir/xxx.webp"""
    try:
        img = Image.open(BytesIO(data))
    except Exception:
        raise HTTPException(status_code=400, detail="不支持的图片格式")

    if square:
        w, h = img.size
        side = min(w, h)
        img = img.crop(((w - side) // 2, (h - side) // 2, (w + side) // 2, (h + side) // 2))
        img = img.resize((AVATAR_SIZE, AVATAR_SIZE))
    elif max_width and img.width > max_width:
        ratio = max_width / img.width
        img = img.resize((max_width, int(img.height * ratio)))

    if img.mode in ("RGBA", "P"):
        img = img.convert("RGB")

    filename = f"{uuid.uuid4().hex}.webp"
    filepath = os.path.join(_upload_root, sub_dir, filename)
    img.save(filepath, "WEBP", quality=82)
    return f"/uploads/{sub_dir}/{filename}"


@router.post("/avatar")
async def upload_avatar(
    file: UploadFile = File(...),
    user=Depends(get_current_user),
):
    data = await file.read()
    if len(data) > MAX_SIZE:
        raise HTTPException(status_code=400, detail="图片大小不能超过 5MB")
    if file.content_type not in ALLOWED_TYPES:
        raise HTTPException(status_code=400, detail="仅支持 JPG/PNG/WebP/GIF 格式")
    url = _save_image(data, "avatars", None, square=True)
    return {"url": url}


@router.post("/image")
async def upload_image(
    file: UploadFile = File(...),
    user=Depends(get_current_user),
):
    data = await file.read()
    if len(data) > MAX_SIZE:
        raise HTTPException(status_code=400, detail="图片大小不能超过 5MB")
    if file.content_type not in ALLOWED_TYPES:
        raise HTTPException(status_code=400, detail="仅支持 JPG/PNG/WebP/GIF 格式")
    url = _save_image(data, "posts", THUMB_MAX)
    return {"url": url}
