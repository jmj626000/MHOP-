<#
.SYNOPSIS
  MHOP 仓库专用：探测本地代理 -> 安全检查 -> 提交 -> 推送 -> GitHub API 校验 -> 可选等待 CI。
.DESCRIPTION
  本机直连 github.com 被 DNS 污染，需走本地加速器（Steam++ 默认 127.0.0.1:26561）。
  代理只写入仓库级 git config（http.proxy/https.proxy），不影响其他项目。
.PARAMETER Message
  提交信息。不传则只推送已有提交（无内容可推时安静退出）。
.PARAMETER Branch
  目标分支，默认 main。
.PARAMETER DryRun
  只做代理探测与敏感文件检查，不 add/commit/push。
.PARAMETER WaitCi
  推送成功后轮询 GitHub Actions 直到完成并报告各作业结果。
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .trae/skills/github-push/scripts/Invoke-GitHubPush.ps1 -DryRun
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .trae/skills/github-push/scripts/Invoke-GitHubPush.ps1 -Message "feat: xxx" -WaitCi
#>
[CmdletBinding()]
param(
    [string]$Message,
    [string]$Branch = "main",
    [switch]$DryRun,
    [switch]$WaitCi
)

# 注意：不能用 Stop —— Git 的正常进度/CRLF 警告走 stderr，
# PS5 会包装成 ErrorRecord，Stop 下会误终止脚本。关键失败均靠 $LASTEXITCODE 与 throw 判定。
$ErrorActionPreference = "Continue"
$RepoApi = "https://api.github.com/repos/jmj626000/MHOP-"
$ProxyPorts = @(26561, 7890, 7897, 7891, 1080, 10809, 10808, 8888, 2080, 33210)
$Forbidden = @('\.env$', '\.db$', '\.sqlite', '\.pem$', '\.key$', 'node_modules/', 'backend/certs/', '^data/')

function Find-Git {
    $c = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    foreach ($p in @("C:\Program Files\Git\cmd\git.exe", "C:\Program Files (x86)\Git\cmd\git.exe", "$env:LOCALAPPDATA\Programs\Git\cmd\git.exe")) {
        if (Test-Path $p) { return $p }
    }
    throw "未找到 git。请先安装：winget install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements"
}

function Test-GitHubViaProxy {
    param([string]$Proxy)
    try {
        Invoke-RestMethod -Uri $RepoApi -Proxy $Proxy -TimeoutSec 6 -Headers @{ 'User-Agent' = 'mhop-deploy' } | Out-Null
        return $true
    } catch { return $false }
}

function Find-WorkingProxy {
    $candidates = New-Object System.Collections.Generic.List[string]
    try {
        $reg = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -ErrorAction Stop
        if ($reg.ProxyServer) {
            foreach ($s in ($reg.ProxyServer -split ';')) {
                if ($s -match '127\.0\.0\.1:(\d+)$') { $candidates.Add("http://$($matches[0])") | Out-Null }
            }
        }
    } catch {}
    foreach ($p in $ProxyPorts) { $c = "http://127.0.0.1:$p"; if ($candidates -notcontains $c) { $candidates.Add($c) | Out-Null } }

    foreach ($c in $candidates) {
        Write-Host "  试代理 $c ..." -NoNewline
        if (Test-GitHubViaProxy -Proxy $c) { Write-Host " OK"; return $c }
        Write-Host " 不可用"
    }
    # 最后试直连（某些网络环境本身可通）
    Write-Host "  试直连 api.github.com ..." -NoNewline
    try {
        Invoke-RestMethod -Uri $RepoApi -TimeoutSec 6 -Headers @{ 'User-Agent' = 'mhop-deploy' } | Out-Null
        Write-Host " OK（直连）"
        return $null
    } catch { Write-Host " 不可用"; throw "没有可用的 GitHub 通道：请确认 Steam++/代理加速器已开启" }
}

$git = Find-Git
$root = (& $git rev-parse --show-toplevel 2>$null)
if (-not $root) { throw "当前目录不在 git 仓库内（应在 d:\AAAA\MHOP 下运行）" }
Set-Location $root
Write-Host "仓库: $root"

Write-Host "[1/5] 探测代理"
$proxy = Find-WorkingProxy

Write-Host "[2/5] 配置仓库级代理"
if ($proxy) {
    & $git config http.proxy $proxy
    & $git config https.proxy $proxy
    Write-Host "  http(s).proxy = $proxy（仅本仓库）"
} else {
    & $git config --unset http.proxy 2>$null
    & $git config --unset https.proxy 2>$null
    Write-Host "  使用直连"
}

Write-Host "[3/5] 敏感文件检查"
$staged = (& $git add -A --dry-run 2>$null)
$leaks = @()
foreach ($line in $staged) {
    $path = ($line -replace "^add '", '' -replace "'$", '')
    foreach ($pat in $Forbidden) {
        if ($path -match $pat) { $leaks += $path; break }
    }
}
if ($leaks.Count -gt 0) {
    Write-Warning "以下敏感文件将被提交，已中止：`n  $($leaks -join "`n  ")`n请先将其加入 .gitignore 并执行 git reset 暂存。"
    exit 2
}
Write-Host "  通过（$($staged.Count) 个文件待提交）"

if ($DryRun) { Write-Host "`nDryRun 完成：未执行 add/commit/push。"; exit 0 }

Write-Host "[4/5] 提交"
& $git add -A 2>&1 | Out-Null
$cached = (& $git diff --cached --name-only)
if ($cached) {
    if (-not $Message) { throw "有未提交改动但未提供 -Message" }
    & $git commit -m $Message 2>&1 | Out-Null
    Write-Host "  已提交: $Message"
} else {
    Write-Host "  无新改动，跳过 commit"
}

$localHead = (& $git rev-parse HEAD).Trim()
$remote = (& $git remote 2>$null)
if ($remote -notcontains "origin") {
    & $git remote add origin https://github.com/jmj626000/MHOP-.git
}

Write-Host "[5/5] 同步并推送到 origin/$Branch"
& $git fetch origin $Branch 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    & $git rebase "origin/$Branch" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "rebase origin/$Branch 失败（可能有冲突），请人工处理后重试；不要 force push"
    }
    $localHead = (& $git rev-parse HEAD).Trim()
}
& $git push -u origin $Branch 2>&1 | ForEach-Object { Write-Host "  $_" }
# git 进度走 stderr，红字 NativeCommandError 不代表失败
if ($LASTEXITCODE -ne 0) { throw "git push 退出码 $LASTEXITCODE（若长时间卡住可能是凭据弹窗，请在桌面完成 GitHub 授权后重试）" }

Start-Sleep -Seconds 3
$remoteSha = $null
try {
    $shaResp = Invoke-RestMethod -Uri "$RepoApi/commits/$Branch" -Proxy $proxy -TimeoutSec 15 -Headers @{ 'User-Agent' = 'mhop-deploy' }
    $remoteSha = $shaResp.sha
} catch { Write-Warning "API 校验失败：$($_.Exception.Message)" }
if ($remoteSha -and $remoteSha -ne $localHead) { throw "校验失败：本地 $localHead != 远程 $remoteSha" }
Write-Host "`n推送成功并已校验：$localHead" -ForegroundColor Green

if ($WaitCi) {
    Write-Host "`n等待 GitHub Actions CI ..."
    $deadline = (Get-Date).AddMinutes(12)
    $last = ""
    while ((Get-Date) -lt $deadline) {
        $runs = Invoke-RestMethod -Uri "$RepoApi/actions/runs?per_page=1" -Proxy $proxy -TimeoutSec 15 -Headers @{ 'User-Agent' = 'mhop-deploy' }
        $run = $runs.workflow_runs[0]
        $jobsResp = Invoke-RestMethod -Uri "$RepoApi/actions/runs/$($run.id)/jobs" -Proxy $proxy -TimeoutSec 15 -Headers @{ 'User-Agent' = 'mhop-deploy' }
        $parts = $jobsResp.jobs | ForEach-Object { $st = if ($_.conclusion) { $_.conclusion } else { $_.status }; "$($_.name)=$st" }
        $line = $parts -join " | "
        if ($line -ne $last) { Write-Host "  [$([DateTime]::Now.ToString('HH:mm:ss'))] $($run.status): $line"; $last = $line }
        if ($run.status -eq "completed") {
            Write-Host "CI 结论: $($run.conclusion)  $($run.html_url)"
            if ($run.conclusion -ne "success") { exit 3 }
            break
        }
        Start-Sleep -Seconds 20
    }
}
