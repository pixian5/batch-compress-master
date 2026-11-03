# 批量压缩解压工具

[![CI](https://github.com/pixian5/batch-compress-master/actions/workflows/ci.yml/badge.svg)](https://github.com/pixian5/batch-compress-master/actions/workflows/ci.yml)

跨平台桌面批处理工具，使用 Avalonia UI 和 .NET 10 构建。应用通过 RAR/WinRAR 与官方 7-Zip 命令行程序执行压缩和解压，界面与业务逻辑均可运行于 Windows、macOS 和 Linux。

当前版本：`0.6.17`。版本号唯一来源为仓库根目录 `VERSION` 文件，csproj、程序集信息、CLI 版本查询与 macOS 打包脚本均读取它。

## 当前开发进度

`0.6.17` 完成批次 4：添加根目录 MIT `LICENSE`（含 7-Zip 与 RAR 第三方许可说明）；根目录卫生——`Trae.md`/`glm.md` 同文去重、与 `seed开发方案.md` 一并归档至 `docs/archive/`；日志安全治理——Unix 下日志文件创建即收紧为 0600（日志含命令行原文与密码，防同机他用户读取）、单文件超 10MB 轮转为 `.1`、会话启动清理 7 天前 `batchcompress_*` 日志；CLI 解析期约 30 条错误文案全部接入 `LocalizationService`（五语言词条，报错完全跟随 `--language/--lang` 且不受参数顺序影响），zh-TW 帮助补齐标准正体中文；测试 44 → 46 项全过、0 警告 0 错误，多语言报错/繁体帮助/0600 权限均经真实进程冒烟。详细内容见 [docs/20261104-0209-批次4完成报告-GLM-5.3.md](docs/20261104-0209-批次4完成报告-GLM-5.3.md) 与 [docs/20261104-0209-当前开发进度-GLM-5.3.md](docs/20261104-0209-当前开发进度-GLM-5.3.md)。

## 下一步待实现

批次 5：并行度（批量任务多进程并发，注意累计进度计数与管线取消/关机语义的并发适配）；批次 6：发布自动化（三平台打包、版本注入、CI 产物）。历史方案底稿已归档至 `docs/archive/`。即时待办见 [docs/20261104-0209-下一步待实现-GLM-5.3.md](docs/20261104-0209-下一步待实现-GLM-5.3.md)；本软件按 MIT 许可发布（见根目录 `LICENSE`）。

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
