#!/bin/zsh
setopt NO_NOMATCH

# BatchCompress.Avalonia 全功能端到端测试。
# 运行前先完成 dotnet build；脚本只清理并重建本目录下的输出、临时和生成物。

ROOT="${0:A:h:h:h}"
FIXTURE="$ROOT/测试夹具/全功能测试"
OUT="$FIXTURE/输出"
TMP="$FIXTURE/临时"
GEN="$FIXTURE/生成物"
# 全功能夹具固定运行已验证的 Release 产物，避免 --no-build 意外复用残留 Debug 输出。
APP=(dotnet run --project "$ROOT/BatchCompress.Avalonia.csproj" --configuration Release --no-build --)

PASS_COUNT=0
FAIL_COUNT=0

backup_dir="$(mktemp -d /tmp/google-compress-e2e-backup.XXXXXX)"
for generated_name in 输出 临时 生成物; do
  if [[ -e "$FIXTURE/$generated_name" ]]; then
    mv "$FIXTURE/$generated_name" "$backup_dir/"
  fi
done
mkdir -p "$OUT" "$TMP" "$GEN"
print "Previous generated data moved to: $backup_dir"
dd if=/dev/zero of="$GEN/大文件.bin" bs=1m count=3 status=none
openssl rand -out "$GEN/大文件随机.bin" 3145728
printf '%s\n' 'delete source payload' > "$GEN/delete-source.txt"
printf '%s\n' 'move source payload' > "$GEN/move-source.txt"
printf '%s\n' 'update-before' > "$GEN/更新测试.txt"
mkdir -p "$GEN/delete-directory"
printf '%s\n' 'delete directory payload' > "$GEN/delete-directory/item.txt"

record_result() {
  local ok="$1" name="$2" detail="$3"
  if [[ "$ok" == "1" ]]; then
    ((PASS_COUNT++))
    print "PASS $name${detail:+: $detail}"
  else
    ((FAIL_COUNT++))
    print -u2 "FAIL $name${detail:+: $detail}"
  fi
}

run_case() {
  local name="$1" expected="$2"
  shift 2
  local stdout="$GEN/$name.stdout"
  local stderr="$GEN/$name.stderr"
  "$@" >"$stdout" 2>"$stderr"
  local exit_code=$?
  if [[ "$exit_code" == "$expected" ]]; then
    record_result 1 "$name" "exit=$exit_code"
  else
    record_result 0 "$name" "expected exit=$expected, got=$exit_code"
  fi
}

run_stdin_case() {
  local name="$1" expected="$2" input="$3"
  shift 3
  local stdout="$GEN/$name.stdout"
  local stderr="$GEN/$name.stderr"
  print -n -- "$input" | "$@" >"$stdout" 2>"$stderr"
  local exit_code=$?
  if [[ "$exit_code" == "$expected" ]]; then
    record_result 1 "$name" "exit=$exit_code"
  else
    record_result 0 "$name" "expected exit=$expected, got=$exit_code"
  fi
}

check_file() {
  local name="$1" path="$2"
  [[ -f "$path" ]] && record_result 1 "$name" || record_result 0 "$name" "missing: $path"
}

check_absent() {
  local name="$1" path="$2"
  [[ ! -e "$path" ]] && record_result 1 "$name" || record_result 0 "$name" "still exists: $path"
}

check_contains() {
  local name="$1" path="$2" text="$3"
  if [[ -f "$path" ]] && /usr/bin/grep -Fq -- "$text" "$path"; then
    record_result 1 "$name"
  else
    record_result 0 "$name" "text not found: $text"
  fi
}

check_not_contains() {
  local name="$1" path="$2" text="$3"
  if [[ ! -f "$path" ]] || ! /usr/bin/grep -Fq -- "$text" "$path"; then
    record_result 1 "$name"
  else
    record_result 0 "$name" "unexpected text: $text"
  fi
}

check_sha() {
  local name="$1" expected="$2" actual="$3"
  local expected_hash actual_hash
  expected_hash="$(shasum -a 256 "$expected" | awk '{print $1}')"
  actual_hash="$(shasum -a 256 "$actual" | awk '{print $1}')"
  [[ "$expected_hash" == "$actual_hash" ]] && record_result 1 "$name" || record_result 0 "$name" "sha256 differs"
}

run_missing_volume_case() {
  local name="$1" format="$2" source_prefix="$3" first_name="$4" last_name="$5" missing_name="$6"
  local input_dir="$GEN/incomplete-$name" output_dir="$OUT/extract-incomplete-$name"
  rm -rf "$input_dir" "$output_dir"
  mkdir -p "$input_dir"
  local volume_name
  for volume_name in "$source_prefix"*; do
    [[ "${volume_name:t}" == "$missing_name" ]] && continue
    cp "$volume_name" "$input_dir/"
  done
  run_case "extract-incomplete-$name" 1 "${APP[@]}" extract --source "$input_dir" -o "$output_dir" -e "$format" --no-random-password
  check_absent "extract-incomplete-$name-no-output" "$output_dir"
}

# 入口、帮助和严格参数校验。
run_case cli-help 0 "${APP[@]}" --help
run_case cli-version 0 "${APP[@]}" --version
run_case cli-alias-compress 0 "${APP[@]}" -c -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/alias" -e 7z --no-random-password --dry-run
run_case cli-alias-decompress-missing-input 2 "${APP[@]}" -d -i "$OUT/alias/普通文本.txt.7z" -o "$OUT/alias-extract" -e 7z --no-random-password --dry-run
run_case cli-invalid-format 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/invalid-format" -e iso
run_case cli-invalid-level 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/invalid-level" -e 7z --level 6
run_case cli-invalid-volume 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/invalid-volume" -e 7z --volume-size 0
run_case cli-invalid-unit 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/invalid-unit" -e 7z --volume-size 1 --volume-unit q
run_case cli-invalid-recovery 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/invalid-recovery" -e rar --recovery 101
run_case cli-conflicting-mode 2 "${APP[@]}" compress extract -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/conflict" -e 7z
run_case cli-conflicting-password 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/conflict-password" -e 7z --password one --password-file "$FIXTURE/password.txt"
run_case cli-conflicting-postprocess 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/conflict-post" -e 7z --delete-source --move-source
run_case cli-conflicting-output 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/conflict-output" -e 7z --verbose --quiet
run_case cli-conflicting-lock-update 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/conflict-lock" -e rar --existing update --lock
run_case cli-invalid-enclosure-file 2 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/invalid-enclosure" -e 7z --comment "$FIXTURE/missing-comment.txt"

# 来源扫描、跳过标记、元数据过滤和 dry-run 不落盘。
run_case scan-default-skip 0 "${APP[@]}" compress --source "$FIXTURE/来源目录" -o "$OUT/scan-default" -e 7z --dry-run
check_absent scan-default-no-output-dir "$OUT/scan-default"
check_not_contains scan-default-no-metadata "$GEN/scan-default-skip.stdout" ".DS_Store"
check_not_contains scan-default-processed "$GEN/scan-default-skip.stdout" "待跳过【已压缩】"
check_not_contains scan-default-processed-extract "$GEN/scan-default-skip.stdout" "待跳过【已解压】"
run_case scan-no-skip 0 "${APP[@]}" compress --source "$FIXTURE/来源目录" -o "$OUT/scan-no-skip" -e 7z --dry-run --no-skip-processed
check_contains scan-no-skip-processed "$GEN/scan-no-skip.stdout" "待跳过【已压缩】"
run_case scan-explicit-inputs 0 "${APP[@]}" compress --input "$FIXTURE/来源目录/普通文本.txt" --input "$FIXTURE/来源目录/普通文本.txt" --input "$FIXTURE/来源目录/子目录" -o "$OUT/scan-inputs" -e 7z --dry-run
run_case scan-equivalent-inputs 0 "${APP[@]}" compress --input "$FIXTURE/来源目录/普通文本.txt" --input "$FIXTURE/来源目录/./普通文本.txt" -o "$OUT/scan-equivalent-inputs" -e 7z --dry-run
check_contains scan-equivalent-inputs-deduplicated "$GEN/scan-equivalent-inputs.stdout" "Total: 1."
run_case compression-list-as-inputs 0 "${APP[@]}" compress --input "$FIXTURE/来源目录/普通文本.txt" --input "$FIXTURE/来源目录/带空格 文件.txt" -o "$OUT/input-archives" -e 7z --no-random-password --test
check_file input-archives-ordinary "$OUT/input-archives/普通文本.txt.7z"
check_file input-archives-spaces "$OUT/input-archives/带空格 文件.txt.7z"

# 三种格式、密码来源、压缩级别、固实、注释、临时目录和 RAR 专属选项。
run_case compress-7z-nopw 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/7z-nopw" -e 7z --no-random-password --level 0 --no-solid --test --temp-dir "$TMP/7z"
run_case cli-alias-decompress-real 0 "${APP[@]}" -d -i "$OUT/7z-nopw/普通文本.txt.7z" -o "$OUT/alias-extract" -e 7z --no-random-password
run_case compress-7z-password-file 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/带空格 文件.txt" -o "$OUT/7z-password-file" -e 7z --password-file "$FIXTURE/password.txt" --level 5 --solid --test --log-file "$GEN/7z-password-file.log"
run_stdin_case compress-7z-password-stdin 0 $'stdin-password\n' "${APP[@]}" compress -i "$FIXTURE/来源目录/中文文件.txt" -o "$OUT/7z-password-stdin" -e 7z --password-stdin --test
run_case compress-7z-direct 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/特殊!@#$ 文件.txt" -o "$OUT/7z-direct" -e 7z --password 'direct password' --test --verbose
run_case compress-7z-random 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/更新测试.txt" -o "$OUT/7z-random" -e 7z --test
run_case compress-7z-base 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/空文件.txt" -o "$OUT/7z-base" -e 7z --password-name base --test
run_case compress-zip 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/中文文件.txt" -o "$OUT/zip" -e zip --password fixture-password --test
run_case compress-rar 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/子目录" -o "$OUT/rar" -e rar --password fixture-password --level 5 --solid --quick-open --recovery 1 --comment "$FIXTURE/comment.txt" --temp-dir "$TMP/rar" --test --verbose
run_case compress-rar-lock 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/rar-lock" -e rar --no-random-password --lock --test
run_case compress-tar 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/子目录" -o "$OUT/tar" -e tar --no-random-password --test
run_case compress-gz 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/gz" -e gz --no-random-password --test
run_case compress-bz2 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/bz2" -e bz2 --no-random-password --test
run_case compress-xz 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/xz" -e xz --no-random-password --test
run_case compress-wim 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/子目录" -o "$OUT/wim" -e wim --no-random-password --test
check_file compress-tar-output "$OUT/tar/子目录.tar"
check_file compress-gz-output "$OUT/gz/普通文本.txt.gz"
check_file compress-bz2-output "$OUT/bz2/普通文本.txt.bz2"
check_file compress-xz-output "$OUT/xz/普通文本.txt.xz"
check_file compress-wim-output "$OUT/wim/子目录.wim"
run_case compress-folder 0 "${APP[@]}" compress --source "$FIXTURE/来源目录" -o "$OUT/folder" -e 7z --no-random-password --no-skip-processed --no-add-enclosures --level 0 --no-solid --existing overwrite
run_case compress-attachment-inline 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/空文件.txt" -o "$OUT/attachment-inline" -e 7z --no-random-password --enclosure "$FIXTURE/附件/联系信息.txt" --enclosure "$FIXTURE/附件/空附件目录" --enclosure "$FIXTURE/附件/不存在附件目录" --test
enclosure_literal="$FIXTURE/附件/联系信息.txt\n$FIXTURE/附件/空附件目录"
run_case compress-attachment-list-literal 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/空文件.txt" -o "$OUT/attachment-list-literal" -e 7z --no-random-password --enclosure-list "$enclosure_literal" --test
run_case compress-no-attachments 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/空文件.txt" -o "$OUT/no-attachments" -e 7z --no-random-password --no-add-enclosures --test

# 已有文件策略、锁定归档、大小上限和后处理。
run_case existing-initial 0 "${APP[@]}" compress -i "$GEN/更新测试.txt" -o "$OUT/existing" -e 7z --no-random-password --existing overwrite
run_case existing-skip 0 "${APP[@]}" compress -i "$GEN/更新测试.txt" -o "$OUT/existing" -e 7z --no-random-password --existing skip
check_contains existing-skip-log "$GEN/existing-skip.stdout" "Skipped"
printf '%s\n' 'updated payload' > "$GEN/更新测试.txt"
run_case existing-update 0 "${APP[@]}" compress -i "$GEN/更新测试.txt" -o "$OUT/existing" -e 7z --no-random-password --existing update
printf '%s\n' 'overwritten payload' >> "$GEN/更新测试.txt"
run_case existing-overwrite 0 "${APP[@]}" compress -i "$GEN/更新测试.txt" -o "$OUT/existing" -e 7z --no-random-password --existing overwrite
run_case locked-update 1 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/rar-lock" -e rar --no-random-password --existing update
run_case max-size-compress 0 "${APP[@]}" compress --input "$FIXTURE/来源目录/普通文本.txt" --input "$GEN/大文件.bin" -o "$OUT/max-size" -e 7z --no-random-password --max-size-gb 0.00000001
check_file max-size-first "$OUT/max-size/普通文本.txt.7z"
check_absent max-size-second "$OUT/max-size/大文件.bin.7z"
run_case post-delete-file 0 "${APP[@]}" compress -i "$GEN/delete-source.txt" -o "$OUT/post-delete" -e 7z --no-random-password --delete-source
check_absent post-delete-file-source "$GEN/delete-source.txt"
check_file post-delete-file-archive "$OUT/post-delete/delete-source.txt.7z"
run_case post-delete-directory 0 "${APP[@]}" compress -i "$GEN/delete-directory" -o "$OUT/post-delete-directory" -e 7z --no-random-password --delete-source
check_absent post-delete-directory-source "$GEN/delete-directory"
run_case post-move-file 0 "${APP[@]}" compress -i "$GEN/move-source.txt" -o "$OUT/post-move" -e 7z --no-random-password --move-source
check_absent post-move-file-source "$GEN/move-source.txt"
check_file post-move-file-target "$GEN/【已压缩】/move-source.txt"

# 分卷创建和创建后校验；7zz 使用实际首卷执行 t。
run_case volume-7z 0 "${APP[@]}" compress -i "$GEN/大文件随机.bin" -o "$OUT/vol7z" -e 7z --no-random-password --volume-size 1 --volume-unit m
run_case volume-7z-test 0 "${APP[@]}" compress -i "$GEN/大文件随机.bin" -o "$OUT/vol7z-test" -e 7z --no-random-password --volume-size 1 --volume-unit m --test
run_case volume-zip-test 0 "${APP[@]}" compress -i "$GEN/大文件随机.bin" -o "$OUT/volzip-test" -e zip --no-random-password --volume-size 1 --volume-unit m --test
run_case volume-zip 0 "${APP[@]}" compress -i "$GEN/大文件随机.bin" -o "$OUT/volzip" -e zip --no-random-password --volume-size 1 --volume-unit m
run_case volume-rar 0 "${APP[@]}" compress -i "$GEN/大文件随机.bin" -o "$OUT/volrar" -e rar --no-random-password --volume-size 1 --volume-unit m --recovery 1 --test
run_case volume-tar 0 "${APP[@]}" compress -i "$GEN/大文件随机.bin" -o "$OUT/voltar" -e tar --no-random-password --volume-size 1 --volume-unit m --test
check_file volume-7z-first "$OUT/vol7z/大文件随机.bin.7z.001"
check_file volume-zip-first "$OUT/volzip/大文件随机.bin.zip.001"
check_file volume-rar-first "$OUT/volrar/大文件随机.bin.part1.rar"
check_file volume-tar-first "$OUT/voltar/大文件随机.bin.tar.001"

# 解压三格式、随机密码、密码本、stdin、多输入、目录扫描和分卷诊断。
run_case extract-7z 0 "${APP[@]}" extract -i "$OUT/7z-nopw/普通文本.txt.7z" -o "$OUT/extract-7z" -e 7z --no-random-password --existing overwrite
run_case extract-zip 0 "${APP[@]}" extract -i "$OUT/zip/中文文件.txt.zip" -o "$OUT/extract-zip" -e zip --password fixture-password --existing overwrite
run_case extract-rar 0 "${APP[@]}" extract -i "$OUT/rar/子目录.rar" -o "$OUT/extract-rar" -e rar --password fixture-password --existing overwrite
run_case extract-tar 0 "${APP[@]}" extract -i "$OUT/tar/子目录.tar" -o "$OUT/extract-tar" -e tar --no-random-password --existing overwrite
run_case extract-gz 0 "${APP[@]}" extract -i "$OUT/gz/普通文本.txt.gz" -o "$OUT/extract-gz" -e gz --no-random-password --existing overwrite
run_case extract-bz2 0 "${APP[@]}" extract -i "$OUT/bz2/普通文本.txt.bz2" -o "$OUT/extract-bz2" -e bz2 --no-random-password --existing overwrite
run_case extract-xz 0 "${APP[@]}" extract -i "$OUT/xz/普通文本.txt.xz" -o "$OUT/extract-xz" -e xz --no-random-password --existing overwrite
run_case extract-wim 0 "${APP[@]}" extract -i "$OUT/wim/子目录.wim" -o "$OUT/extract-wim" -e wim --no-random-password --existing overwrite
check_file extract-tar-content "$OUT/extract-tar/子目录/嵌套文件.txt"
check_file extract-gz-content "$OUT/extract-gz/普通文本.txt"
check_file extract-bz2-content "$OUT/extract-bz2/普通文本.txt"
check_file extract-xz-content "$OUT/extract-xz/普通文本.txt"
check_file extract-wim-content "$OUT/extract-wim/子目录/嵌套文件.txt"
run_case extract-random 0 "${APP[@]}" extract -i "$OUT/7z-random/更新测试.txt.7z" -o "$OUT/extract-random" -e 7z
run_case extract-base 0 "${APP[@]}" extract -i "$OUT/7z-base/空文件.txt.7z" -o "$OUT/extract-base" -e 7z --password-name base
run_stdin_case extract-stdin 0 $'fixture-password\n' "${APP[@]}" extract -i "$OUT/7z-password-file/带空格 文件.txt.7z" -o "$OUT/extract-stdin" -e 7z --password-stdin
run_case extract-source-scan 0 "${APP[@]}" extract --source "$OUT/7z-nopw" -o "$OUT/extract-source-scan" -e 7z --no-random-password
run_case extract-multiple 0 "${APP[@]}" extract -i "$OUT/7z-nopw/普通文本.txt.7z" -i "$OUT/7z-nopw/普通文本.txt.7z" -i "$OUT/7z-password-file/带空格 文件.txt.7z" -o "$OUT/extract-multiple" -e 7z --password fixture-password --existing overwrite
printf '%s\n' \
  '输出/7z-password-file/带空格 文件.txt.7z' 'fixture-password' \
  '输出/7z-password-file/不存在的归档.7z' 'missing-password' > "$GEN/passwordbook-7z.txt"
printf '%s\n' \
  '输出/7z-password-file/带空格 文件.txt.7z' 'fixture-password' \
  '输出/7z-password-file/带空格 文件.txt.7z' 'other-password' > "$GEN/passwordbook-duplicate.txt"
run_case extract-textbook 0 "${APP[@]}" extract --source "$FIXTURE" --text-file "$GEN/passwordbook-7z.txt" -o "$OUT/extract-textbook" -e 7z --no-random-password --existing overwrite
run_case extract-textbook-duplicate 0 "${APP[@]}" extract --source "$FIXTURE" --text-file "$GEN/passwordbook-duplicate.txt" -o "$OUT/extract-textbook-duplicate" -e 7z --no-random-password --existing overwrite
run_case extract-volume-7z 0 "${APP[@]}" extract -i "$OUT/vol7z/大文件随机.bin.7z.001" -o "$OUT/extract-volume-7z" -e 7z --no-random-password --existing overwrite
run_case extract-volume-zip 0 "${APP[@]}" extract -i "$OUT/volzip/大文件随机.bin.zip.001" -o "$OUT/extract-volume-zip" -e zip --no-random-password --existing overwrite
run_case extract-volume-rar 0 "${APP[@]}" extract -i "$OUT/volrar/大文件随机.bin.part1.rar" -o "$OUT/extract-volume-rar" -e rar --no-random-password --existing overwrite
run_case extract-volume-tar 0 "${APP[@]}" extract -i "$OUT/voltar/大文件随机.bin.tar.001" -o "$OUT/extract-volume-tar" -e tar --no-random-password --existing overwrite
check_sha extract-volume-7z-content "$GEN/大文件随机.bin" "$OUT/extract-volume-7z/大文件随机.bin"
check_sha extract-volume-zip-content "$GEN/大文件随机.bin" "$OUT/extract-volume-zip/大文件随机.bin"
check_sha extract-volume-rar-content "$GEN/大文件随机.bin" "$OUT/extract-volume-rar/大文件随机.bin"
check_sha extract-volume-tar-content "$GEN/大文件随机.bin" "$OUT/extract-volume-tar/大文件随机.bin"

# 缺卷：复制一、三、四卷，二卷缺失；程序必须诊断并返回 1，不产生部分解压。
mkdir -p "$GEN/incomplete-7z"
cp "$OUT/vol7z/大文件随机.bin.7z.001" "$GEN/incomplete-7z/"
cp "$OUT/vol7z/大文件随机.bin.7z.003" "$GEN/incomplete-7z/"
cp "$OUT/vol7z/大文件随机.bin.7z.004" "$GEN/incomplete-7z/"
run_case extract-incomplete-volume 1 "${APP[@]}" extract --source "$GEN/incomplete-7z" -o "$OUT/extract-incomplete" -e 7z --no-random-password
check_not_contains incomplete-no-output "$GEN/extract-incomplete.stdout" "成功"

# 真实引擎极端分卷：分别删除首卷、中间卷、末卷。末卷缺失时 7zz 可能先写出残片，
# 业务层必须回滚新增输出；三个引擎都必须返回非零且不留下部分文件。
run_missing_volume_case 7z-first 7z "$OUT/vol7z/大文件随机.bin.7z." \
  大文件随机.bin.7z.001 大文件随机.bin.7z.004 大文件随机.bin.7z.001
run_missing_volume_case 7z-middle 7z "$OUT/vol7z/大文件随机.bin.7z." \
  大文件随机.bin.7z.001 大文件随机.bin.7z.004 大文件随机.bin.7z.002
run_missing_volume_case 7z-last 7z "$OUT/vol7z/大文件随机.bin.7z." \
  大文件随机.bin.7z.001 大文件随机.bin.7z.004 大文件随机.bin.7z.004
run_missing_volume_case zip-first zip "$OUT/volzip/大文件随机.bin.zip." \
  大文件随机.bin.zip.001 大文件随机.bin.zip.004 大文件随机.bin.zip.001
run_missing_volume_case zip-middle zip "$OUT/volzip/大文件随机.bin.zip." \
  大文件随机.bin.zip.001 大文件随机.bin.zip.004 大文件随机.bin.zip.002
run_missing_volume_case zip-last zip "$OUT/volzip/大文件随机.bin.zip." \
  大文件随机.bin.zip.001 大文件随机.bin.zip.004 大文件随机.bin.zip.004
run_missing_volume_case rar-first rar "$OUT/volrar/大文件随机.bin.part" \
  大文件随机.bin.part1.rar 大文件随机.bin.part4.rar 大文件随机.bin.part1.rar
run_missing_volume_case rar-middle rar "$OUT/volrar/大文件随机.bin.part" \
  大文件随机.bin.part1.rar 大文件随机.bin.part4.rar 大文件随机.bin.part2.rar
run_missing_volume_case rar-last rar "$OUT/volrar/大文件随机.bin.part" \
  大文件随机.bin.part1.rar 大文件随机.bin.part4.rar 大文件随机.bin.part4.rar

# 解压已有文件策略和后处理。
run_case extract-existing-initial 0 "${APP[@]}" extract -i "$OUT/attachment-inline/空文件.txt.7z" -o "$OUT/extract-existing" -e 7z --no-random-password --existing overwrite
run_case extract-existing-skip 0 "${APP[@]}" extract -i "$OUT/attachment-inline/空文件.txt.7z" -o "$OUT/extract-existing" -e 7z --no-random-password --existing skip
run_case extract-existing-update 0 "${APP[@]}" extract -i "$OUT/attachment-inline/空文件.txt.7z" -o "$OUT/extract-existing" -e 7z --no-random-password --existing update
run_case extract-post-delete 0 "${APP[@]}" extract -i "$OUT/7z-password-file/带空格 文件.txt.7z" -o "$OUT/extract-post-delete" -e 7z --password fixture-password --delete-source
check_absent extract-post-delete-archive "$OUT/7z-password-file/带空格 文件.txt.7z"
check_file extract-post-delete-file "$OUT/extract-post-delete/带空格 文件.txt"

# 错误密码、quiet/verbose 和原始日志内容。
run_case extract-wrong-password 1 "${APP[@]}" extract -i "$OUT/7z-direct/特殊!@#$ 文件.txt.7z" -o "$OUT/extract-wrong" -e 7z --password wrong-password --quiet --log-file "$GEN/wrong-password.log"
check_file wrong-password-log "$GEN/wrong-password.log"
run_case compress-verbose 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/普通文本.txt" -o "$OUT/verbose" -e 7z --password 'raw log password' --verbose --log-file "$GEN/verbose.log"
check_contains raw-password-in-log "$GEN/verbose.log" "raw log password"
run_case compress-quiet-success 0 "${APP[@]}" compress -i "$FIXTURE/来源目录/空文件.txt" -o "$OUT/quiet" -e 7z --no-random-password --quiet
check_file quiet-archive "$OUT/quiet/空文件.txt.7z"

# 参数 --shutdown 只做帮助/解析和静态日志检查，禁止真实请求关机。
check_contains shutdown-option-help "$GEN/cli-help.stdout" "--shutdown"

print "\nTOTAL PASS=$PASS_COUNT FAIL=$FAIL_COUNT"
[[ "$FAIL_COUNT" == 0 ]]
