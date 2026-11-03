# Seed 开发方案 - 批量压缩解压工具

## 项目概述

**项目名称**：BatchCompress.Avalonia（批量压缩解压工具）
**当前版本**：0.4.5
**技术栈**：.NET 10 + Avalonia UI 12.1.1 + CommunityToolkit.Mvvm 8.4.2
**目标平台**：Windows、macOS、Linux（跨平台桌面应用）

### 项目定位
这是一个功能完整的跨平台批量压缩/解压桌面工具，支持 RAR、7z、ZIP、TAR、GZ 等多种格式，提供 GUI 和 CLI 双模式，具备密码管理、分卷、恢复记录、附件、后处理等高级功能。

---

## 一、当前架构分析

### 1.1 现有架构分层

```
┌─────────────────────────────────────────────────────────┐
│                    Views (Avalonia AXAML)               │
│  MainWindow.axaml - 主界面、拖放、快捷键、窗口状态       │
└───────────────────────┬─────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────┐
│               ViewModels (MVVM)                         │
│  MainWindowViewModel - 全部界面状态、命令绑定            │
│  OperationTabState - 压缩/解压页独立状态                 │
└───────────────────────┬─────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────┐
│               Core/Services (业务逻辑)                   │
│  BatchOperationService - 批处理编排                     │
│  ArchiveEngineRouter - 格式路由                         │
│  ├─ RarArchiveEngine → WinRAR/RAR                       │
│  └─ SevenZipArchiveEngine → 官方 7zz                    │
│  ArchiveVolumeResolver - 分卷解析                       │
│  PasswordUtility - 密码生成/兼容                        │
│  OutputPathResolver - 输出路径处理                      │
│  SystemIntegrationService - 平台集成                    │
│  SystemMetadataFileFilter - 系统文件过滤                │
│  FileLoggerService - 日志服务                          │
└───────────────────────┬─────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────┐
│               Core/Models + Interfaces                  │
│  OperationModels - 数据模型                             │
│  ArchiveFormatCatalog - 格式目录                        │
│  ArchiveDefaults - 默认配置                             │
│  IArchiveEngine, ISystemIntegration - 接口定义          │
└─────────────────────────────────────────────────────────┘
```

### 1.2 入口流程

1. **Program.Main** - 进程入口，先处理 CLI 参数（--help、--version、compress、extract）
2. **CommandLineHandler** - 轻量命令行解析器，无需第三方库
3. **HeadlessBatchRunner** - 无界面模式执行批处理
4. **WaitForMacOsDisplayLink** - macOS 显示链竞态等待修复
5. **BuildAvaloniaApp** - GUI 模式启动 Avalonia

### 1.3 现有功能清单

| 类别 | 功能 | 状态 |
|------|------|------|
| 压缩格式 | RAR、7z、ZIP、TAR、GZ、BZ2、XZ、WIM | ✅ 完整 |
| 解压格式 | 上述 + ISO、APK、ESD 等 7zz 支持格式 | ✅ 完整 |
| 密码 | 随机密码、自定义密码、旧版兼容密码查询 | ✅ 完整 |
| 分卷 | RAR partN.rar、7z/ZIP/TAR .NNN 数字分卷 | ✅ 完整 |
| 压缩选项 | 压缩级别、固实、恢复记录、快速打开、测试、锁定 | ✅ 完整 |
| 后处理 | 删除源、移动源、跳过已处理、关机/取消关机 | ✅ 完整 |
| 输入源 | 目录扫描、手动列表、TXT 文件、密码本 | ✅ 完整 |
| 附件 | 附件目录、空目录创建、注释文件 | ✅ 完整 |
| 平台集成 | 通知、托盘、打开目录、剪贴板 | ⚠️ macOS 需 StatusBarHelper |
| 本地化 | 中文简/繁、英、日、德 | ✅ 完整 |
| CLI | compress/extract 动词、完整参数、退出码 | ✅ 完整 |

---

## 二、当前问题与技术债务

### 2.1 已知问题

#### P0 - 阻塞性问题
1. **macOS GUI 启动失败** - Avalonia Native RenderTimer 错误 `-6661`
   - 位置：[Program.cs](file:///Users/x/code/Google-compress/Program.cs#L63-L130)
   - 原因：macOS 26 与 Avalonia 12 显示链初始化竞态
   - 当前修复：WaitForMacOsDisplayLink 轮询，但实测仍不稳定
   - 影响：所有 GUI 功能在 macOS 上无法验证

#### P1 - 架构问题
2. **MainWindowViewModel 过于庞大**（1300+ 行）
   - 位置：[MainWindowViewModel.cs](file:///Users/x/code/Google-compress/ViewModels/MainWindowViewModel.cs)
   - 问题：压缩和解压逻辑、状态管理、UI 回调全部耦合在一个类
   - 违反：单一职责原则

3. **无依赖注入容器**
   - 问题：服务直接在 ViewModel 构造函数中 `new`，难以单元测试和替换
   - 位置：MainWindowViewModel 构造函数第 424-429 行

4. **无配置持久化**
   - 问题：窗口位置、用户选项、历史路径等仅在内存中，重启丢失
   - 当前仅有 OperationTabState 在标签切换时临时保存

5. **测试框架自定义**
   - 位置：[BatchCompress.Avalonia.Tests/Program.cs](file:///Users/x/code/Google-compress/BatchCompress.Avalonia.Tests/Program.cs)
   - 问题：手写 Assert、无测试发现、无断言框架、无隔离
   - 建议：迁移到 xUnit/NUnit

#### P2 - 代码质量
6. **日志系统过于简单** - FileLoggerService 仅写文件，无级别、轮转
7. **错误处理粒度粗** - 很多地方 catch(Exception) 只记消息，无分类
8. **进度通知频率硬编码** - 10项/1GB/5分钟，不可配置
9. **命令行解析器手写** - 虽避免预览版依赖，但维护成本高
10. **WinRAR/7z 路径查找** - 仅支持环境变量和固定位置，无自动探测

### 2.2 代码组织问题

```
当前根目录文件过多（40+个）：
- Program.cs、App.axaml.cs、CommandLineHandler.cs、HeadlessBatchRunner.cs 等混在根目录
- Views/ViewModels/Core 分离良好，但入口和 CLI 未分层
- 文档/ 和 docs/ 两个文档目录并存，内容重复风险
- 测试夹具/ 目录包含测试脚本和 GUI 自动化脚本
```

---

## 三、开发路线图

### 3.1 近期目标（v0.5.0 - v0.6.0）

#### 阶段一：修复阻塞问题 + 架构重构（优先级最高）

| 任务 | 描述 | 预计工作量 | 验收标准 |
|------|------|-----------|----------|
| **修复 macOS GUI 启动** | 解决 Avalonia RenderTimer -6661 问题 | 中 | macOS 上 GUI 正常启动、窗口可操作 |
| **ViewModel 拆分** | 将 MainWindowViewModel 拆分为：<br>- CompressionViewModel<br>- DecompressionViewModel<br>- LogsViewModel<br>- MainWindowViewModel（容器） | 中 | 每个 ViewModel < 400 行，职责清晰 |
| **引入依赖注入** | 使用 Microsoft.Extensions.DependencyInjection | 小 | 服务通过构造函数注入，可替换测试Mock |
| **配置持久化** | 实现用户配置保存/加载（JSON） | 中 | 窗口位置、选项、历史路径重启后保留 |
| **迁移到 xUnit** | 将自定义测试迁移到 xUnit + FluentAssertions | 中 | 所有现有测试用例通过，支持 `dotnet test` |

#### 阶段二：核心功能增强

| 任务 | 描述 | 预计工作量 |
|------|------|-----------|
| **日志系统升级** | 实现日志级别、文件轮转、结构化日志 | 小 |
| **进度显示优化** | 进度条、当前文件详情、速度图表 | 中 |
| **错误处理增强** | 自定义异常类型、错误分类、用户友好提示 | 中 |
| **归档预览** | 不解压直接列出归档内容 | 中 |
| **密码管理器** | 保存常用密码、密码本导入/导出加密 | 大 |

### 3.2 中期目标（v0.7.0 - v0.9.0）

| 功能 | 描述 |
|------|------|
| **多任务队列** | 支持排队多个批处理任务、暂停/继续、优先级 |
| **拖拽增强** | 拖拽文件直接添加到列表、拖拽归档直接解压 |
| **校验和验证** | 压缩后自动生成 MD5/SHA 校验文件、解压时验证 |
| **加密文件名** | 7z 文件名加密支持完善、RAR 头加密 |
| **SFX 自解压** | 创建自解压归档（RAR SFX、7z SFX） |
| **云存储集成** | 直接压缩/解压到网盘（可选插件） |
| **批量重命名** | 归档内文件批量重命名规则 |
| **差分/增量备份** | 基于修改时间的增量压缩 |

### 3.3 远期目标（v1.0.0）

| 功能 | 描述 |
|------|------|
| **任务调度** | 定时压缩任务、文件夹监控自动压缩 |
| **插件系统** | 支持第三方格式插件、后处理脚本插件 |
| **主题系统** | 深色/浅色主题、自定义主题 |
| **多语言完善** | 更多语言支持、社区翻译 |
| **签名和公证** | macOS 公证、Windows 代码签名、自动更新 |
| **性能优化** | 并行压缩、大文件内存优化、SSD 优化 |

---

## 四、详细技术方案

### 4.1 架构重构方案

#### 新目录结构

```
BatchCompress.Avalonia/
├── src/
│   ├── BatchCompress.Core/              # 核心业务逻辑（无UI依赖）
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   ├── Services/
│   │   ├── Enums/
│   │   └── BatchCompress.Core.csproj
│   ├── BatchCompress.Cli/               # CLI 模式（无UI依赖）
│   │   ├── CommandLineHandler.cs
│   │   ├── HeadlessBatchRunner.cs
│   │   └── BatchCompress.Cli.csproj
│   └── BatchCompress.Avalonia/          # Avalonia GUI
│       ├── App.axaml
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs
│       │   ├── CompressionViewModel.cs
│       │   ├── DecompressionViewModel.cs
│       │   └── LogsViewModel.cs
│       ├── Views/
│       ├── Services/
│       │   └── GuiSystemIntegration.cs  # GUI特有系统集成
│       └── BatchCompress.Avalonia.csproj
├── tests/
│   ├── BatchCompress.Core.Tests/        # xUnit 核心测试
│   └── BatchCompress.Cli.Tests/         # CLI 测试
└── tools/                               # 第三方工具（7zz、rar等）
```

#### 依赖注入配置

```csharp
// 在 App.axaml.cs 中配置
public static IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();
    
    // 核心服务
    services.AddSingleton<IArchiveEngineRouter, ArchiveEngineRouter>();
    services.AddSingleton<IBatchOperationService, BatchOperationService>();
    services.AddSingleton<IVolumeResolver, ArchiveVolumeResolver>();
    services.AddSingleton<IPasswordService, PasswordUtility>();
    services.AddSingleton<IFormatCatalog, ArchiveFormatCatalog>();
    
    // 平台集成（根据平台注册不同实现）
    if (OperatingSystem.IsWindows())
        services.AddSingleton<ISystemIntegration, WindowsSystemIntegration>();
    else if (OperatingSystem.IsMacOS())
        services.AddSingleton<ISystemIntegration, MacSystemIntegration>();
    else
        services.AddSingleton<ISystemIntegration, LinuxSystemIntegration>();
    
    // 日志
    services.AddLogging(builder => 
        builder.AddFile("logs/batchcompress-{Date}.log")
               .SetMinimumLevel(LogLevel.Information));
    
    // 配置
    services.AddSingleton<IConfigurationService, JsonConfigurationService>();
    
    // ViewModels
    services.AddTransient<MainWindowViewModel>();
    services.AddTransient<CompressionViewModel>();
    services.AddTransient<DecompressionViewModel>();
    
    return services.BuildServiceProvider();
}
```

#### ViewModel 拆分示例

```csharp
// CompressionViewModel - 仅处理压缩相关状态和逻辑
public partial class CompressionViewModel : ViewModelBase
{
    [ObservableProperty] private string _sourcePath = string.Empty;
    [ObservableProperty] private string _outputPath = string.Empty;
    [ObservableProperty] private string _extension = "rar";
    [ObservableProperty] private bool _useRandomPassword = true;
    // ... 其他压缩选项
    
    private readonly IBatchOperationService _batchService;
    private readonly IConfigurationService _config;
    
    public CompressionViewModel(
        IBatchOperationService batchService,
        IConfigurationService config)
    {
        _batchService = batchService;
        _config = config;
        LoadSettings();
    }
    
    [RelayCommand]
    private async Task CompressAsync(CancellationToken ct) { ... }
    
    private void LoadSettings()
    {
        // 从配置加载上次使用的选项
        Extension = _config.Get("Compression.Extension", "rar");
        _useRandomPassword = _config.Get("Compression.UseRandomPassword", true);
    }
}
```

### 4.2 配置持久化方案

使用 `appsettings.json` + 用户配置目录：

- **Windows**: `%APPDATA%/BatchCompress/config.json`
- **macOS**: `~/Library/Application Support/BatchCompress/config.json`
- **Linux**: `~/.config/BatchCompress/config.json`

```csharp
public interface IConfigurationService
{
    T Get<T>(string key, T defaultValue = default!);
    void Set<T>(string key, T value);
    void Save();
    void Load();
}

// 配置项示例
{
  "Window": {
    "Width": 900,
    "Height": 700,
    "Left": 100,
    "Top": 100,
    "Maximized": false
  },
  "Compression": {
    "Extension": "rar",
    "CompressionLevel": 1,
    "UseRandomPassword": true,
    "SolidArchive": true,
    "VolumeSize": "20",
    "VolumeUnit": "GB",
    "OutputPath": "",
    "RecentPaths": []
  },
  "Decompression": {
    "Extension": "rar",
    "OutputPath": "",
    "RecentPaths": []
  },
  "General": {
    "Language": "zh-CN",
    "ShutdownAfterComplete": false,
    "AddEnclosures": true,
    "NotificationInterval": "00:05:00"
  }
}
```

### 4.3 macOS GUI 启动问题修复方案

问题根源分析：
1. macOS 26 (Tahoe) 更改了 CoreVideo 显示链初始化时机
2. Avalonia 12.1.1 的 CVDisplayLink 创建在某些时机返回 kCVReturnInvalidArgument (-6661)
3. 当前 WaitForMacOsDisplayLink 只检查显示链是否可创建，但未等待 NSApplication 完全激活

修复方案：
```csharp
private static void WaitForMacOsDisplayLinkReady()
{
    if (!OperatingSystem.IsMacOS()) return;
    
    // 方案1：等待 NSApplication 激活通知
    // 需要引入 Xamarin.Mac 或使用 NSNotificationCenter
    WaitForNSApplicationActivation();
    
    // 方案2：双重确认（显示链 + 运行循环）
    for (int attempt = 0; attempt < 30; attempt++)
    {
        Thread.Sleep(200);
        if (HasDisplayLink() && TryCreateAvaloniaRuntime())
            return;
    }
    
    // 方案3：如果还是失败，延迟启动（给系统更多时间）
    Thread.Sleep(2000);
}

// 备选：升级到 Avalonia 12.2+ 或 nightly 版本，可能已修复
// 备选：使用 Software Renderer 作为 fallback（软件渲染慢但稳定）
```

### 4.4 测试方案

迁移到 xUnit 后的测试结构：

```csharp
// tests/BatchCompress.Core.Tests/Services/ArchiveVolumeResolverTests.cs
public class ArchiveVolumeResolverTests
{
    private readonly ArchiveVolumeResolver _resolver;
    
    public ArchiveVolumeResolverTests()
    {
        _resolver = new ArchiveVolumeResolver();
    }
    
    [Fact]
    public void Resolve_RarPartVolume_FindsFirstVolumeAndDetectsGap()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFile("rar.part01.rar");
        temp.CreateFile("rar.part02.rar");
        temp.CreateFile("rar.part10.rar");
        
        // Act
        var result = _resolver.Resolve(temp.GetPath("rar.part02.rar"));
        
        // Assert
        result.VolumeKind.Should().Be(ArchiveVolumeKind.RarPart);
        result.FirstVolumePath.Should().EndWith("rar.part01.rar");
        result.Volumes.Should().HaveCount(3);
        result.IsSequenceContiguous.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("seven.7z.001", true)]
    [InlineData("seven.7z.002", false)]
    public void HasRequiredFirstVolume_VariousCases(string filename, bool expected)
    {
        // ...
    }
}
```

测试命令：
```bash
dotnet test tests/BatchCompress.Core.Tests/ --logger "console;verbosity=detailed"
dotnet test --collect:"XPlat Code Coverage"  # 覆盖率
```

---

## 五、关键功能设计方案

### 5.1 归档预览功能

```csharp
public interface IArchivePreviewService
{
    Task<ArchiveContents> ListContentsAsync(
        string archivePath, 
        string? password = null,
        CancellationToken ct = default);
}

public class ArchiveContents
{
    public List<ArchiveEntry> Entries { get; set; } = new();
    public long TotalUncompressedSize { get; set; }
    public long TotalCompressedSize { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsSolid { get; set; }
}

public class ArchiveEntry
{
    public string Path { get; set; } = string.Empty;
    public long CompressedSize { get; set; }
    public long UncompressedSize { get; set; }
    public DateTime Modified { get; set; }
    public uint Crc32 { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsDirectory { get; set; }
}

// 7z 命令：7zz l archive.7z
// RAR 命令：rar lb archive.rar (bare format) 或 rar l -v
```

### 5.2 多任务队列设计

```csharp
public interface IBatchQueueService
{
    Task<Guid> EnqueueAsync(BatchTask task, CancellationToken ct = default);
    Task PauseAsync(Guid taskId);
    Task ResumeAsync(Guid taskId);
    Task CancelAsync(Guid taskId);
    IObservable<QueueStatus> ObserveStatus();
}

public class BatchTask
{
    public Guid Id { get; set; }
    public BatchOperationType Type { get; set; } // Compress/Extract
    public BatchOperationOptions Options { get; set; } = null!;
    public List<string> Inputs { get; set; } = new();
    public int Priority { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
```

### 5.3 进度条 UI 设计

在 AXAML 中添加：
```xml
<!-- 整体进度 -->
<ProgressBar 
    Minimum="0" Maximum="100" 
    Value="{Binding OverallProgress}"
    IsIndeterminate="{Binding IsOperating, Converter={StaticResource InverseBool}}"/>

<!-- 当前文件进度（需要解析归档工具输出） -->
<ProgressBar 
    Minimum="0" Maximum="100" 
    Value="{Binding CurrentFileProgress}"
    IsVisible="{Binding IsCurrentFileProgressAvailable}"/>

<StackPanel Orientation="Horizontal" Spacing="8">
    <TextBlock Text="{Binding CurrentFile}"/>
    <TextBlock Text="{Binding ProcessingSpeedDisplay}"/>
    <TextBlock Text="{Binding RemainingTimeDisplay}"/>
</StackPanel>
```

需要解析 7zz/RAR 的进度输出：
- 7zz: `  0% 1 - file.txt` 或 ` 45% 23 - bigfile.iso`
- RAR: `  0%` 到 `100%` 带文件名

---

## 六、CI/CD 和发布方案

### 6.1 GitHub Actions 工作流

在 `.github/workflows/` 下创建：

1. **build.yml** - 每次 PR/push 构建和测试
2. **release.yml** - tag 推送时自动构建多平台包并发布 Release
3. **publish-docs.yml** - 文档更新时发布 GitHub Pages

构建矩阵：
```yaml
jobs:
  build:
    strategy:
      matrix:
        os: [windows-latest, macos-latest, ubuntu-latest]
        rid: [win-x64, osx-arm64, osx-x64, linux-x64, linux-arm64]
        exclude:
          - os: windows-latest
            rid: osx-arm64
          # ... 合理组合
```

### 6.2 macOS 打包优化

当前 `scripts/package-macos.sh` 增强：
- ✅ 创建 .app bundle
- ✅ 注入 Info.plist
- ⬜ 添加代码签名（需要 Apple Developer ID）
- ⬜ 公证（notarization）
- ⬜ 创建 dmg 安装镜像
- ⬜ 自动更新支持（Sparkle 或 Avalonia Updater）

### 6.3 版本号管理

按照用户规则：每次修改 +0.0.1，满十进一
- 当前：0.4.5
- 下一次修改：0.4.6
- 0.4.9 之后：0.5.0

可以使用 Nerdbank.GitVersioning 自动管理，或手动更新 VERSION 文件。

---

## 七、文档和知识管理

### 7.1 文档整理

当前有两个文档目录：
- `docs/` - 近期变更记录（2026-08-xx）
- `文档/` - 历史文档、设计文档、参考

建议：
1. **合并**：将两个目录合并为 `docs/`
2. **分类**：
   - `docs/getting-started/` - 快速开始、安装
   - `docs/user-guide/` - 用户手册
   - `docs/developer/` - 开发文档、架构
   - `docs/changelog/` - 变更日志
   - `docs/archive/` - 历史文档归档

### 7.2 需要补充的文档

- [ ] **贡献指南** (CONTRIBUTING.md) - 如何参与开发
- [ ] **API 文档** - 核心服务接口 XML 文档
- [ ] **构建指南** - 各平台详细构建步骤
- [ ] **故障排查** - 常见问题和解决方案
- [ ] **格式兼容性矩阵** - 各格式支持的功能对比

---

## 八、性能优化方向

### 8.1 大文件处理
- 当前整个文件列表加载到内存，对百万级文件场景优化
- 流式处理目录扫描，边扫描边处理
- 内存映射文件计算校验和

### 8.2 并行处理
- 多个独立小文件可以并行压缩（需要考虑磁盘IO瓶颈）
- 7zz 多线程参数 `-mmt=on` 已支持，但应用层面可调度
- 后处理（删除、移动）可并行

### 8.3 磁盘IO优化
- 大文件顺序读写，避免随机访问
- 临时目录和输出目录同分区时使用移动而非复制
- SSD/HDD 检测和不同策略

---

## 九、安全考虑

### 9.1 密码安全
- ⚠️ 当前日志明文记录密码（设计如此，用户明确要求）
- 建议：提供"不记录密码到日志"选项（默认关，可开启）
- 密码本文件加密存储
- 内存中密码使用 SecureString（如果 .NET 10 仍支持）

### 9.2 路径遍历攻击
- 解压时验证文件路径，防止 `../../../etc/passwd` 类攻击
- 7zz 和 RAR 本身有保护，但应用层再校验

### 9.3 恶意归档
- 解压炸弹（zip bomb）检测 - 检查压缩比
- 符号链接处理 - 防止链接到系统文件

---

## 十、优先开发顺序建议

### 立即开始（本周）
1. ✅ 修复 macOS GUI 启动问题（最高优先级，阻塞所有 macOS 测试）
2. 拆分 MainWindowViewModel，降低耦合
3. 引入 DI 容器，为测试做准备

### 近期（2周内）
4. 配置持久化（用户体验提升明显）
5. 迁移测试到 xUnit
6. 日志系统升级
7. 整理文档结构

### 中期（1-2个月）
8. 归档预览功能
9. 进度条和UI优化
10. 错误处理和用户提示优化
11. CI/CD 完善

### 长期
12. 多任务队列
13. 密码管理器
14. 自动更新
15. 代码签名和公证

---

## 十一、代码规范约定

基于现有代码观察：

### C# 编码规范
- 使用文件范围命名空间（`namespace X;`）
- 注释使用中文，复杂逻辑用 `// GPT-5, YYYY-MM-DD：` 标注
- 使用 CommunityToolkit.Mvvm 的 `[ObservableProperty]`、`[RelayCommand]`
- 可空引用类型启用（`<Nullable>enable</Nullable>`）
- 使用 `ArgumentList` 传递进程参数，绝对不拼接 Shell 字符串
- 异步方法后缀 `Async`
- 接口以 `I` 开头

### Git 提交规范（用户要求）
- Commit 信息使用中文
- 每次修改完成后提交并推送
- 不必要的文件加入 .gitignore
- 版本号每次修改 +0.0.1

### 平台注意事项
- macOS 应用打包后复制到 /Applications，前台运行
- 服务器程序使用 GitHub Actions 自动部署
- 不在本机安装 MySQL 等服务，使用 Docker
- MacBook M5 (arm64)，注意原生二进制和 Rosetta 区别

---

## 附录：现有核心文件索引

| 文件 | 职责 | 行数 |
|------|------|------|
| [Program.cs](file:///Users/x/code/Google-compress/Program.cs) | 入口、CLI/GUI分流、macOS显示链修复 | ~150 |
| [MainWindowViewModel.cs](file:///Users/x/code/Google-compress/ViewModels/MainWindowViewModel.cs) | 主界面全部状态和逻辑 | ~1350 |
| [BatchOperationService.cs](file:///Users/x/code/Google-compress/Core/Services/BatchOperationService.cs) | 批处理编排核心 | - |
| [ArchiveEngineRouter.cs](file:///Users/x/code/Google-compress/Core/Services/ArchiveEngineRouter.cs) | RAR/7z 格式路由 | - |
| [ArchiveVolumeResolver.cs](file:///Users/x/code/Google-compress/Core/Services/ArchiveVolumeResolver.cs) | 分卷解析、连续性检查 | - |
| [CommandLineHandler.cs](file:///Users/x/code/Google-compress/CommandLineHandler.cs) | CLI 参数解析 | - |
| [MainWindow.axaml](file:///Users/x/code/Google-compress/Views/MainWindow.axaml) | GUI 布局定义 | - |
| [SystemIntegrationService.cs](file:///Users/x/code/Google-compress/Core/Services/SystemIntegrationService.cs) | 平台集成（通知、关机等） | - |
| [PasswordUtility.cs](file:///Users/x/code/Google-compress/Core/Services/PasswordUtility.cs) | 密码生成、旧版兼容 | - |

---

**方案编制日期**：2026-08-10
**方案版本**：1.0
