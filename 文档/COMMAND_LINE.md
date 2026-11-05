# 命令行参考

GPT-5，2026-08-10：命令行与 GUI 共用批处理服务和归档引擎。创建支持 RAR、7z、ZIP、TAR、GZIP、BZIP2、XZ、WIM；解压还支持格式目录中由 7zz 读取的只读格式。RAR 使用外部 RAR，其他格式使用官方 7zz；参数解析错误不会启动图形界面。解析器由项目内部维护，不依赖 System.CommandLine 预览期 API。

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
| `--format EXT` | 创建可用 `rar`、`7z`、`zip`、`tar`、`gz`、`bz2`、`xz`、`wim`；解压可用格式目录中的任一可读后缀。别名为 `--extension`、`-e`。 |

7z 数字分卷目录只把 `.7z.001` 作为任务入口；移动分卷前会检查 `【已解压】` 下的整组目标，发现冲突时保留整组源卷，不删除已有目标。移动或删除失败会单独计入 `PostProcessFailed`。

## 密码

- 默认按规范化后的归档文件名生成兼容密码：`a.part001.rar` 使用 `a.rar`，`a.7z.001` 使用 `a.7z`。
- `--password-name archive|base`：密码依据为完整归档名（默认，如 `a.rar`）或去除归档扩展名（`a`）。压缩、普通解压与分卷解压使用同一规则。
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
| `--comment PATH` | RAR 注释文本文件。 |
| `--temp-dir PATH` | 归档程序临时目录。 |
| `--existing skip|update|overwrite` | 已有文件处理策略。 |
| `--lock` | 锁定 RAR 归档；不能与 `--existing update` 同时使用。 |
| `--password-name archive|base` | 随机密码依据：完整归档名（默认）或去除归档扩展名。 |
| `--max-size-gb N` | 最大处理总量；0 表示不限。 |
| `--delete-source` / `--move-source` | 成功后删除或移动，二者互斥。 |
| `--shutdown` | 全部任务完成后请求系统关机。 |
| `--concurrency N` / `-j N` | 并发执行任务数，1-16；默认 1 串行。语义见[并发执行](#并发执行)。 |

RAR 默认将已有归档、媒体、压缩图片和 ZIP 容器文档作为“仅存储”内容，避免二次压缩；ZIP/7z 会保留全部文件，不将该规则误映射为排除项。完整分类见 [RAR 与 7z 后端边界](2026-08-07-RAR与7z边界.md)。

默认开启跳过已处理项目和添加附件，可使用 `--no-skip-processed`、`--no-add-enclosures` 关闭。附件目录通过可重复的 `--enclosure PATH` 指定；`--enclosure-list` 保留旧版换行列表兼容。

## 并发执行

GLM-5.3，2026-11-04（批次 8 文档化）：`--concurrency/-j` 大于 1 时多个归档进程并行执行（信号量限流），吞吐随存储与 CPU 明显提升，但**消息不再保证输入顺序**：

- 任务完成事件（成功/失败/跳过）与进度报告按**实际完成顺序**异步输出，与 `--input`/`--source` 给出的队列顺序无关；耗时短的靠后任务可能先出现在输出中。
- 七类计数（成功、归档失败、后处理失败、忽略、未找到、分卷不完整、歧义）与统计口径和串行模式完全一致，仅输出顺序不同。
- `--output-format json`（NDJSON）的消费方**必须使用每行事件中的 `source` 字段**（当前处理项的文件名）唯一关联与跟踪特定任务的 `progress`/`item` 事件，不得假定事件按输入顺序到达。
- 大小上限（`--max-size-gb`）达到后仅阻止尚未启动的排队任务，在途任务自然完成并正常计入统计；外部取消（Ctrl+C）会即时传播到全部并发引擎。
- 任意并发度下完成时机的语义与串行一致：仅在非取消的正常结束后，`--shutdown` 才恰好请求一次关机。

## 输出与退出码

- `--dry-run`：列出模式、格式、输出和所有任务，不创建目录或归档。
  **实测边界（2026-11-05）**：试运行会提前返回，即使同时指定 `--output-format json`，
  stdout（标准输出）也**不产生任何事件**，DRY-RUN 清单写在 stderr（标准错误）。
  自动化脚本联调 NDJSON 时必须使用真实执行，不要拿试运行模式对接。
- `--verbose`：逐项显示进度和完整归档进程 stdout/stderr。
- `--quiet`：只向 stderr 输出错误；不能与 verbose 同时使用。
- `--log-file PATH`：指定 UTF-8 日志文件；默认写入程序目录的 `logs/`。
- `--log-retention-days DAYS` / `--log-retention`：指定过期日志文件的保留天数（默认 7，必须为 >= 1 的整数）；每次会话启动时自动清理超过该天数的活跃与压缩轮转日志。
- `--language LANG` / `--lang LANG`：指定界面或命令行语言。支持 `zh-CN`、`zh-TW`、`en`、`ja`、`de`。仅传语言且不带批处理参数时，默认启动对应语言的桌面 GUI；带批处理参数时控制终端输出文案与错误信息语言。
- `--output-format text|json` / `--format-output`：指定输出格式，默认 `text`；`json` 开启标准 NDJSON 事件流。

| 退出码 | 含义 |
| --- | --- |
| `0` | 至少一个任务成功，且没有失败；或 dry-run 找到任务。 |
| `1` | 归档失败、后处理失败、没有匹配项目或运行时错误。 |
| `2` | 参数或输入验证错误。 |
| `130` | Ctrl+C 取消。2026-11-05 实测：返回 `130`，日志记录 `Operation cancelled by user (Ctrl+C)`，且无残留 `7zz` 进程；自动化复现方法见 `测试夹具/经验验证/verify.sh`。 |

## NDJSON 结构化流式输出

批次 3 / 8 / 9 规范：通过 `--output-format json`（或 `--format-output json`）启用机器可解析的单行 JSON（NDJSON）流，适用于自动化脚本、监控后台或管道集成。

- **格式特征**：每行输出为一个独立的 JSON 对象，UTF-8 编码，保留可读非 ASCII 字符（如中文文件名与提示）；密码绝不输出到流中。
- **事件 Schema**：
  1. `progress`（任务进度事件）：
     ```json
     {"type":"progress","phase":"start","source":"item1.txt","message":"[1/2] 正在处理 item1.txt...","successCount":0,"failCount":0,"nonexistentCount":0,"postProcessFailCount":0}
     ```
  2. `item`（单项完成事件）：
     ```json
     {"type":"item","status":"success","source":"item1.txt","message":"成功：item1.txt -> item1.7z","successCount":1,"failCount":0,"nonexistentCount":0,"postProcessFailCount":0}
     ```
     `status` 可为 `success`（成功）、`skip`（跳过已有/忽略）、`fail`（归档失败）等。
  3. `summary`（最终汇总事件）：
     ```json
     {"type":"summary","success":2,"failed":0,"postProcessFailed":0,"nonexistent":0,"elapsedSeconds":3.250}
     ```
- **消费准则**：在并发模式（`-j > 1`）下，消费端应依赖每行事件中的 `source` 字段来聚合与追踪特定任务的生命周期，而非依赖接收顺序。

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
