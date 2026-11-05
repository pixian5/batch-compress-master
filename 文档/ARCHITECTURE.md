# 当前架构

> 状态：**2026-11-05 刷新到 `VERSION 0.7.6`**，依据源码与命令行实测核对。
> 更早的版本号、依赖数字与「裸 RAR 二进制」表述已作废；历史升级快照见
> [Avalonia 12 与命令行解析器更新](2026-08-06-Avalonia12与命令行解析器更新.md)（仅追溯）。
> 完整运行链路见 [项目运行机制分析](../docs/20261105-2243-项目运行机制分析-Claude-Opus.md)，
> 可复用避坑见 [经验索引](../经验/README.md)。

## 技术栈与依赖现状

| 项 | 当前值 | 核对来源 |
| --- | --- | --- |
| 目标框架 | `net10.0` | `BatchCompress.Avalonia.csproj` |
| 界面框架（Avalonia） | 12.1.5（Avalonia / Desktop / Themes.Fluent / Fonts.Inter 四包一致） | csproj |
| MVVM（模型-视图-视图模型）框架 | CommunityToolkit.Mvvm 8.4.2 | csproj |
| 日志 | Microsoft.Extensions.Logging / .Console 10.0.14 | csproj |
| 资源扩展 | System.Resources.Extensions 10.0.14 | csproj |
| DBus 协议库 | Tmds.DBus.Protocol 0.95.2 | csproj |
| 官方 7-Zip 命令行 `7zz` | 26.05（macOS 通用二进制 / Linux x64 / Linux ARM64 三份） | `tools/7zip/` |
| 开发机 .NET 开发工具包（SDK） | 10.0.403（用户目录安装，非系统级） | `dotnet --list-sdks` |
| 版本号唯一来源 | 根目录 `VERSION` = `0.7.6` | csproj / `--version` / `Info.plist` |

刻意不引用 `Avalonia.Diagnostics`（Avalonia 12 无匹配发布包）；命令行解析为项目内自研
`CommandLineHandler`，不依赖 `System.CommandLine` 预览期 API。回归测试为控制台程序，当前 **59 项**全过。

## 分层

```text
Program.cs（唯一入口，早判定分流）
  ├─ HeadlessBatchRunner（无界面 / CLI）
  └─ App.axaml.cs + Views/MainWindow.axaml（图形界面）
        └─ ViewModels/MainWindowViewModel + OperationSession
              ↓ 两套入口共用 ↓
        Core/Services/SourceCollector（来源规范化）
        Core/Services/BatchOperationService（业务编排）
              ↓
        Core/Services/Pipeline/BatchExecutionPipeline<T>（执行管线）
              ├─ CompressionItemAdapter / DecompressionItemAdapter（条目级差异）
              └─ PostProcessService（删除 / 移动 / 改名）
              ↓
        ArchiveEngineRouter（按格式路由）
              ├─ RarArchiveEngine  + WinRarCommandBuilder   → 外部 rar / WinRAR
              └─ SevenZipArchiveEngine + SevenZipCommandBuilder → 官方 7zz
              ↓
        ArchiveProcessRunner（唯一进程边界，ArgumentList 传参）
              ↓
        外部 7zz / rar 子进程
```

| 目录 | 职责 |
| --- | --- |
| `Views/` | Avalonia 视图、拖放、快捷键、窗口状态事件。 |
| `ViewModels/` | 绑定状态、命令、文件选择器、日志集合、操作会话；**不拼接 Shell 命令**。 |
| `Core/Models/` | 批处理、归档、进度模型与格式目录。 |
| `Core/Interfaces/` | 归档引擎与系统集成边界。 |
| `Core/Services/` | 编排、路由、命令构建、进程执行、密码兼容、日志、本地化支持、附件配置、结构化输出。 |
| `Core/Services/Pipeline/` | 通用批量执行管线与压缩/解压适配器，是批次 0-6 重构收敛出的骨架。 |
| `BatchCompress.Avalonia.Tests/` | 回归测试控制台，**不在 `.sln` 内**，须单独指定 csproj 运行。 |

## 命令行入口

`Program.Main` 是唯一入口（`[STAThread]`）：先过滤 macOS 访达（Finder）附带的 `-psn_*` 参数；
`--help` 与 `--version` 在初始化界面框架**之前**返回；参数解析失败返回 `2`，不启动图形界面；
解析成功后立刻同步界面语言，使无界面运行期的服务层消息与终端输出同语言。

`CommandLineHandler` 接受 `compress` / `extract` / `gui` 动词与旧式 `--compress` / `--decompress` 开关，
校验互斥模式、来源、密码来源、数值范围与文件路径。支持五语言（简体中文、繁體中文、English、
日本語、Deutsch）、`--concurrency/-j 1-16` 受控并发、`--output-format json` 结构化事件流（NDJSON，
逐行 JSON），以及日志保留天数等选项。

`HeadlessBatchRunner` 支持来源目录、可重复 `--input`、TXT 密码本；来源经 `SourceCollector`
统一规范化（去尾部斜杠、保序去重、过滤系统元数据）。`--dry-run` 只列任务、不创建输出目录。

退出码：`0` 成功 / `1` 任务失败 / `2` 参数错误 / `130` Ctrl+C 协作式取消。
**实测边界**：试运行会提前返回，即使加了 `--output-format json` 也不产生事件流，
DRY-RUN 清单写在标准错误（stderr）；只有真实执行时标准输出才是 NDJSON。

## 归档执行

`ArchiveEngineRouter` 创建时按规范化格式路由：RAR 进 `RarArchiveEngine`，其余可创建格式
（7z、ZIP、TAR、GZIP、BZIP2、XZ、WIM）进 `SevenZipArchiveEngine`；解压先由
`ArchiveVolumeResolver` 按实际文件名识别种类与分卷再分派。

两个命令构建器把每个开关与路径生成为独立参数，`ArchiveProcessRunner` 只用
`ProcessStartInfo.ArgumentList` 启动进程，stdout（标准输出）/stderr（标准错误）并行异步读取，
取消时 `Kill(entireProcessTree: true)` 终止整棵子进程树。

`BatchExecutionPipeline` 负责循环、取消、计数、并发与关机：
并发度大于 1 时信号量限流，共享状态由同一把锁保护并上报克隆快照；
「大小上限」用闸门令牌只停**未启动**任务（不杀在途引擎），「用户取消」用引擎令牌传播到全部在途任务；
系统关机只在非取消的正常结束路径触发恰好一次。归档成功与后处理成功分别计数。

外部程序退出码 `0`、`1` 视为成功或非致命警告，其余为失败。`7zz` 没有「创建后自动测试」开关，
创建成功后需显式执行 `t` 命令，分卷归档以实际首卷为入口。日志保留归档命令与原始 stdout/stderr，
**包含明文密码且不脱敏**，属用户明确选择的诊断行为；日志文件权限 0600，多代轮转（第 1 代纯文本、
第 2 代起 Gzip 压缩），不得作为公开数据处理。

7z 支持密码及文件名加密、固实压缩、分卷、压缩级别、临时目录、创建后测试和覆盖策略。
恢复记录、快速打开、锁定、注释文件是 RAR 专属能力，不会传给 `7zz`。

## 平台能力

`SystemIntegrationService` 用 `ArgumentList` 调用系统命令：打开目录、通知、关机与取消关机按运行系统选择实现。
通知失败不中断归档；关机可能因权限或系统策略失败并通过通知报告。

macOS 图形界面启动前会轮询 CoreVideo 的 `CVDisplayLink`（显示链接口）连续两次成功，
绕开显示器唤醒/切换时 Avalonia 建窗抛 `RenderTimer -6661` 的竞态；命令行模式不经过。
Windows 与 Linux 使用 Avalonia 原生 `TrayIcon`；macOS 实测不生成状态栏项，
由随 `.app` 打包的 Swift 原生进程 `BatchCompress.StatusBarHelper` 显示「压」字菜单，
其文案启动时固定，切换界面语言时主程序会结束旧进程并带新文案重启。主进程退出后帮助进程自动结束。

`7zz` 按「显式配置 → 程序目录 `tools/7zip/<平台>/7zz` → 当前工作目录同类路径 → 系统路径」查找并校验架构。
csproj 按目标平台标识（RID，RuntimeIdentifier）把对应目录复制进产物；macOS 打包脚本额外放到
`.app/Contents/MacOS/tools/7zip/macos/` 固定相对路径，因为访达启动时 `PATH` 不可靠。

## 状态与持久化

窗口大小与位置由视图层保存并恢复。普通关闭窗口即退出应用；「隐藏到托盘」和托盘「显示/隐藏」
只改主窗口可见性。所有高级选项默认可用，没有许可证或解锁验证流程。附件联系方式由
`EnclosureConfigService` 按「自定义路径 > 应用目录相邻 `enclosures.txt` > 用户应用数据目录」解析，
无配置时回退默认模版，IO 失败不阻断启动。

## 维护约束（当前 0.7.6）

- 归档进程参数必须逐项写入 `ArgumentList`，不得拼接 Shell 命令字符串；stdout/stderr 必须并行异步读取。
- 活动源码新增或修改的注释使用中文，并以「模型名, YYYY-MM-DD: 为什么这样改」标注非显而易见逻辑的动机。
- `desktop.ini`、`.DS_Store`、`Thumbs.db`、Linux 桌面元数据与回收站目录由统一过滤器跳过，不能进入归档任务。
- 版本号唯一来源是根目录 `VERSION`；csproj 读它写程序集信息，`--version` 读程序集，
  `scripts/package-macos.sh` 写入并回读校验 `Info.plist`。**只有修改源代码才把版本号加 `0.0.1`，
  改文档不触发加版本**（2026-11-05 用户确认）。
- 用户可见消息必须五语言本地化；仅写入 `ILogger` 的内部诊断可保留固定文案。
  归档程序原始输出的分类使用 `OperationProgressInfo.IsCommandOutput` 标记，不对消息文本做前缀匹配。
- `tools/7zip/` 的官方 `7zz` 是运行资源，不应被 `.gitignore` 忽略。
  `tools/rarLinux/` 现为官方 `rarlinux-x64-723` **完整未修改分发包（实测 11 个文件）**，
  `linux-x64` 构建会整体复制；macOS 私有打包需用户自行提供 `BATCHCOMPRESS_RAR_DIR`，
  授权责任由用户承担。许可依据见 [RAR 二进制许可风险说明](../docs/20260929-2351-RAR二进制许可风险说明-未标注.md)。
- 新增归档能力须同时更新对应命令构建器、格式目录、五语言文案与回归测试；
  新扫描入口必须调用 `SystemMetadataFileFilter.ShouldSkip`。
- 构建输出、发布目录、日志、IDE 缓存与平台临时文件由根 `.gitignore` 统一排除。

## RAR 与 7z 后端边界

格式路由必须在创建 `ArchiveOptions` 后立即进入对应引擎：RAR 只进
`RarArchiveEngine`/`WinRarCommandBuilder`，7z 与 ZIP 只进
`SevenZipArchiveEngine`/`SevenZipCommandBuilder`。两个构建器都用 `ArgumentList`，但参数集合不同：
WinRAR 的 `-ms`、`-rr`、`-k`、`-z`、`-qo` 不得传给 `7zz`；`7zz` 的 `-t7z`、`-tzip`、`-mx`、
`-mhe`、`-mem`、`-ao*` 不得传给 WinRAR。

默认文件分类与两套后端的能力限制记录在 [RAR 与 7z 后端边界](2026-08-07-RAR与7z边界.md)。

## 验证入口

```bash
dotnet build BatchCompress.Avalonia.sln --nologo
dotnet run --project BatchCompress.Avalonia.Tests/BatchCompress.Avalonia.Tests.csproj --nologo
./测试夹具/经验验证/verify.sh
scripts/package-macos.sh --check
```

可复跑的 15 项实测（`测试夹具/经验验证/verify.sh`）覆盖版本同源、退出码、试运行边界、
元数据过滤、尾部斜杠、带密码压缩解压往返、日志权限、结构化输出与打包自检等。
完整说明见 [经验索引](../经验/README.md)。
