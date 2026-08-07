# 当前架构

本仓库根目录即为当前 Avalonia 项目，不包含可构建的 WinForms 应用。目标框架为 `net10.0`，UI 框架为 Avalonia 12.1.1，MVVM 使用 CommunityToolkit.Mvvm 8.4.2。

## 分层

```text
Views/MainWindow.axaml + App.axaml
        -> ViewModels/MainWindowViewModel
        -> Core/Services/BatchOperationService
        -> Core/Services/ArchiveEngineRouter
           -> RarArchiveEngine -> WinRAR/RAR
           -> SevenZipArchiveEngine -> 官方 7zz
        -> Core/Services/ArchiveProcessRunner
```

- `Views/`：Avalonia 视图、拖放、快捷键和窗口状态事件。
- `ViewModels/`：绑定状态、命令、文件选择器和日志集合；不直接拼接 Shell 命令。
- `Core/Models/`：批处理、归档和进度模型。
- `Core/Interfaces/`：归档引擎与系统集成边界。
- `Core/Services/`：批处理编排、归档格式路由、WinRAR 与 7-Zip 定位、密码兼容、平台集成和系统元数据过滤。
- `BatchCompress.Avalonia.Tests/`：无需第三方测试框架的回归测试控制台。

## 命令行入口

`Program` 在初始化 Avalonia 前处理帮助、版本和参数错误。`CommandLineHandler` 是项目内轻量解析器，同时接受 `compress`/`extract` 动词与旧版 `--compress`/`--decompress` 开关，统一规范格式、单位和布尔默认值，并验证互斥模式、输入来源、密码来源、数值范围及文件路径。解析失败返回退出码 2，不会继续启动 GUI。该层不再依赖 System.CommandLine，避免预览期 API 变更影响发布。

`HeadlessBatchRunner` 支持来源目录、精确重复 `--input` 和 TXT 密码清单。`--source` 在压缩时展开目录直接子项，`--input` 中的目录作为单个压缩来源；解压目录只枚举匹配格式的归档。CLI 使用同步进度接收器维持 stdout/stderr 和计数顺序，`--dry-run` 不创建输出目录。

## 归档执行

`ArchiveEngineRouter` 在压缩时按 `ArchiveFormat` 路由：RAR 进入 `RarArchiveEngine`，ZIP/7z 进入 `SevenZipArchiveEngine`；解压时优先根据实际文件名识别 `.zip`、`.7z` 和 `.7z.001` 分卷。附件存在时作为额外根级输入，不存在时仅在暂存目录创建空目录。两个命令构建器都将每个开关与路径生成为独立参数，通用 `ArchiveProcessRunner` 仅通过 `ProcessStartInfo.ArgumentList` 启动进程，并同时异步读取标准输出和错误输出。取消会终止整个进程树。

WinRAR 和 7-Zip 的退出码 `0`、`1` 视为成功或非致命警告，其余返回码为失败。日志保留归档程序的原始 stdout/stderr，可能包含密码；这是用户明确选择的诊断行为，因此不得加入脱敏、替换或掩码逻辑。

7z 支持密码及文件名加密、固实压缩、分卷、压缩级别、临时目录、创建后测试和解压覆盖策略。恢复记录、快速打开和注释文件是 RAR 专属能力，不会传给 7zz。

## 平台能力

`SystemIntegrationService` 使用 `ArgumentList` 调用系统命令：打开目录、通知、关机和取消关机均由运行时操作系统选择实现。系统通知失败不会中断归档任务；关机则可能因权限或系统策略失败，并通过通知报告。

Windows 与 Linux 使用 Avalonia 原生 `TrayIcon`。由于实测 macOS 上 Avalonia 后端未创建状态栏项目，`App` 会启动随 `.app` 打包的 `BatchCompress.StatusBarHelper`。该原生帮助进程以状态栏文字“压”显示，并通过 Bundle ID 直接显示、隐藏或退出主应用；主进程退出后帮助进程自动结束。

## 状态与持久化

窗口大小和位置由视图层保存并在下次启动恢复。普通关闭会退出应用；“隐藏到托盘”和托盘“显示/隐藏”菜单只改变主窗口可见性。所有高级选项默认可用，没有许可证或解锁验证流程。

## 维护约束（0.2.6）

- 归档进程参数必须使用 `ArgumentList`，不得拼接 Shell 命令字符串；stdout/stderr 必须并行异步读取。
- 活动源码新增或修改的注释使用中文，并以 `GPT-5, YYYY-MM-DD：` 标注复杂逻辑的维护日期和原因。
- `desktop.ini`、`.DS_Store`、`Thumbs.db`、Linux 桌面元数据及回收站目录由统一过滤器跳过，不能进入归档任务。
- `tools/7zip/` 中的官方 7zz 和 `tools/WinRAR/rarreg.key` 是项目运行资源，不应被 `.gitignore` 忽略；其他本地密钥、证书和环境文件不得提交。
- 构建输出、发布目录、覆盖率结果、IDE 缓存和平台临时文件由根 `.gitignore` 统一排除。
