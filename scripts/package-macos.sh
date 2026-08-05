#!/bin/zsh
set -euo pipefail

# GPT-5, 2026-08-05：从脚本位置解析路径，使打包可在任意当前目录执行。
repo_root="${0:A:h:h}"
publish_dir="$repo_root/.artifacts/macos-publish"
app_path="/Applications/BatchCompress.Avalonia.app"
icon_source="$repo_root/Assets/压缩.ico"
icon_png="$repo_root/Assets/压缩.png"
seven_zip_source="$repo_root/tools/7zip/macos"
iconset_tmp="$(mktemp -d /tmp/batch-compress-iconset.XXXXXX)"
iconset_dir="${iconset_tmp}.iconset"
mv "$iconset_tmp" "$iconset_dir"
trap 'rm -rf "$iconset_dir"' EXIT

if [[ ! -f "$icon_source" ]]; then
  print -u2 "missing icon source: $icon_source"
  exit 1
fi

if [[ ! -x "$seven_zip_source/7zz" || ! -f "$seven_zip_source/License.txt" ]]; then
  print -u2 "missing official macOS 7-Zip files: $seven_zip_source"
  exit 1
fi

# GPT-5, 2026-08-05：Avalonia 和 Finder 需要不同图标容器。保留 ICO 作为来源，
# derive a normal RGBA PNG for Avalonia, and build the native ICNS for Finder.
sips -s format png "$icon_source" --out "$icon_png" >/dev/null
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$icon_png" --out "$iconset_dir/icon_${size}x${size}.png" >/dev/null
  if [[ "$size" -le 256 ]]; then
    double=$((size * 2))
    sips -z "$double" "$double" "$icon_png" --out "$iconset_dir/icon_${size}x${size}@2x.png" >/dev/null
  fi
done
iconutil -c icns "$iconset_dir" -o "$repo_root/macos/压缩.icns"

mkdir -p "$publish_dir"
# GPT-5, 2026-08-05：发布自包含 Apple Silicon 可执行文件，使 /Applications 不依赖用户安装的 .NET 运行时。
dotnet publish "$repo_root/BatchCompress.Avalonia.csproj" -c Release -r osx-arm64 \
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$publish_dir" --nologo

# GPT-5, 2026-08-06：编译 macOS 原生状态栏回退，运行时不依赖 Swift 编译器。
swiftc "$repo_root/macos/StatusBarHelper.swift" -o "$publish_dir/BatchCompress.StatusBarHelper"

rm -rf "$app_path"
mkdir -p "$app_path/Contents/MacOS" "$app_path/Contents/Resources"
cp "$publish_dir/BatchCompress.Avalonia" "$app_path/Contents/MacOS/BatchCompress.Avalonia"
cp "$publish_dir/BatchCompress.StatusBarHelper" "$app_path/Contents/MacOS/BatchCompress.StatusBarHelper"
chmod +x "$app_path/Contents/MacOS/BatchCompress.Avalonia"
chmod +x "$app_path/Contents/MacOS/BatchCompress.StatusBarHelper"
cp "$repo_root/macos/Info.plist" "$app_path/Contents/Info.plist"
cp "$repo_root/macos/压缩.icns" "$app_path/Contents/Resources/压缩.icns"
# GPT-5, 2026-08-06：Finder 启动时 PATH 不可靠，因此将官方 7zz 及授权文件放入应用包固定相对路径。
mkdir -p "$app_path/Contents/MacOS/tools/7zip/macos"
cp "$seven_zip_source/7zz" "$app_path/Contents/MacOS/tools/7zip/macos/7zz"
cp "$seven_zip_source/License.txt" "$seven_zip_source/readme.txt" "$seven_zip_source/History.txt" \
  "$app_path/Contents/MacOS/tools/7zip/macos/"
chmod +x "$app_path/Contents/MacOS/tools/7zip/macos/7zz"
touch "$app_path"
codesign --force --deep --sign - "$app_path" >/dev/null

print "Installed: $app_path"
