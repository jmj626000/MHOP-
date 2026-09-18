# MHOP 公益心理辅助平台

> 一站式心理帮扶网站：匿名倾诉论坛、AI 即时陪伴、心理量表评估、危机干预热线与后台人工巡检。
>
> 技术栈 **FastAPI + Vue 3 + Element Plus + SQLite/MySQL**，AI 默认接入讯飞星火（OpenAI 兼容协议），全链路支持 HTTPS。

[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](./LICENSE)

---

## 功能特性

### 用户端
- **匿名倾诉论坛（Flarum 风格）**
  - 7 个心理板块：危机求助、情绪树洞、压力倾诉、人际情感、睡眠困扰、康复经验、随便聊聊（板块色横幅）
  - 楼层式帖子流、最后回复排序/最新发布排序、板块筛选、搜索、加载更多、浏览量
  - Markdown 写作与实时预览（自研零依赖安全渲染器）、点赞「送暖心」、复制分享链接、楼层时间线锚点
- **AI 即时陪伴**
  - 发帖后 AI 自动共情回复（可切换任意 OpenAI 兼容大模型）
  - 自伤/自杀危机信号自动识别，强制置顶全国心理援助热线 12356 / 010-82951332 / 110·120
- **心理量表评估**：PHQ-9 抑郁筛查、GAD-7 焦虑筛查、自由倾诉，自动分级并给出 AI 解读与求助指引
- **紧急援助**：首页与危机场景常驻热线横幅，内置全国/危机干预/急救热线，可扩展合作机构专线
- 支持注册登录或匿名发布，在线人数统计

### 管理后台 `/admin`
- 数据看板（主题/回复/用户/待审/今日活跃等）
- 帖子巡检：通过 / 隐藏，板块与危机标记
- 回复审核：通过 / 驳回
- **AI 回复撤回 / 恢复**：「防止 AI 发疯」——可撤回不当 AI 回复（软删除、需填写原因、可恢复、公开页显示热线占位）
- 用户管理（封禁/角色）、AI 调用日志审计

### 安全与工程
- 密码哈希、JWT 鉴权、角色权限校验
- 危机词/敏感词双重识别；AI 输出危机热线双保险
- 内容先 HTML 转义再渲染，Markdown 链接仅允许 http/https，防 XSS
- Schema 幂等迁移（SQLite/MySQL 通用，老库自动补列）
- 密钥仅从 `.env` 读取，不入库不入代码；以 CC BY-NC-SA 4.0 协议开源

---

## 技术架构

```
MHOP/
├── backend/                FastAPI
│   ├── app/
│   │   ├── main.py         应用入口（建表/迁移/种子数据）
│   │   ├── config.py       环境变量配置（pydantic-settings）
│   │   ├── models.py       SQLAlchemy 模型（User/Post/Reply/Like/Assessment/AiLog）
│   │   ├── schemas.py      Pydantic 出入参
│   │   ├── database.py     引擎 + 幂等迁移 migrate_schema()
│   │   ├── ai.py           AI 适配层（OpenAI 兼容 + 本地共情兜底 + 危机前缀）
│   │   ├── boards.py       板块静态常量（slug/名称/颜色）
│   │   ├── moderation.py   危机词/敏感词
│   │   ├── scales.py       PHQ-9/GAD-7 量表与分级
│   │   ├── online.py       在线人数
│   │   ├── security.py     密码哈希/JWT
│   │   ├── seed.py         种子数据
│   │   └── routers/        auth / forum / assessment / admin / common
│   ├── certs/              本地自签证书（.gitignore 排除）
│   ├── .env.example        配置模板（复制为 .env）
│   └── requirements.txt
├── frontend/               Vue 3 + Vite + Element Plus + Pinia
│   └── src/{views,components,layouts,router,stores,utils,styles}
└── scripts/gen_cert.py     本地开发 TLS 自签证书生成
```

| 层 | 技术 |
|---|---|
| 前端 | Vue 3、Vue Router、Pinia、Element Plus、Vite |
| 后端 | FastAPI、SQLAlchemy 2、Pydantic v2、httpx、PyJWT |
| 数据库 | SQLite（默认零配置）/ MySQL（生产可切换） |
| AI | 任意 OpenAI 兼容 `/v1/chat/completions` 服务（默认讯飞星火 4.0Ultra，HTTPS） |

---

## 快速开始

### 环境要求
- Python 3.11+
- Node.js 18+（推荐 20/24）

### 1. 后端

```powershell
cd backend
python -m venv .venv
.\.venv\Scripts\Activate.ps1      # 或使用系统 Python 直接 pip install
pip install -r requirements.txt

copy .env.example .env            # 然后编辑 .env 填入大模型凭证
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

`.env` 关键配置：

```ini
# OpenAI 兼容协议；讯飞星火的鉴权口令格式为 APIKey:APISecret（APPID 不用于 HTTP 兼容接口）
LLM_BASE_URL=https://spark-api-open.xf-yun.com/v1
LLM_API_KEY=你的APIKey:你的APISecret
LLM_MODEL=4.0Ultra

# 默认 SQLite 无需配置；生产可换 MySQL：
# DATABASE_URL=mysql+pymysql://user:password@host:3306/mhop?charset=utf8mb4
# JWT_SECRET=生产环境务必修改
```

> 未配置或调用大模型失败时，系统自动降级为内置共情式规则回复，平台不会中断。

### 2. 前端

```powershell
cd frontend
npm install
npm run dev
```

打开 http://127.0.0.1:5173

- 普通用户可直接注册；**管理员初始账号 `admin / admin123`**（种子数据，登录后请修改密码），后台入口 `/admin`

---

## 本地 HTTPS（开发）

证书仅用于本地开发，浏览器需信任；生产请使用受信任 CA 证书。

```powershell
# 1) 生成自签证书（SAN 含 localhost/127.0.0.1，输出到 backend/certs/）
pip install cryptography
python scripts/gen_cert.py

# 2) 后端以 TLS 启动
cd backend
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000 `
  --ssl-certfile certs\cert.pem --ssl-keyfile certs\key.pem

# 3) 前端（vite.config.js 会自动复用同一张证书，并把 /api 代理到 https 后端）
cd frontend
npm run dev   # https://127.0.0.1:5173
```

Windows 下若浏览器提示证书不受信任，可把 `backend/certs/cert.pem` 导入「当前用户 → 受信任的根证书颁发机构」（无需管理员）。

---

## 生产部署（域名 + HTTPS）

1. 准备一台服务器与域名，构建前端静态文件：`cd frontend && npm run build`（产物 `dist/`，可用 Nginx/Caddy 托管）
2. 后端建议用进程管理启动，仅监听本地：
   ```bash
   uvicorn app.main:app --host 127.0.0.1 --port 8000
   ```
3. 用 **Caddy**（自动申请并续期 Let's Encrypt 免费证书）反代，示例 `Caddyfile`：
   ```
   your-domain.com {
       root * /var/www/mhop/dist
       try_files {path} /index.html
       reverse_proxy /api/* 127.0.0.1:8000
   }
   ```
   或用 Nginx + certbot 配置证书后反向代理 `/api/` 到 8000。
4. 生产环境务必：修改 `JWT_SECRET`、更换管理员密码、将 `.env` 权限收紧、数据库改用 MySQL 并定期备份。

---

## AI 模型切换

项目不绑定厂商，任何兼容 OpenAI Chat Completions 协议的服务均可通过 `.env` 切换：

```ini
LLM_BASE_URL=https://你的网关/v1
LLM_API_KEY=你的密钥
LLM_MODEL=模型名
```

AI 适配逻辑见 [backend/app/ai.py](./backend/app/ai.py)：危机信号强制热线前缀、超时与异常自动兜底。

---

## 安全说明与免责声明

- 本平台提供的 AI 回复与量表结果**仅用于心理支持与健康科普，不构成医学诊断或治疗**。
- 若你或身边人正处于紧急危险中，请立即拨打 **12356**（全国心理援助热线，24 小时）或 **110/120**。
- `.env`、数据库与证书已通过 `.gitignore` 排除；部署后请妥善保管密钥并定期轮换。

---

## 开源协议

本项目采用 **知识共享 署名-非商业性使用-相同方式共享 4.0 国际许可协议**（[CC BY-NC-SA 4.0](./LICENSE)）© 2026 MHOP 公益心理辅助平台 contributors。

你可以在 **署名** 原作者的前提下自由共享与改编，但必须遵守：

- **BY（署名）**：使用、转载或二次开发时须保留版权声明、协议链接并注明修改之处
- **NC（非商业性使用）**：不得将本作品用于商业目的
- **SA（相同方式共享）**：基于本项目的衍生作品须以相同协议（CC BY-NC-SA 4.0）发布

完整法律文本见 [LICENSE](./LICENSE)；人类可读的协议摘要见 https://creativecommons.org/licenses/by-nc-sa/4.0/deed.zh

> 提示：CC 协议主要面向内容类作品；若你需要商业使用授权（例如商用部署、SaaS 运营），请联系版权持有者另行取得书面许可。
