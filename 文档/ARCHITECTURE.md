# 当前架构

本仓库根目录即为当前 Avalonia 项目，不包含可构建的 WinForms 应用。目标框架为 `net10.0`，UI 框架为 Avalonia 11.3.10，MVVM 使用 CommunityToolkit.Mvvm。

## 分层

```text
Views/MainWindow.axaml + App.axaml
        -> ViewModels/MainWindowViewModel
        -> Core/Services/BatchOperationService
        -> Core/Services/RarArchiveEngine
        -> Core/Services/WinRarProcessRunner
        -> RAR/WinRAR 可执行文件
```

- `Views/`：Avalonia 视图、拖放、快捷键和窗口状态事件。
- `ViewModels/`：绑定状态、命令、文件选择器和日志集合；不直接拼接 Shell 命令。
- `Core/Models/`：批处理、归档和进度模型。
- `Core/Interfaces/`：归档引擎与系统集成边界。
- `Core/Services/`：批处理编排、WinRAR 定位、密码兼容、平台集成和系统元数据过滤。
- `BatchCompress.Avalonia.Tests/`：无需第三方测试框架的回归测试控制台。

## 归档执行

`WinRarCommandBuilder` 将每个 WinRAR 开关和路径生成一个独立参数；`WinRarProcessRunner` 仅通过 `ProcessStartInfo.ArgumentList` 启动进程，并异步读取标准输出和错误输出。取消操作会终止整个进程树，退出码 `0`、`1` 视为成功，其余返回码为失败。

日志记录保留 WinRAR 的原始 stdout/stderr，可能包含密码；这是用户明确选择的诊断行为，因此不得加入脱敏、替换或掩码逻辑。

## 平台能力

`SystemIntegrationService` 使用 `ArgumentList` 调用系统命令：打开目录、通知、关机和取消关机均由运行时操作系统选择实现。系统通知失败不会中断归档任务；关机则可能因权限或系统策略失败，并通过通知报告。

`App.axaml` 使用 Avalonia 原生 `TrayIcon`。图标显式可见，macOS 启用 `MacOSProperties.IsTemplateIcon`，使状态栏根据深浅菜单栏正确显示图标。

## 状态与持久化

窗口大小和位置由视图层保存并在下次启动恢复。普通关闭会退出应用；“隐藏到托盘”和托盘“显示/隐藏”菜单只改变主窗口可见性。所有高级选项默认可用，没有许可证或解锁验证流程。
