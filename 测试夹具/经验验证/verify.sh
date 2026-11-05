#!/bin/zsh
# 经验库实测验证脚本
# 用途：把 经验/ 目录里可检验的关键结论，用真实命令行与真实归档往返跑一遍，
#       避免经验文档只是照抄历史文档。可重复执行，不改动仓库内任何业务文件。
# 环境：macOS（Apple Silicon）/ .NET 10；其它平台需自行调整 stat 参数。
# 输出：每项 PASS/FAIL，最后汇总；所有产物落在系统临时目录并在退出时清理。
set -uo pipefail

repo_root="${0:A:h:h:h}"
fixture_dir="${0:A:h}/输入"
dotnet_bin="${DOTNET_BIN:-$(command -v dotnet)}"
app="$repo_root/bin/Debug/net10.0/BatchCompress.Avalonia.dll"
tests_dll="$repo_root/BatchCompress.Avalonia.Tests/bin/Debug/net10.0/BatchCompress.Avalonia.Tests.dll"
work="$(mktemp -d /tmp/bc-exp-verify.XXXXXX)"
trap 'rm -rf "$work"' EXIT

pass=0
fail=0
report() {
  if [[ "$2" == "0" ]]; then
    echo "PASS  $1"
    pass=$((pass + 1))
  else
    echo "FAIL  $1"
    fail=$((fail + 1))
  fi
}

echo "=== 0. 开测前重新构建，确认被测产物是最新的 ==="
"$dotnet_bin" build "$repo_root/BatchCompress.Avalonia.sln" --nologo > "$work/build.log" 2>&1
if [[ $? -ne 0 ]]; then
  echo "构建失败，测试结论不成立："
  tail -20 "$work/build.log"
  exit 1
fi
grep -E "Build succeeded|Warning\(s\)|Error\(s\)" "$work/build.log" | tail -3

cp -R "$fixture_dir" "$work/来源"
# 刻意造的系统元数据文件在运行时生成：仓库 .gitignore 会忽略 .DS_Store，
# 若依赖静态夹具，别人克隆后复跑会缺文件、让「元数据过滤」用例形同虚设。
printf 'metadata' > "$work/来源/.DS_Store"
printf 'temp' > "$work/来源/.tmp缓存"
expected_version="$(tr -d '[:space:]' < "$repo_root/VERSION")"

echo ""
echo "=== 1. 版本号唯一来源 ==="
ver="$("$dotnet_bin" "$app" --version 2>/dev/null)"
if [[ "$ver" == *"$expected_version"* ]]; then r=0; else r=1; fi
report "--version 输出与 VERSION 一致（$expected_version）：$ver" "$r"

echo ""
echo "=== 2. 帮助语言预扫描 ==="
help_en="$("$dotnet_bin" "$app" --help --lang en 2>/dev/null)"
if [[ "$help_en" == *"compress"* && "$help_en" != *"用法:"* ]]; then r=0; else r=1; fi
report "--help --lang en 输出英文帮助（不经过完整解析）" "$r"

echo ""
echo "=== 3. 参数错误退出码 2 ==="
"$dotnet_bin" "$app" compress --concurrency 99 --input "$work/来源/普通文本.txt" --output "$work/invalid" > "$work/invalid.log" 2>&1
rc=$?
if [[ $rc -eq 2 ]]; then r=0; else r=1; fi
report "非法并发度返回退出码 2（实际 $rc）" "$r"

echo ""
echo "=== 4. 试运行不改文件 ==="
"$dotnet_bin" "$app" compress --source "$work/来源" --output "$work/out-dry" --format 7z --dry-run \
  --log-file "$work/dry.log" > "$work/dry.txt" 2>&1
rc=$?
if [[ $rc -eq 0 ]] && grep -q "DRY-RUN" "$work/dry.txt" && [[ ! -d "$work/out-dry" ]]; then r=0; else r=1; fi
report "--dry-run 只列任务、不创建输出目录（退出码 $rc）" "$r"

echo ""
echo "=== 5. 系统元数据过滤 ==="
if ! grep -q "DS_Store" "$work/dry.txt" && ! grep -q "tmp缓存" "$work/dry.txt" \
   && grep -q "正常文件" "$work/dry.txt"; then r=0; else r=1; fi
report "跳过 .DS_Store 与 .tmp 前缀，同时保留普通文件" "$r"

echo ""
echo "=== 6. 来源路径尾部斜杠不产生空名归档 ==="
mkdir -p "$work/out1"
"$dotnet_bin" "$app" compress --input "$work/来源/另一个目录/" --output "$work/out1" --format 7z \
  --log-file "$work/c1.log" > "$work/c1.txt" 2>&1
rc=$?
empty_name="$(find "$work/out1" -maxdepth 1 -name ".7z" -print)"
named_archive="$(find "$work/out1" -maxdepth 1 -name "*另一个目录*.7z" -print)"
if [[ $rc -eq 0 && -z "$empty_name" && -n "$named_archive" ]]; then r=0; else r=1; fi
report "带尾斜杠目录压缩出正常归档名，无 .7z 空名文件（退出码 $rc）" "$r"
echo "      实际产物：$(ls -1 "$work/out1" | tr '\n' ' ')"

echo ""
echo "=== 7. 带密码真实压缩 → 解压 → 逐字节核对 ==="
printf 'verify-pass-2026\n' > "$work/pass.txt"
mkdir -p "$work/out2"
"$dotnet_bin" "$app" compress --input "$work/来源/普通文本.txt" --output "$work/out2" --format 7z \
  --password-file "$work/pass.txt" --test --log-file "$work/c2.log" > "$work/c2.txt" 2>&1
compress_rc=$?
archive="$(find "$work/out2" -maxdepth 1 -name "*.7z" -print | head -1)"
if [[ $compress_rc -eq 0 && -n "$archive" ]]; then
  "$dotnet_bin" "$app" extract --input "$archive" --output "$work/unpack" --format 7z \
    --password-file "$work/pass.txt" --log-file "$work/e1.log" > "$work/e1.txt" 2>&1
  extract_rc=$?
  extracted="$(find "$work/unpack" -type f -name "普通文本.txt" -print | head -1)"
  if [[ -n "$extracted" ]] && cmp -s "$fixture_dir/普通文本.txt" "$extracted"; then
    r=0
  else
    r=1
  fi
else
  extract_rc=-1
  extracted=""
  r=1
fi
report "7zz 真实带密码压缩与解压往返内容一致（压缩 $compress_rc / 解压 $extract_rc）" "$r"

echo ""
echo "=== 8. 日志文件权限 0600 ==="
if [[ "$(uname)" == "Darwin" ]]; then
  perm="$(stat -f "%Lp" "$work/c2.log" 2>/dev/null)"
else
  perm="$(stat -c "%a" "$work/c2.log" 2>/dev/null)"
fi
if [[ "$perm" == "600" ]]; then r=0; else r=1; fi
report "含密码日志权限为 0600（实际 $perm）" "$r"

echo ""
echo "=== 9. 结构化输出 NDJSON 不含明文密码 ==="
mkdir -p "$work/out3"
"$dotnet_bin" "$app" compress --input "$work/来源/普通文本.txt" --output "$work/out3" --format 7z \
  --password-file "$work/pass.txt" --output-format json --log-file "$work/j.log" \
  > "$work/j.txt" 2> "$work/j.err"
json_lines="$(grep -c '^{' "$work/j.txt" 2>/dev/null || true)"
if [[ "$json_lines" -gt 0 ]] && ! grep -q "verify-pass-2026" "$work/j.txt" \
   && grep -q "Found" "$work/j.err"; then r=0; else r=1; fi
report "真实压缩时 stdout 为 NDJSON 且不含密码、人类提示在 stderr（$json_lines 行）" "$r"

echo ""
echo "=== 9b. 试运行模式不产生结构化事件流（实测边界） ==="
"$dotnet_bin" "$app" compress --source "$work/来源" --output "$work/out-dry2" --format 7z --dry-run \
  --output-format json --log-file "$work/jd.log" > "$work/jd.txt" 2> "$work/jd.err"
if [[ ! -s "$work/jd.txt" ]] && grep -q "DRY-RUN" "$work/jd.err"; then r=0; else r=1; fi
report "--dry-run 提前返回：stdout 无事件，DRY-RUN 清单走 stderr" "$r"

echo ""
echo "=== 10. 全量回归测试 ==="
"$dotnet_bin" "$tests_dll" > "$work/tests.txt" 2>&1
tests_rc=$?
if [[ $tests_rc -eq 0 ]] && grep -q "全部 .* 项测试通过" "$work/tests.txt"; then r=0; else r=1; fi
report "测试控制台全部通过（退出码 $tests_rc）：$(tail -1 "$work/tests.txt")" "$r"

echo ""
echo "=== 11. 打包前置自检 ==="
"$repo_root/scripts/package-macos.sh" --check > "$work/pkg.txt" 2>&1
pkg_rc=$?
if [[ $pkg_rc -eq 0 ]]; then r=0; else r=1; fi
report "package-macos.sh --check 通过：$(cat "$work/pkg.txt")" "$r"

echo ""
echo "=== 12. 静态事实核对 ==="
rar_count="$(find "$repo_root/tools/rarLinux" -maxdepth 1 -type f | wc -l | tr -d ' ')"
if [[ "$rar_count" == "11" ]]; then r=0; else r=1; fi
report "tools/rarLinux 为官方完整分发包 11 文件（实际 $rar_count）" "$r"

v7="$(strings "$repo_root/tools/7zip/macos/7zz" | grep -m1 -o "7-Zip (z) [0-9.]*" || true)"
if [[ "$v7" == *"26.05"* ]]; then r=0; else r=1; fi
report "macOS 7zz 为 26.05：$v7" "$r"

sln_projects="$(grep -c '^Project(' "$repo_root/BatchCompress.Avalonia.sln" || true)"
if [[ "$sln_projects" == "1" ]]; then r=0; else r=1; fi
report "解决方案只含主项目（$sln_projects 个），测试需单独指定 csproj" "$r"

echo ""
echo "=== 汇总 ==="
echo "PASS=$pass FAIL=$fail"
echo "临时目录：$work（退出时自动清理）"
if [[ $fail -gt 0 ]]; then
  echo "有用例未通过，请把失败项回写到经验文档复核。"
  exit 1
fi
echo "全部实测通过，经验结论已由真实运行验证。"
exit 0
