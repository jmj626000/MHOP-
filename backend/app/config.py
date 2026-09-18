"""全局配置：通过环境变量 / .env 文件覆盖，默认零配置即可本地运行。"""
import os

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    APP_NAME: str = "MHOP 公益心理辅助平台"

    # 默认 SQLite，零依赖开箱即跑；生产改为 MySQL，例如：
    # mysql+pymysql://user:password@host:3306/mhop?charset=utf8mb4
    DATABASE_URL: str = "sqlite:///./mhop.db"

    # JWT
    JWT_SECRET: str = "dev-only-change-me-in-production"
    JWT_ALG: str = "HS256"
    JWT_EXPIRE_HOURS: int = 72

    # LLM（OpenAI 兼容协议；智谱 GLM: https://open.bigmodel.cn/api/paas/v4/）
    LLM_BASE_URL: str = ""
    LLM_API_KEY: str = ""
    LLM_MODEL: str = "glm-4-flash"

    # 北京乐科心理干预研究院专属援助电话（由运营方提供后配置）
    LEKE_HOTLINE: str = ""


settings = Settings()

# 紧急援助热线（首页置顶 + 危机场景强制提示）
HOTLINES = [
    {
        "name": "全国心理援助热线",
        "phone": "12356",
        "tag": "全国通用 · 24小时",
        "description": "国家卫健委统一心理援助热线，免费、保密",
        "level": "national",
    },
    {
        "name": "北京心理危机研究与干预中心",
        "phone": "010-82951332",
        "tag": "危机干预 · 24小时",
        "description": "面向自杀/自伤等紧急心理危机的专业干预热线",
        "level": "national",
    },
    {
        "name": "紧急报警 / 医疗急救",
        "phone": "110 / 120",
        "tag": "生命受到直接威胁时",
        "description": "若你或身边人正处在立即危险中，请第一时间拨打",
        "level": "emergency",
    },
]
if settings.LEKE_HOTLINE:
    HOTLINES.append(
        {
            "name": "北京乐科心理干预研究院专属援助电话",
            "phone": settings.LEKE_HOTLINE,
            "tag": "合作机构专线",
            "description": "平台合作研究院专属援助通道",
            "level": "partner",
        }
    )

IS_SQLITE = settings.DATABASE_URL.startswith("sqlite")
