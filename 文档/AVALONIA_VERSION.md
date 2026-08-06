# Avalonia 版本说明

这是当前应用的唯一可构建版本，项目根目录就是 Avalonia 解决方案。历史 WinForms 资料仅保留在文档中作迁移参考。

| 项目 | 当前值 |
| --- | --- |
| 应用版本 | 0.2.3 |
| 目标框架 | .NET 10 (`net10.0`) |
| UI | Avalonia 11.3.18 |
| MVVM | CommunityToolkit.Mvvm 8.2.1 |
| 支持系统 | Windows、macOS、Linux |
| 压缩后端 | RAR/WinRAR 与官方 7-Zip 命令行 |
| 可创建格式 | RAR、ZIP、7z |

运行开发版：`dotnet run --project BatchCompress.Avalonia.csproj`。

macOS 通过 `scripts/package-macos.sh` 发布为自包含 Apple Silicon `.app`，并安装到 `/Applications/BatchCompress.Avalonia.app`。该脚本还会将 `Assets/压缩.ico` 转换为 Avalonia 使用的 PNG 和 Finder 使用的 ICNS。

托盘、拖放、快捷键、通知、窗口状态记忆、完成后关机和取消关机均已实现。平台的通知权限、关机权限和 RAR 安装路径仍由操作系统和本机环境决定；macOS、Linux 的官方 7zz 已保存于项目对应平台目录。
