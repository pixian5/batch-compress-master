# Avalonia 版本说明

> 状态：**2026-11-05 刷新到 `VERSION 0.7.6`**。本文记录当前技术版本与打包方式；
> 历史升级过程（Avalonia 11.3.18 → 12.1.1、移除 System.CommandLine 等）见
> [Avalonia 12 与命令行解析器更新](2026-08-06-Avalonia12与命令行解析器更新.md)，
> 那篇里的版本数字是当时快照，不代表当前。模块边界以 [当前架构](ARCHITECTURE.md) 为准。

本仓库根目录就是当前可构建的解决方案（解决方案只包含主项目），旧 WinForms 实现位于 `old/` 且不参与编译。

| 项目 | 当前值 |
| --- | --- |
| 应用版本 | `0.7.6`（根目录 `VERSION`） |
| 目标框架 | .NET 10（`net10.0`） |
| 界面框架（Avalonia） | 12.1.5（四个包版本一致） |
| MVVM（模型-视图-视图模型）框架 | CommunityToolkit.Mvvm 8.4.2 |
| 日志 / 资源扩展 | Microsoft.Extensions.Logging(.Console) 10.0.14、System.Resources.Extensions 10.0.14 |
| DBus 协议库 | Tmds.DBus.Protocol 0.95.2 |
| 官方 7-Zip 命令行 `7zz` | 26.05（macOS 通用二进制、Linux x64、Linux ARM64） |
| 支持系统 | Windows、macOS（Apple Silicon）、Linux |
| 压缩后端 | RAR/WinRAR 与官方 `7zz` 命令行程序 |
| 可创建格式 | RAR、7z、ZIP、TAR、GZIP、BZIP2、XZ、WIM |
| 界面语言 | 简体中文、繁體中文、English、日本語、Deutsch |
| 回归测试 | 控制台程序 59 项（须单独指定测试项目的 csproj） |

刻意不引用 `Avalonia.Diagnostics`（Avalonia 12 无匹配发布包）；命令行解析由项目内
`CommandLineHandler` 维护，不依赖 `System.CommandLine` 预览期 API。

## 运行与发布

```bash
dotnet restore
dotnet build BatchCompress.Avalonia.sln --nologo
dotnet run --project BatchCompress.Avalonia.csproj --no-build
scripts/package-macos.sh
```

macOS 通过 `scripts/package-macos.sh` 做自包含单文件发布（`osx-arm64`），
用 `swiftc`（Swift 编译器）编译状态栏帮助进程，组装 `.app` 并签名，默认安装到
`/Applications/BatchCompress.Avalonia.app`；`--dist` 只产出 `.app.zip`。
脚本把 `Assets/压缩.ico` 转成 Avalonia 用的 PNG 与访达（Finder）用的 ICNS，
并从 `VERSION` 写入 `Info.plist` 后回读校验。自包含产物不依赖本机 .NET 运行时。
不要用 `open -n` 启动，会在 Dock 最近使用留下重复记录。

## 打包内容（易错点）

- **随包的只有官方 `7zz` 及其许可/说明文件**，放在 `.app/Contents/MacOS/tools/7zip/macos/`。
- **默认不注入 RAR**。只有用户设置 `BATCHCOMPRESS_RAR_DIR` 且目录内同时有 `rar`、`unrar` 时才复制，
  授权责任由用户承担；Linux x64 构建则复制 `tools/rarLinux/` 的官方完整分发包。
- 早期版本写过「macOS 内置 arm64 RAR 与通用二进制 7zz」，前半句已不成立，勿据此排查。

托盘、拖放、快捷键、通知、窗口状态记忆、完成后关机与取消关机均已实现。
macOS 的托盘由随包原生帮助进程提供（Avalonia 自带 `TrayIcon` 在该平台不生成状态栏项），
切换界面语言时会重启它以刷新菜单文案。
