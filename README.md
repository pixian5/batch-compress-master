# 批量压缩解压工具

跨平台桌面批处理工具，使用 Avalonia UI 和 .NET 10 构建。应用通过 RAR/WinRAR 与官方 7-Zip 命令行程序执行压缩和解压，界面与业务逻辑均可运行于 Windows、macOS 和 Linux。

当前版本：`0.6.5`。版本号唯一来源为仓库根目录 `VERSION` 文件，csproj、程序集信息、CLI 版本查询与 macOS 打包脚本均读取它。

## 当前开发进度

`0.6.5` 修复运行中切换语言后托盘/状态栏菜单文案不刷新：`App.axaml.cs` 现在订阅语言变化，macOS 会以新文案重启状态栏帮助进程，其它平台重建托盘菜单，实测四种语言切换均即时生效且无进程泄漏。此前 `0.6.4` 完成服务层诊断日志可见性修复（入口注入 `ILogger` ＋ 后处理失败原因并入失败记录）与界面文案本地化收口，`0.6.3` 完成运行消息本地化。详细内容见 [docs/202609300107当前开发进度.md](docs/202609300107当前开发进度.md)。

## 下一步待实现

优先级最高的是 RAR 二进制最终处置（附 `GetLinuxCandidates` 死路径一致性问题），其次是 CLI 输出语言策略、旧式分卷识别（`.r00`、`.z01` 等）、CI 与测试基建；中低优先级另含来源收集逻辑统一、仓库冗余文件与 LICENSE 等卫生项。详细清单见 [docs/202609300107下一步待实现.md](docs/202609300107下一步待实现.md)。

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

**RAR 二进制许可风险（已知并保留）**：仓库内仍保留一份历史提交的 Linux x64 单文件 `rar`（`tools/rarLinux/rar`）。它不属于 win.rar GmbH 许可协议允许分发的“完整且未修改的官方分发包”，按该协议第 3a、3b 条，对外分发与打包进其它软件包均需书面许可。当前构建不会把它复制到输出目录，运行时不依赖它。风险事实、协议原文依据与可选处置见 [docs/202609292351RAR二进制许可风险说明.md](docs/202609292351RAR二进制许可风险说明.md)。

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

- [当前开发进度](docs/202609300107当前开发进度.md)：本批次完成内容与验证结果。
- [下一步待实现](docs/202609300107下一步待实现.md)：按优先级排列的待办清单。
- [RAR 二进制许可风险说明](docs/202609292351RAR二进制许可风险说明.md)：仓库内 RAR 二进制的实际状态、官方协议依据与可选处置。
- 其余 `docs/2026-0x-xx-*.md` 为各次修复与实测报告，保留原始记录。

历史 WinForms 设计和早期改动记录保留在 `文档/`，仅用于追溯，不代表当前实现。
