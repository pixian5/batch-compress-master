#!/usr/bin/env bash
set -euo pipefail

# GLM-5.3, 2026-11-04：批次 6——Linux 便携版打包（linux-x64 自包含 tar.gz）。
# 可在任意支持 .NET SDK 的 Unix 环境交叉打包。tar.gz 内容包含：
# - 主程序（自包含单文件，无需安装 .NET 运行时）；
# - 官方 7-Zip linux-x64（7zz + License/readme/History，csproj 按 RID 自动复制）；
# - 官方完整未修改 RAR Linux x64 分发包（tools/rarLinux 全部 12 个文件——许可第 3a 条
#   要求整包携带，不得只挑二进制；详见 docs/20260929-2351 RAR 许可风险说明“已落实方案 B”）；
# - MIT LICENSE。
# 产物名与根目录 VERSION 严格绑定：BatchCompress.Avalonia_<版本>_linux-x64.tar.gz。
# 用法：package-linux.sh [--dist|--check]；默认 dist 模式，输出到 .artifacts/dist。
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mode="${1:---dist}"

if [[ "$mode" == "--check" ]]; then
  command -v dotnet >/dev/null 2>&1 || { echo "check failed: dotnet missing" >&2; exit 1; }
  [[ -f "$repo_root/VERSION" ]] || { echo "check failed: VERSION missing" >&2; exit 1; }
  for f in rar unrar license.txt readme.txt; do
    [[ -f "$repo_root/tools/rarLinux/$f" ]] || { echo "check failed: tools/rarLinux/$f missing" >&2; exit 1; }
  done
  [[ -f "$repo_root/tools/7zip/linux-x64/7zz" ]] || { echo "check failed: tools/7zip/linux-x64/7zz missing" >&2; exit 1; }
  echo "check ok: dotnet/VERSION/rarLinux/7zip ready"
  exit 0
fi
if [[ "$mode" != "--dist" ]]; then
  echo "usage: package-linux.sh [--dist|--check]" >&2
  exit 2
fi

app_version="$(tr -d '[:space:]' < "$repo_root/VERSION")"
if [[ -z "$app_version" ]]; then
  echo "empty version file: $repo_root/VERSION" >&2
  exit 1
fi

dist_dir="${BATCHCOMPRESS_DIST_DIR:-$repo_root/.artifacts/dist}"
publish_dir="$repo_root/.artifacts/linux-publish"
archive_path="$dist_dir/BatchCompress.Avalonia_${app_version}_linux-x64.tar.gz"

# 自包含单文件发布：csproj 的 RID 条件会把 tools/7zip/linux-x64/** 与 tools/rarLinux/**
# 整体复制进产物（Content 项），脚本只做存在性核验，不自行挑选文件。
dotnet publish "$repo_root/BatchCompress.Avalonia.csproj" -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$publish_dir" --nologo

if [[ ! -f "$publish_dir/BatchCompress.Avalonia" ]]; then
  echo "publish output missing executable: $publish_dir/BatchCompress.Avalonia" >&2
  exit 1
fi
for f in "tools/7zip/linux-x64/7zz" "tools/rarLinux/rar" "tools/rarLinux/unrar" "tools/rarLinux/license.txt"; do
  if [[ ! -f "$publish_dir/$f" ]]; then
    echo "publish output missing bundled tool file: $publish_dir/$f" >&2
    exit 1
  fi
done

mkdir -p "$dist_dir/stage/BatchCompress.Avalonia"
cp "$publish_dir/BatchCompress.Avalonia" "$dist_dir/stage/BatchCompress.Avalonia/"
chmod +x "$dist_dir/stage/BatchCompress.Avalonia/BatchCompress.Avalonia"
cp -R "$publish_dir/tools" "$dist_dir/stage/BatchCompress.Avalonia/tools"
cp "$repo_root/LICENSE" "$dist_dir/stage/BatchCompress.Avalonia/"
rm -f "$archive_path"
(cd "$dist_dir/stage" && tar -czf "${archive_path##*/}" "BatchCompress.Avalonia")
rm -rf "$dist_dir/stage" "$publish_dir"
if [[ ! -f "$archive_path" ]]; then
  echo "archive packaging failed: $archive_path" >&2
  exit 1
fi
echo "Packaged: $archive_path (v$app_version)"
