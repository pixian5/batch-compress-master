# 批量压缩解压工具 - Avalonia 跨平台版本

这是批量压缩解压工具的跨平台重写版本，使用 Avalonia UI 框架构建，支持 Windows、Linux 和 macOS。

## 功能特性

### 核心功能
- ✅ 批量压缩文件和文件夹到 RAR/ZIP 格式
- ✅ 批量解压 RAR/ZIP 文件
- ✅ 支持从文件夹或文本文件加载待处理列表
- ✅ MD5 基于的随机密码生成
- ✅ 自定义密码支持
- ✅ 分卷压缩支持
- ✅ 固实压缩选项
- ✅ 多级压缩率选择（不压缩、轻度、标准、最佳等）
- ✅ 密码查询功能
- ✅ 高级功能解锁机制

### 跨平台特性
- ✅ 跨平台 UI（Avalonia UI）
- ✅ 跨平台压缩引擎集成（RAR/UnRAR）
- ✅ 跨平台文件对话框
- ✅ 跨平台剪贴板支持
- ✅ 跨平台文件夹打开功能
- ✅ 跨平台系统通知（计划中）

### 压缩选项
- 压缩率：不压缩、轻度、快速、标准、较好、最佳
- 固实压缩：减小文件大小
- 分卷压缩：支持 GB/MB/KB 单位
- 已存在文件处理：跳过、更新、覆盖
- 快速打开选项
- 压缩包校验
- 注释文件支持
- 临时目录设置

### 后处理选项
- 处理后删除源文件
- 处理后移动源文件到【已压缩】/【已解压】目录
- 跳过已处理的文件
- 添加附件/联系方式目录
- 处理大小限制
- 完成后关机（仅 Windows）

## 系统要求

### Windows
- Windows 7 或更高版本
- .NET 9.0 运行时
- WinRAR 已安装（用于压缩/解压）

### Linux
- 任何现代 Linux 发行版
- .NET 9.0 运行时
- rar 或 unrar 命令行工具已安装

### macOS
- macOS 10.13 或更高版本
- .NET 9.0 运行时
- rar 或 unrar 命令行工具已安装（可通过 Homebrew 安装）

## 安装依赖

### 安装 .NET 9.0
从 [Microsoft .NET 下载页面](https://dotnet.microsoft.com/download/dotnet/9.0) 下载并安装 .NET 9.0 运行时。

### 安装 RAR/UnRAR

**Windows:**
下载并安装 [WinRAR](https://www.winrar.com/)

**Linux (Ubuntu/Debian):**
```bash
sudo apt-get update
sudo apt-get install rar unrar
```

**Linux (Fedora/RHEL):**
```bash
sudo dnf install rar unrar
```

**macOS (Homebrew):**
```bash
brew install rar
# 或者只安装 unrar
brew install unrar
```

## 构建和运行

### 从源代码构建

1. 克隆仓库
```bash
git clone https://github.com/pixian5/batch-compress-master.git
cd batch-compress-master
```

2. 进入 Avalonia 项目目录
```bash
cd BatchCompress.Avalonia
```

3. 恢复 NuGet 包
```bash
dotnet restore
```

4. 构建项目
```bash
dotnet build
```

5. 运行应用
```bash
dotnet run
```

### 发布独立应用

**Windows (x64):**
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

**Linux (x64):**
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

**macOS (x64):**
```bash
dotnet publish -c Release -r osx-x64 --self-contained
```

**macOS (ARM64):**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained
```

发布的应用将位于 `bin/Release/net9.0/{runtime}/publish/` 目录。

## 使用方法

### 批量压缩

1. 选择来源模式：
   - **从文件夹压缩**：选择包含要压缩的文件/文件夹的目录
   - **从 TXT 读取**：使用文本文件指定文件名和密码

2. 设置输出目录

3. 配置压缩选项：
   - 扩展名（rar、zip 等）
   - 是否使用随机密码
   - 压缩率
   - 是否固实压缩
   - 分卷大小（可选）
   - 已存在文件的处理方式

4. 点击 **压缩** 按钮开始

### 批量解压

1. 选择来源模式：
   - **从文件夹解压**：选择包含压缩包的目录
   - **从 TXT 读取**：使用文本文件指定文件名和密码

2. 设置输出目录

3. 配置选项：
   - 是否使用随机密码（如果压缩时使用了）
   - 已存在文件的处理方式
   - 后处理选项

4. 点击 **解压** 按钮开始

### 密码查询

如果使用了随机密码压缩，可以通过密码查询功能找回密码：

1. 在"文件名（不含扩展名）"输入框输入文件名
2. 点击 **查询密码** 按钮
3. 密码将显示并自动复制到剪贴板

### TXT 文件格式

当使用"从 TXT 读取"模式时，文本文件格式如下：
```
文件1
密码1
文件2
密码2
```

奇数行是文件名（不含扩展名），偶数行是对应的密码。

## 技术架构

### 项目结构
```
BatchCompress.Avalonia/
├── Core/
│   ├── Interfaces/          # 接口定义
│   │   ├── IArchiveEngine.cs
│   │   └── ISystemIntegration.cs
│   ├── Models/              # 数据模型
│   │   └── OperationModels.cs
│   └── Services/            # 业务逻辑服务
│       ├── BatchOperationService.cs
│       ├── PasswordUtility.cs
│       ├── RarArchiveEngine.cs
│       └── SystemIntegrationService.cs
├── ViewModels/              # MVVM 视图模型
│   ├── MainWindowViewModel.cs
│   └── ViewModelBase.cs
├── Views/                   # UI 视图
│   ├── MainWindow.axaml
│   └── MainWindow.axaml.cs
├── App.axaml               # 应用程序定义
├── App.axaml.cs
└── Program.cs              # 程序入口
```

### 关键技术

- **UI 框架**: Avalonia UI 11.3.10
- **MVVM 框架**: CommunityToolkit.Mvvm
- **目标框架**: .NET 9.0
- **压缩引擎**: RAR/WinRAR 命令行工具
- **密码算法**: MD5 哈希（兼容原版）

### 跨平台抽象

应用使用接口抽象了平台特定功能：

- `IArchiveEngine`: 压缩/解压缩引擎接口
- `ISystemIntegration`: 系统集成功能（文件夹打开、剪贴板、通知等）

这使得可以轻松为不同平台实现不同的后端，同时保持相同的 UI 和业务逻辑。

## 与原版 WinForms 版本的差异

### 已实现
- 所有核心压缩/解压功能
- 所有密码功能（随机密码、自定义密码、查询密码）
- 所有压缩选项（压缩率、固实、分卷等）
- 文件列表管理
- 进度显示和日志
- 跨平台支持

### 计划实现
- 系统托盘图标
- 桌面通知/气泡提示
- 拖放文件支持
- 热键支持
- 更完善的错误处理

### 简化/移除的功能
- UAC 提权（不需要，跨平台不适用）
- Windows 特定的注册表操作（已改为跨平台路径搜索）
- 一些 Windows 特定的 UI 提示功能

## 开发

### 前置条件
- .NET 9.0 SDK
- 任何支持 C# 的 IDE（Visual Studio、Visual Studio Code、JetBrains Rider）

### 运行调试
```bash
cd BatchCompress.Avalonia
dotnet run
```

### 添加新功能

1. 定义接口（如果需要跨平台抽象）
2. 在 `Core/Services` 中实现业务逻辑
3. 在 `ViewModels` 中添加 ViewModel 逻辑
4. 在 `Views` 中更新 UI

## 许可证

与原项目相同的许可证。

## 贡献

欢迎贡献！请提交 Pull Request 或创建 Issue。

## 鸣谢

- 原始 WinForms 版本作者
- Avalonia UI 团队
- .NET 社区

## 联系方式

- Email: qgkc520@Gmail.com
- QQ: 2027123419
- 微信: i17269637581
