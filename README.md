# 批量压缩解压工具

跨平台桌面批处理工具，使用 Avalonia UI 和 .NET 10 构建。应用通过 RAR/WinRAR 命令行执行压缩与解压，界面与业务逻辑均可运行于 Windows、macOS 和 Linux。

当前版本：`0.1.4`。

## 功能

- 批量压缩和解压，支持目录、手动列表及 TXT 文件列表。
- 压缩格式仅支持 `rar` 与 `zip`；WinRAR 不能创建 7z，界面会拒绝该格式。
- 支持随机密码、自定义密码、旧版兼容密码查询、分卷、恢复记录、固实压缩、压缩级别、校验、注释、临时目录和既有文件处理策略。
- 支持删除或移动源文件、跳过已处理项目、附件目录、大小限制、完成后关机及取消关机。
- 压缩进程使用 `ProcessStartInfo.ArgumentList` 传递独立参数，并发异步读取 stdout/stderr；命令日志保留原始输出，**不会脱敏或替换密码**。
- 自动跳过 Windows、macOS、Linux 的常见系统元数据和锁文件，例如 `desktop.ini`、`.DS_Store`、`Thumbs.db`、`.Trash-*`。
- 支持拖放、快捷键、原生文件/文件夹选择器、原生通知、系统托盘、窗口位置与尺寸记忆。

## 依赖与平台

需要 .NET 10 SDK（源码运行）和可用的 RAR/WinRAR 命令行程序。

- Windows：安装 WinRAR。程序依次查找随程序发布的 `tools/WinRAR`、注册表、标准安装目录等位置。
- macOS：可安装 RAR，或将可执行文件放入应用的 `tools/rarmacOS/rar`。Apple Silicon 应用包由 `scripts/package-macos.sh` 生成并安装到 `/Applications`。
- Linux：安装 RAR，并可放入应用的 `tools/rarLinux/rar`。

仅 `rar` 可用于创建 RAR 归档；`unrar` 不能完成压缩。

## 开发与验证

```bash
dotnet restore
dotnet build BatchCompress.Avalonia.sln --nologo
dotnet run --project BatchCompress.Avalonia.csproj --no-build
dotnet run --project BatchCompress.Avalonia.Tests/BatchCompress.Avalonia.Tests.csproj --nologo
```

macOS 打包、安装与启动：

```bash
scripts/package-macos.sh
open -n /Applications/BatchCompress.Avalonia.app
```

测试项目覆盖格式限制、密码参数、取消、失败退出码、恢复记录、旧密码和系统元数据过滤。

## 文档

- [文档索引](文档/README.md)：当前文档与历史快照的范围。
- [架构](文档/ARCHITECTURE.md)：当前组件、数据流和平台边界。
- [快速参考](文档/QUICK_REFERENCE.md)：日常使用和排障。
- [跨平台功能评估](文档/跨平台功能补充评估.md)：已实现能力和平台限制。

历史 WinForms 设计和早期改动记录保留在 `文档/`，仅用于追溯，不代表当前实现。
