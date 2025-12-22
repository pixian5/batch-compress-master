<#
PowerShell 脚本：移除当前仓库的 .git 并重新初始化，然后推送到新远程仓库。
用法：
  .\reinit-git.ps1 -RemoteUrl "https://github.com/NEWUSER/NEWREPO.git" -Branch "main"
如果不提供 -RemoteUrl，会提示输入。
警告：该操作会永久删除本地仓库历史（.git 文件夹）。请先备份需要的内容。
#>
param(
    [string]$RemoteUrl,
    [string]$Branch = "main",
    [switch]$Force
)

function Abort($msg){ Write-Host $msg -ForegroundColor Red; exit 1 }

# 检查 git 是否存在
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Abort "找不到 git，可执行文件。请先安装 Git 并保证 git 在 PATH 中。"
}

if (-not $RemoteUrl) {
    $RemoteUrl = Read-Host "请输入新的远程仓库 URL（例如 https://github.com/USER/REPO.git）"
}
if (-not $RemoteUrl) { Abort "未提供远程仓库 URL，已退出。" }

$cwd = Get-Location
$gitDir = Join-Path $cwd ".git"

Write-Host "当前目录： $cwd"
Write-Host "远程仓库： $RemoteUrl"
Write-Host "目标分支： $Branch"

if (Test-Path $gitDir) {
    if (-not $Force) {
        $confirm = Read-Host "将删除本地 .git 目录并清除历史，是否继续？(y/N)"
        if ($confirm -ne 'y' -and $confirm -ne 'Y') {
            Abort "已取消。"
        }
    }

    Write-Host "删除 .git ..."
    try {
        Remove-Item -Recurse -Force $gitDir -ErrorAction Stop
    }
    catch {
        Abort "删除 .git 失败： $_"
    }
}
else {
    Write-Host ".git 未找到，将直接重新初始化仓库。"
}

Write-Host "初始化新的 git 仓库..."
git init

Write-Host "添加全部文件并提交..."
git add .
# 如果工作树为空，仍尝试创建空提交
$commitResult = git commit -m "Initial commit: reinitialize repository" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "git commit 返回非零（可能没有可提交的更改），尝试创建空提交..."
    git commit --allow-empty -m "Initial empty commit: reinitialize repository"
}

Write-Host "设置主分支为 $Branch ..."
# 在旧版本 git 中可能没有 -M
try { git branch -M $Branch } catch { git checkout -b $Branch }

Write-Host "添加远程 origin 并推送（强制）..."
# 删除可能残留的 origin
try { git remote remove origin } catch {}

git remote add origin "$RemoteUrl"

# 强制推送到远程
$push = git push -u origin $Branch --force
if ($LASTEXITCODE -ne 0) {
    Write-Host "推送可能失败，请检查远程仓库权限和 URL。"
    exit $LASTEXITCODE
}

Write-Host "完成。已删除本地历史并将当前内容推送到 $RemoteUrl（分支：$Branch）。" -ForegroundColor Green
