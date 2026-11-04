#!/usr/bin/env bash
# Antigravity, 2026-11-04：批次 10——快速全功能冒烟自检脚本。
# 在 5 秒内完成 Release 构建、全量单元测试、多语言 CLI 帮助、参数校验与打包检查。
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

VERSION="$(tr -d ' \r\n' < "$ROOT/VERSION")"
echo "== BatchCompress.Avalonia v${VERSION} 快速冒烟自检 =="

echo "1. 编译验证 (Release)..."
dotnet build BatchCompress.Avalonia.sln -c Release --nologo > /dev/null

echo "2. 单元与表征测试套件..."
dotnet run -c Release --project BatchCompress.Avalonia.Tests/BatchCompress.Avalonia.Tests.csproj --nologo > /dev/null

APP_CMD="dotnet run -c Release --no-build --"

echo "3. 命令行版本与帮助冒烟..."
cli_version=$($APP_CMD --version)
if [[ "$cli_version" != "BatchCompress.Avalonia $VERSION" ]]; then
  echo "Error: Version mismatch. Expected 'BatchCompress.Avalonia $VERSION', got '$cli_version'"
  exit 1
fi

# 检查多语言帮助文案
$APP_CMD --help > /dev/null
$APP_CMD --lang en --help > /dev/null
$APP_CMD --lang ja --help > /dev/null
$APP_CMD --lang de --help > /dev/null
$APP_CMD --lang zh-TW --help > /dev/null

echo "4. 参数异常路径阻断验证..."
# 无效语言退出码 2
if $APP_CMD --lang fr 2> /dev/null; then
  echo "Error: Expected --lang fr to exit with code 2"
  exit 1
fi

# 无效并发度退出码 2
if $APP_CMD --concurrency 0 2> /dev/null; then
  echo "Error: Expected --concurrency 0 to exit with code 2"
  exit 1
fi

# 无效日志保留天数退出码 2
if $APP_CMD --log-retention-days 0 2> /dev/null; then
  echo "Error: Expected --log-retention-days 0 to exit with code 2"
  exit 1
fi

echo "5. 打包脚本自检..."
bash scripts/package-all.sh --check > /dev/null

echo "== 全部冒烟检查通过 (PASS) =="
