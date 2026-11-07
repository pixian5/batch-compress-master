#!/usr/bin/env bash
set -euo pipefail

# GLM-5.3, 2026-11-04：批次 6——统一打包入口。
# 用法：package-all.sh [macos|windows|linux|all]
#   - 无参数或 all：三个平台全部打包（macOS 产物需 macOS 宿主：sips/iconutil/swiftc；
#     windows/linux 为 RID 交叉发布，可在 macOS/Linux 上完成）；
#   - 指定平台：只打对应平台。
# 所有产物统一输出到 .artifacts/dist（BATCHCOMPRESS_DIST_DIR 可覆盖），
# 产物名与根目录 VERSION 严格绑定。
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
target="${1:-all}"

if [[ ! -f "$repo_root/VERSION" ]]; then
  echo "missing version file: $repo_root/VERSION" >&2
  exit 1
fi
app_version="$(tr -d '[:space:]' < "$repo_root/VERSION")"
echo "== BatchCompress.Avalonia v$app_version 打包（目标：${target}）=="

run_macos() {
  if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "跳过 macOS 打包：当前宿主不是 macOS（sips/iconutil/swiftc 不可用）。" >&2
    return 0
  fi
  zsh "$repo_root/scripts/package-macos.sh" --dist
}

run_windows() {
  bash "$repo_root/scripts/package-windows.sh" --dist
}

run_linux() {
  bash "$repo_root/scripts/package-linux.sh" --dist
}

case "$target" in
  --check)
    if [[ "$(uname -s)" == "Darwin" ]]; then
      zsh "$repo_root/scripts/package-macos.sh" --check
    else
      echo "跳过 macOS 检查：当前宿主不是 macOS。"
    fi
    bash "$repo_root/scripts/package-windows.sh" --check
    bash "$repo_root/scripts/package-linux.sh" --check
    echo "all packaging scripts check ok"
    exit 0
    ;;
  macos)  run_macos ;;
  windows) run_windows ;;
  linux)  run_linux ;;
  all)    run_macos; run_windows; run_linux ;;
  *)
    echo "usage: package-all.sh [macos|windows|linux|all|--check]" >&2
    exit 2
    ;;
esac

echo "== 打包完成，产物清单 =="
ls -l "${BATCHCOMPRESS_DIST_DIR:-$repo_root/.artifacts/dist}" 2>/dev/null || true
