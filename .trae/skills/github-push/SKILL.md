---
name: github-push
description: Push MHOP commits to GitHub from this Windows machine where direct github.com is DNS-poisoned and a local accelerator proxy (Steam++ on 127.0.0.1:26561) is required. Use when the user asks to commit, push, update the GitHub repo jmj626000/MHOP-, or check its Actions CI result. Do not use for git operations on other repositories or unrelated network tasks.
---

# github-push（MHOP 专用）

在本机把 MHOP 提交推送到 `https://github.com/jmj626000/MHOP-` 并核对结果。
本机直连 github.com 会被 DNS 污染（解析到假地址 199.59.148.9），必须走本地加速器代理（Steam++，`http://127.0.0.1:26561`）。

## 标准流程

1. **优先直接调用脚本**（已封装下面全部步骤）：
   ```powershell
   # 仅探测代理、检查待提交文件是否含敏感内容，不提交
   powershell -ExecutionPolicy Bypass -File .trae/skills/github-push/scripts/Invoke-GitHubPush.ps1 -DryRun

   # 正式：add+commit+push+API 校验
   powershell -ExecutionPolicy Bypass -File .trae/skills/github-push/scripts/Invoke-GitHubPush.ps1 -Message "feat: ..."

   # 推送后顺带等待并报告 GitHub Actions CI 结果
   powershell -ExecutionPolicy Bypass -File .trae/skills/github-push/scripts/Invoke-GitHubPush.ps1 -Message "fix: ..." -WaitCi
   ```
   从工具调用时用 Shell（PowerShell 5），cwd 为 `d:\AAAA\MHOP`。

2. 需要手工操作时遵守以下要点（脚本即按此实现）：

## 关键事实与坑（勿重复踩）

- **Git 路径**：本机 PATH 无 git 时用 `C:\Program Files\Git\cmd\git.exe`；未安装则 `winget install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity`，装后当前 shell 仍需用完整路径。
- **代理只写入本仓库配置**，不污染全局：
  `git config http.proxy http://127.0.0.1:26561` / `git config https.proxy ...`；不需要时代理端口可能变化，先探测再更新。
- **代理探测顺序**：① 读注册表 `HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings` 的 `ProxyServer`（即使 ProxyEnable=0 也要试，Steam++ 就是这种情况）；② 常见端口 26561/7890/7897/7891/1080/10809/10808/8888；③ 对每个候选用 `Invoke-RestMethod https://api.github.com/repos/jmj626000/MHOP- -Proxy http://127.0.0.1:<port> -TimeoutSec 6` 验证，第一个成功的即真实代理。
- **验证仓库存在用 api.github.com（经代理）**，不要用浏览器抓 github.com 主页——污染环境下会返回劫持页 HTTP 200，造成误判。
- **PowerShell 5 没有 `&&`/`||`**；多条命令用 `;` 或分行。
- **git 的进度信息走 stderr**：PowerShell 会显示红色 NativeCommandError / `git.exe : To https://...`，这不是失败；**只看 `$LASTEXITCODE` 和输出里的 `旧sha..新sha main -> main`**。
- **提交信息不要用 bash heredoc**；用单个 `-m "..."` 或多个 `-m`。
- **推前安全检查**（.gitignore 已排除但仍需人工确认）：暂存区不得包含 `**/.env`、`*.db`、`*.sqlite*`、`*.pem`、`*.key`、`backend/certs/`、`node_modules/`、`data/`。星火真实密钥绝不能进仓库（占位符仅在 `.env.example`）。
- 远程 main 可能已有提交（如 GitHub 初始 README）：先 `git fetch origin`，再 `git rebase origin/main`，禁止 `--force` 推送。
- 推送可能卡在凭据交互（Git Credential Manager 弹窗）：长时间无输出时提示用户在桌面完成 GitHub 授权后重试。
- 推完后用 GitHub API 核对：远程 `repos/.../commits/main` 的 sha 必须等于本地 `git rev-parse HEAD`。

## CI 轮询

`GET /repos/jmj626000/MHOP-/actions/runs?per_page=1` 取最新 run，再 GET `/actions/runs/{id}/jobs`，每 20-30 秒一次直到 `status=completed`；成功标准是全部 job `conclusion=success`（ci.yml 三个作业：前端构建、后端检查、Docker 镜像构建）。所有 API 请求带 `-Proxy http://127.0.0.1:26561`（或探测到的代理）和 `-Headers @{ 'User-Agent'='mhop-deploy' }`。

## 不要做

- 不要把代理写入 `git config --global`。
- 不要 force push、不要改历史、不要在未确认时 reset/clean。
- 不要提交 `.env`、数据库、证书；发现被暂存立即取消（`git restore --staged`）并检查 .gitignore。
