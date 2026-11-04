# 批量压缩解压工具

[![CI](https://github.com/pixian5/batch-compress-master/actions/workflows/ci.yml/badge.svg)](https://github.com/pixian5/batch-compress-master/actions/workflows/ci.yml)

跨平台桌面批处理工具，使用 Avalonia UI 和 .NET 10 构建。应用通过 RAR/WinRAR 与官方 7-Zip 命令行程序执行压缩和解压，界面与业务逻辑均可运行于 Windows、macOS 和 Linux。

当前版本：`0.7.6`（体验与排版增强批次）。版本号唯一来源为仓库根目录 `VERSION` 文件，csproj、程序集信息、CLI 版本查询、macOS Info.plist 与打包产物命名均读取它。

## 当前开发进度

`0.7.6` 完成批次 12（2026-11-04）：**CLI 帮助文本终端宽度自适应折行**——`CommandLineHandler` 实现东亚宽字符（CJK 2 列）计算与基于控制台真实宽度的智能断行算法（ASCII 单词边界切分、东亚单字切分、缩进保持与长缩进动态收缩，重定向时保留原样）；**帮助排版缺陷修正**——纠正简体中文 `--existing` 与 `--lock` 说明错位；**无界面进度展示增强**——`HeadlessBatchRunner` 进度行附加当前条目文件名（` | {FileName}`）并提取为纯函数纳管至测试工程；测试套件 57 → **59 项**全过。详细内容见 [docs/20261104-1155-批次12完成报告-Antigravity.md](docs/20261104-1155-批次12完成报告-Antigravity.md)。

`0.7.5` 完成批次 11（2026-11-04）：**解压/无大小预估场景 ETA 算法补全**——`OperationSession` 引入基于会话总项数与实时单项均速的智能 ETA 兜底算法，彻底解决历史版本在解压、密码本及文本列表模式下剩余时间与完成时刻始终显示为 0 的盲区；**CLI 密码读取错误 5 语言本地化**——`HeadlessBatchRunner` 消除硬编码中文错误，标准输入与外部密码源读取失败文案全面支持 `zh-CN`、`zh-TW`、`en`、`ja`、`de`；测试套件 56 → **57 项**全过。详细内容见 [docs/20261104-1120-批次11完成报告-Antigravity.md](docs/20261104-1120-批次11完成报告-Antigravity.md)。

`0.7.4` 完成批次 10（2026-11-04）：**日志保留天数可配置化**——`FileLoggerService` 与 CLI 增加 `--log-retention-days / --log-retention` 参数（支持 5 语言帮助与错误校验，默认 7 天），按需自定义清理策略；**极速全功能冒烟脚本**——新增 `scripts/smoke-test.sh`（Release 构建、全量 56 项测试、多语言帮助、参数异常阻断、三平台打包自检一键秒级通过）；**端到端夹具测试扩充**——`测试夹具/全功能测试/run-all-tests.sh` 扩充并发、日志保留、语言与 NDJSON 场景（140 → **149 项**端到端测试全绿）；测试套件 54 → 56 项全过。详细内容见 [docs/20261104-1100-批次10完成报告-Antigravity.md](docs/20261104-1100-批次10完成报告-Antigravity.md)。

`0.7.3` 完成批次 9（2026-11-04）：**GUI 附件配置一键保存与重置**——在主窗口附件列表栏提供“保存为默认配置”与“恢复默认模版”操作按钮，支持全 5 语言本地化（简体中文、繁體中文、English、日本語、Deutsch），一键持久化至跨平台配置文件并实时提示；**日志 Gzip 轮转容灾降级（防丢日志）**——`FileLoggerService` 采用临时文件写入加原子替换机制，在极端磁盘满或 IO 异常时自动降级保留纯文本代次，绝不丢失任何日志；**补全 `文档/COMMAND_LINE.md` 完整参考**——补充 `--language / --lang` 5 语言与默认 GUI 启动语义，以及 `--output-format json`（NDJSON 事件流 Schema、事件类型与敏感信息保护规范）；测试 52 → 54 项全过、Release 构建 0 警告 0 错误。详细内容见 [docs/20261104-1030-批次9完成报告-Antigravity.md](docs/20261104-1030-批次9完成报告-Antigravity.md)。

`0.7.2` 完成批次 8（2026-11-04）：**附件联系方式外置**——新建 `Core/Services/EnclosureConfigService.cs`，GUI 启动时按“自定义路径 > 应用目录相邻 `enclosures.txt`（便携模式）> 用户应用数据目录（Unix `~/.config/batchcompress/enclosures.txt`）”查找配置，无配置回退默认联系文案（原硬编码逐字保留），IO 失败安全回退不阻断启动，`MainWindowViewModel` 两处硬编码全部移除；**旧代日志 Gzip 压缩归档**——轮转第 1 代 `.1` 保持纯文本（cat/tail 直接查看），第 2 代起为 `.log.<代次>.gz`（BCL `GZipStream` 零外部依赖，实测文本压缩约 96% 节省、系统 gunzip 互操作校验通过），0.7.1 遗留纯文本代下一轮转自动收敛，`.gz` 同样 0600 并纳入 7 天过期清理；**并发无序性文档化**——`文档/COMMAND_LINE.md` 新增“并发执行”章节（完成顺序非输入顺序、NDJSON 消费方须按 `source` 字段关联、达限/取消/关机语义）；测试 50 → **52 项**全过、Release 构建 0 警告 0 错误。详细内容见 [docs/20261104-0917-批次8完成报告-GLM-5.3.md](docs/20261104-0917-批次8完成报告-GLM-5.3.md)。

`0.7.1` 完成批次 7（2026-11-04）：**CLI 纯 GUI 语言平滑启动**——`--lang en` / `--lang ja` 等不带模式动词、不带批处理来源的命令行现在直接以指定语言启动桌面程序（携带 `--input/--source/--output/--text-file` 等批处理意图时仍要求显式模式，防止误启动 GUI 掩盖参数错误；三语帮助同步更新）；**多代日志轮转**——`FileLoggerService` 由单代 `.1` 升级为 `.1→.2→…→.5` 标准滚动（默认保留 5 代、超限淘汰最旧、0600 权限随 `File.Move` 继承、过期清理覆盖全部代次）；**并发管线锁内精简**——启动/达限日志与完成消息格式化移出 `gate` 锁（缩短高并发临界区），审查确认 `gateCts`/`engineCts`/信号量经 `using` 在取消与异常路径下均确定性释放；测试 48 → 50 项全过、`dotnet build` 0 警告 0 错误。详细内容见 [docs/20261104-0854-批次7完成报告-GLM-5.3.md](docs/20261104-0854-批次7完成报告-GLM-5.3.md)。

`0.7.0` 完成批次 6 并收官 2026-11-03 至 11-04 的全部六批次重构：三平台打包脚本体系（`scripts/package-{macos,windows,linux,all}.sh`，产物命名与 VERSION 严格绑定；macOS 输出 `.app.zip` 已本地实测）与 GitHub Releases 自动发布流（`.github/workflows/release.yml`：`v*` tag 或手工触发，三平台 48 项测试为发布硬门禁，gh CLI 创建 Release 并上传三平台附件，tag 与 VERSION 一致性校验）。六批次累计交付：**批次 0-2**（表征测试护栏→后处理/执行管线/来源收集/界面会话四层下沉，`BatchOperationService` 1,020→425 行、`MainWindowViewModel` 1,673→1,448 行、17 项表征零漂移、尾部斜杠致命缺陷阻断）；**批次 3-5**（CLI 五语言 `--language`＋NDJSON `--output-format json`、RAR 官方完整分发包合规化、MIT LICENSE、日志 0600/轮转/清理、CLI 错误全本地化、受控并发 `--concurrency/-j` 双令牌调度实测 2.4× 加速）；测试 30 → 48 项全过、0 警告 0 错误。详细内容见 [docs/20261104-0232-全项目六批次重构全面收官报告-GLM-5.3.md](docs/20261104-0232-全项目六批次重构全面收官报告-GLM-5.3.md)。

## 下一步待实现

批次 12 已收口。后续候选方向：分卷压缩实时子分卷进度条微粒度估算、跨平台 Release 云端触发验证。发布通道：推送 `v0.7.6` tag 或在 Actions 页手工触发 Release 工作流即可产出三平台安装包。见 [docs/20261104-1155-下一步待实现-Antigravity.md](docs/20261104-1155-下一步待实现-Antigravity.md)。

## 功能

- 批量压缩和解压，支持目录、手动列表及 TXT 文件列表。
- 支持创建与解压 `rar`、`7z`、`zip`、`tar`、`gz`、`bz2`、`xz`、`wim`；RAR 使用 RAR，其余使用官方 `7zz`；7zz 只读格式可解压。
- 支持随机密码、自定义密码、旧版兼容密码查询、分卷、恢复记录、固实压缩、压缩级别、校验、注释、临时目录和既有文件处理策略。
- 支持删除或移动源文件、跳过已处理项目、附件目录、大小限制、完成后关机及取消关机。
- 压缩进程使用 `ProcessStartInfo.ArgumentList` 传递独立参数，并发异步读取 stdout/stderr；命令日志保留原始输出，**不会脱敏或替换密码**。
- 自动跳过 Windows、macOS、Linux 的常见系统元数据和锁文件，例如 `desktop.ini`、`.DS_Store`、`Thumbs.db`、`.Trash-*`。
- 支持拖放、快捷键、原生文件/文件夹选择器、原生通知、系统托盘、窗口位置与尺寸记忆。
- 提供完整 CLI：`compress`、`extract`、精确多输入、目录批处理、TXT 密码清单、密码文件/标准输入、dry-run、详细输出和严格参数校验。

## 依赖与平台

源码运行需要 .NET 10 SDK。RAR 需要用户自行安装或在私有发布流程中注入完整 RAR/WinRAR；其它支持格式使用官方 7-Zip 命令行程序。发布产物（含默认 macOS 应用包）只携带官方 7zz，不打包任何第三方 RAR/WinRAR 二进制或注册文件。

- 所有平台：`BATCHCOMPRESS_RAR_PATH` 可指定一个完整的、用户已获授权的 `rar` 可执行文件，并优先于默认查找路径。
- Windows：安装 WinRAR。程序随后检查注册表和标准安装目录。
- macOS：项目内含官方 7-Zip 26.02 universal `7zz`；RAR 可安装到系统。Apple Silicon 应用包由 `scripts/package-macos.sh` 生成并安装到 `/Applications`；若要私有注入 RAR，设置 `BATCHCOMPRESS_RAR_DIR` 为同时含 `rar`、`unrar` 的目录。
- Linux：项目内分别包含官方 7-Zip 25.01 x64、ARM64 `7zz`；RAR 可安装到系统。

**RAR 二进制许可风险（已知并保留）**：仓库内仍保留一份历史提交的 Linux x64 单文件 `rar`（`tools/rarLinux/rar`）。它不属于 win.rar GmbH 许可协议允许分发的“完整且未修改的官方分发包”，按该协议第 3a、3b 条，对外分发与打包进其它软件包均需书面许可。当前构建不会把它复制到输出目录，运行时不依赖它。风险事实、协议原文依据与可选处置见 [docs/20260929-2351-RAR二进制许可风险说明-未标注.md](docs/20260929-2351-RAR二进制许可风险说明-未标注.md)。

仅 `rar` 可用于创建 RAR 归档；`unrar` 不能完成压缩。

## 开发与验证

```bash
dotnet restore
dotnet build BatchCompress.Avalonia.sln --nologo
dotnet run --project BatchCompress.Avalonia.csproj --no-build
dotnet run --project BatchCompress.Avalonia.Tests/BatchCompress.Avalonia.Tests.csproj --nologo
```

命令行示例：

```bash
# 精确压缩一个目录为 7z；密码从文件读取，不必直接写在本工具命令行中
dotnet run --project BatchCompress.Avalonia.csproj -- compress \
  --input ./data --output ./archives --format 7z \
  --password-file ./password.txt --test --verbose

# 解压多个归档；--input 可重复
dotnet run --project BatchCompress.Avalonia.csproj -- extract \
  --input ./a.7z --input ./b.7z --output ./extracted \
  --format 7z --password-stdin

# 只列出任务，不创建输出目录或归档
dotnet run --project BatchCompress.Avalonia.csproj -- compress \
  --source ./batch --output ./archives --format rar --dry-run
```

使用 `--help` 查看所有选项。参数错误返回 `2`，任务失败返回 `1`，Ctrl+C 取消返回 `130`。完整语义见[命令行参考](文档/COMMAND_LINE.md)。
归档子进程参数与原始日志会保留密码，日志不得作为可公开数据处理。

macOS 打包、安装与启动：

```bash
scripts/package-macos.sh
open /Applications/BatchCompress.Avalonia.app
```

不要使用 `open -n` 启动应用。该参数会强制创建新实例，可能在 macOS Dock 的最近使用区域留下同一应用的重复记录。

测试项目覆盖格式路由、密码参数、取消、失败退出码、恢复记录、旧密码、系统元数据过滤，以及官方 `7zz` 的真实带密码压缩与解压。

## 文档

`文档/` 保存实现层面的长期文档：

- [文档索引](文档/README.md)：当前文档与历史快照的范围。
- [架构](文档/ARCHITECTURE.md)：当前组件、数据流和平台边界。
- [快速参考](文档/QUICK_REFERENCE.md)：日常使用和排障。
- [命令行参考](文档/COMMAND_LINE.md)：完整选项、输入语义、退出码与示例。
- [跨平台功能评估](文档/跨平台功能补充评估.md)：已实现能力和平台限制。

`docs/` 保存按“年月日时分＋标题”命名的进度、决策与风险记录：

- [当前开发进度](docs/20260930-0107-当前开发进度-未标注.md)：本批次完成内容与验证结果。
- [下一步待实现](docs/20260930-0107-下一步待实现-未标注.md)：按优先级排列的待办清单。
- [RAR 二进制许可风险说明](docs/20260929-2351-RAR二进制许可风险说明-未标注.md)：仓库内 RAR 二进制的实际状态、官方协议依据与可选处置。
- 其余 `docs/YYYYMMDD-HHMM-*.md` 为各次修复与实测报告，保留原始记录。完整清单见 [文档索引.md](文档索引.md)。

历史 WinForms 设计和早期改动记录保留在 `文档/`，仅用于追溯，不代表当前实现。
