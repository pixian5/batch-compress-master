# 命令行参考

GPT-5，2026-08-06：命令行与 GUI 共用批处理服务和归档引擎，支持 RAR、ZIP、7z。参数解析错误不会启动图形界面。

## 调用形式

```bash
BatchCompress.Avalonia compress [选项]
BatchCompress.Avalonia extract [选项]
BatchCompress.Avalonia gui
BatchCompress.Avalonia --help
BatchCompress.Avalonia --version
```

旧版 `--compress`、`--decompress`、`--gui` 仍然有效。压缩和解压不能同时指定。

macOS 应用包中的可执行文件：

```bash
/Applications/BatchCompress.Avalonia.app/Contents/MacOS/BatchCompress.Avalonia --help
```

## 输入与输出

| 参数 | 语义 |
| --- | --- |
| `--source PATH` | 批处理来源。压缩目录时处理其直接子项；解压目录时只选择匹配格式的归档。 |
| `--input PATH` | 精确输入，可重复。压缩时目录本身作为一个归档来源。 |
| `--text-file PATH` | 仅用于解压；文件名和密码交替排列。`--source` 可作为相对文件名基准目录。 |
| `--output PATH` | 必填输出目录；正式执行时不存在则创建，dry-run 不创建。 |
| `--format rar|zip|7z` | 创建格式或解压目录筛选格式，别名为 `--extension`、`-e`。 |

7z 数字分卷目录只把 `.7z.001` 作为任务入口；成功后的移动或删除会覆盖同组 `.002`、`.003` 等全部分卷。

## 密码

- 默认按归档文件名生成兼容密码。
- `--no-random-password` 创建或解压无密码归档。
- `--password VALUE` 直接提供密码。
- `--password-file PATH` 从文件第一行读取密码。
- `--password-stdin` 从标准输入第一行读取密码。
- 三种显式密码来源互斥，任一种都会关闭随机密码。
- 归档 stdout/stderr 原样输出和记录，明确不脱敏、不替换密码为 `***`。

## 压缩与处理选项

| 参数 | 说明 |
| --- | --- |
| `--level 0..5` | 存储、最快、快速、标准、较好、最佳。 |
| `--solid` / `--no-solid` | 开启或关闭固实压缩。 |
| `--volume-size N --volume-unit b|k|m|g|t` | 创建分卷。 |
| `--test` | 创建后校验归档。 |
| `--quick-open` | RAR 快速打开信息。 |
| `--recovery 0..100` | RAR 恢复记录百分比。 |
| `--comment PATH` | RAR/ZIP 注释文本文件。 |
| `--temp-dir PATH` | 归档程序临时目录。 |
| `--existing skip|update|overwrite` | 已有文件处理策略。 |
| `--max-size-gb N` | 最大处理总量；0 表示不限。 |
| `--delete-source` / `--move-source` | 成功后删除或移动，二者互斥。 |
| `--shutdown` | 全部任务完成后请求系统关机。 |

默认开启跳过已处理项目和添加附件，可使用 `--no-skip-processed`、`--no-add-enclosures` 关闭。附件目录通过可重复的 `--enclosure PATH` 指定；`--enclosure-list` 保留旧版换行列表兼容。

## 输出与退出码

- `--dry-run`：列出模式、格式、输出和所有任务，不创建目录或归档。
- `--verbose`：逐项显示进度和完整归档进程 stdout/stderr。
- `--quiet`：只向 stderr 输出错误；不能与 verbose 同时使用。
- `--log-file PATH`：指定 UTF-8 日志文件；默认写入程序目录的 `logs/`。

| 退出码 | 含义 |
| --- | --- |
| `0` | 至少一个任务成功，且没有失败；或 dry-run 找到任务。 |
| `1` | 任务失败、没有匹配项目或运行时错误。 |
| `2` | 参数或输入验证错误。 |
| `130` | Ctrl+C 取消。 |

## 示例

```bash
# 目录中每个直接子项分别创建一个 7z
BatchCompress.Avalonia compress --source ./source --output ./archives --format 7z --test

# 一个目录整体创建为单个 RAR，不使用密码
BatchCompress.Avalonia compress --input ./source --output ./archives --format rar --no-random-password

# 密码通过管道传入并解压两个文件
printf '%s\n' 'password' | BatchCompress.Avalonia extract \
  --input ./a.7z --input ./b.7z --output ./out --format 7z --password-stdin

# 在执行前核对任务范围
BatchCompress.Avalonia extract --source ./archives --output ./out --format 7z --dry-run
```
