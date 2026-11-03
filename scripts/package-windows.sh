#!/usr/bin/env bash
set -euo pipefail

# GLM-5.3, 2026-11-04：批次 6——Windows 便携版打包（win-x64 自包含 zip）。
# 可在任意支持 .NET SDK 的 Unix 环境（macOS/Linux/CI runner）交叉打包：dotnet RID 发布
# 不依赖构建机操作系统；Windows 运行时的 RAR/7-Zip 由最终用户按 LICENSE 与 README 安装
# （仓库不携带 Windows 版第三方归档器二进制，Windows 侧历史策略为系统 WinRAR/7-Zip）。
# 产物名与根目录 VERSION 严格绑定：BatchCompress.Avalonia_<版本>_win-x64.zip。
# 用法：package-windows.sh [--dist|--check]；--dist 输出到 .artifacts/dist（默认即 dist 模式）。
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mode="${1:---dist}"

if [[ "$mode" == "--check" ]]; then
  command -v dotnet >/dev/null 2>&1 || { echo "check failed: dotnet missing" >&2; exit 1; }
  [[ -f "$repo_root/VERSION" ]] || { echo "check failed: VERSION missing" >&2; exit 1; }
  echo "check ok: dotnet/VERSION ready"
  exit 0
fi
if [[ "$mode" != "--dist" ]]; then
  echo "usage: package-windows.sh [--dist|--check]" >&2
  exit 2
fi

app_version="$(tr -d '[:space:]' < "$repo_root/VERSION")"
if [[ -z "$app_version" ]]; then
  echo "empty version file: $repo_root/VERSION" >&2
  exit 1
fi

dist_dir="${BATCHCOMPRESS_DIST_DIR:-$repo_root/.artifacts/dist}"
publish_dir="$repo_root/.artifacts/windows-publish"
zip_path="$dist_dir/BatchCompress.Avalonia_${app_version}_win-x64.zip"

# 自包含单文件发布：Windows 机器无需安装 .NET 运行时即可运行 GUI 与 CLI。
dotnet publish "$repo_root/BatchCompress.Avalonia.csproj" -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$publish_dir" --nologo

if [[ ! -f "$publish_dir/BatchCompress.Avalonia.exe" ]]; then
  echo "publish output missing executable: $publish_dir/BatchCompress.Avalonia.exe" >&2
  exit 1
fi

mkdir -p "$dist_dir/stage/BatchCompress.Avalonia"
# 便携版附带许可与说明：MIT 本体 + 第三方（RAR 由用户自装）指引。
cp "$publish_dir/BatchCompress.Avalonia.exe" "$dist_dir/stage/BatchCompress.Avalonia/"
cp "$repo_root/LICENSE" "$dist_dir/stage/BatchCompress.Avalonia/"
rm -f "$zip_path"
(cd "$dist_dir/stage" && zip -r -q "${zip_path##*/}" "BatchCompress.Avalonia")
rm -rf "$dist_dir/stage" "$publish_dir"
if [[ ! -f "$zip_path" ]]; then
  echo "zip packaging failed: $zip_path" >&2
  exit 1
fi
echo "Packaged: $zip_path (v$app_version)"
