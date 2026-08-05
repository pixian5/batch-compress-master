#!/bin/zsh
set -euo pipefail

repo_root="${0:A:h:h}"
publish_dir="$repo_root/.artifacts/macos-publish"
app_path="/Applications/BatchCompress.Avalonia.app"
icon_source="$repo_root/Assets/压缩.ico"
icon_png="$repo_root/Assets/压缩.png"
iconset_tmp="$(mktemp -d /tmp/batch-compress-iconset.XXXXXX)"
iconset_dir="${iconset_tmp}.iconset"
mv "$iconset_tmp" "$iconset_dir"
trap 'rm -rf "$iconset_dir"' EXIT

if [[ ! -f "$icon_source" ]]; then
  print -u2 "missing icon source: $icon_source"
  exit 1
fi

# Avalonia and macOS use different icon containers. Keep the ICO as the source,
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
dotnet publish "$repo_root/BatchCompress.Avalonia.csproj" -c Release -r osx-arm64 \
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$publish_dir" --nologo

rm -rf "$app_path"
mkdir -p "$app_path/Contents/MacOS" "$app_path/Contents/Resources"
cp "$publish_dir/BatchCompress.Avalonia" "$app_path/Contents/MacOS/BatchCompress.Avalonia"
chmod +x "$app_path/Contents/MacOS/BatchCompress.Avalonia"
cp "$repo_root/macos/Info.plist" "$app_path/Contents/Info.plist"
cp "$repo_root/macos/压缩.icns" "$app_path/Contents/Resources/压缩.icns"
touch "$app_path"
codesign --force --deep --sign - "$app_path" >/dev/null

print "Installed: $app_path"
