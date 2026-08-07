# Avalonia 版本说明

这是当前应用的唯一可构建版本，项目根目录就是 Avalonia 解决方案。历史 WinForms 资料仅保留在文档中作迁移参考。

| 项目 | 当前值 |
| --- | --- |
| 应用版本 | 0.2.8 |
| 目标框架 | .NET 10 (`net10.0`) |
| UI | Avalonia 12.1.1 |
| MVVM | CommunityToolkit.Mvvm 8.4.2 |
| 支持系统 | Windows、macOS、Linux |
| 压缩后端 | RAR/WinRAR 与官方 7-Zip 命令行 |
| 可创建格式 | RAR、ZIP、7z |

运行开发版：`dotnet run --project BatchCompress.Avalonia.csproj`。

macOS 通过 `scripts/package-macos.sh` 发布为自包含 Apple Silicon `.app`，并安装到 `/Applications/BatchCompress.Avalonia.app`。该脚本还会将 `Assets/压缩.ico` 转换为 Avalonia 使用的 PNG 和 Finder 使用的 ICNS。

托盘、拖放、快捷键、通知、窗口状态记忆、完成后关机与取消关机均已实现。RAR 使用 RAR 后端，ZIP/7z 使用项目内官方 7zz；macOS 内置 arm64 RAR 与 universal 7zz。随机密码可按 `a.rar`（默认）或 `a` 派生，分卷会先归一为逻辑归档名。附件存在时进入归档根目录，不存在时在归档根目录创建空目录。锁定归档不能与更新已有归档同时使用；跳过已有归档返回成功并计入 Skipped。移动目标冲突不再删除目标，后处理失败单独统计并使 CLI 返回失败。命令行解析由项目内轻量解析器维护，不再依赖 System.CommandLine 预览期 API。
