# 批量压缩解压工具

跨平台桌面批处理工具，使用 Avalonia UI 和 .NET 10 构建。应用通过 RAR/WinRAR 与官方 7-Zip 命令行程序执行压缩和解压，界面与业务逻辑均可运行于 Windows、macOS 和 Linux。

当前版本：`0.4.3`。

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

源码运行需要 .NET 10 SDK。RAR 需要 RAR/WinRAR，ZIP/7z 需要官方 7-Zip 命令行程序；macOS 应用包内置两者。

- Windows：安装 WinRAR。程序依次查找随程序发布的 `tools/WinRAR`、注册表、标准安装目录等位置。
- macOS：项目内含官方 7-Zip 25.01 universal `7zz`；RAR 可安装到系统或放入 `tools/rarmacOS/rar`。Apple Silicon 应用包由 `scripts/package-macos.sh` 生成并安装到 `/Applications`。
- Linux：项目内分别包含官方 7-Zip 25.01 x64、ARM64 `7zz`；RAR 可安装到系统或放入 `tools/rarLinux/rar`。

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
# 精确压缩一个目录为 7z；密码从文件读取，不出现在进程参数中
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

macOS 打包、安装与启动：

```bash
scripts/package-macos.sh
open -n /Applications/BatchCompress.Avalonia.app
```

测试项目覆盖格式路由、密码参数、取消、失败退出码、恢复记录、旧密码、系统元数据过滤，以及官方 `7zz` 的真实带密码压缩与解压。

## 文档

- [文档索引](文档/README.md)：当前文档与历史快照的范围。
- [架构](文档/ARCHITECTURE.md)：当前组件、数据流和平台边界。
- [快速参考](文档/QUICK_REFERENCE.md)：日常使用和排障。
- [命令行参考](文档/COMMAND_LINE.md)：完整选项、输入语义、退出码与示例。
- [跨平台功能评估](文档/跨平台功能补充评估.md)：已实现能力和平台限制。

历史 WinForms 设计和早期改动记录保留在 `文档/`，仅用于追溯，不代表当前实现。
