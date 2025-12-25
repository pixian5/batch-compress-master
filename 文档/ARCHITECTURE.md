# 批量压缩解压工具 - 架构文档

## 项目概述

这是一个基于 WinRAR 的批量压缩和解压缩 Windows 桌面应用程序，使用 C# 和 .NET 8 Windows Forms 开发。

## 技术栈

- **开发框架**: .NET 8.0 (Windows 7.0+)
- **UI框架**: Windows Forms
- **压缩引擎**: WinRAR 5.0+
- **编程语言**: C# 12
- **开发工具**: Visual Studio 2022+
- **目标平台**: Windows 7/8/10/11 (x64)

## 项目结构

```
批量压缩解压/
├── Program.cs                      # 应用程序入口点，处理管理员权限
├── 批量压缩解压文件.cs              # 主窗体业务逻辑
├── 批量压缩解压文件.Designer.cs    # 主窗体UI设计器生成代码
├── 批量压缩解压文件.resx           # 主窗体资源文件
├── API.cs                         # WinRAR API封装
├── md5.cs                         # MD5密码生成工具
├── Win32Utility.cs                # Windows API辅助工具
├── GlobalSuppressions.cs          # 代码分析抑制配置
├── Properties/                    # 应用程序属性
│   ├── Resources.Designer.cs     # 资源设计器代码
│   ├── Resources.resx            # 应用程序资源
│   ├── Settings.Designer.cs      # 设置设计器代码
│   └── Settings.settings         # 应用程序设置
├── 压缩.ico                       # 应用程序图标
├── 7z.ico                        # 7z格式图标
├── WinRAR.chm                    # WinRAR帮助文档
└── 批量压缩.sln                   # Visual Studio解决方案文件
```

## 核心组件

### 1. Program.cs - 应用程序入口

**职责**:
- 应用程序启动点
- 检查并请求管理员权限
- 初始化Windows Forms应用

**关键功能**:
- 使用 `WindowsIdentity` 检查当前用户权限
- 如果不是管理员，使用 UAC 提权启动
- 启动主窗体 `Mainform`

### 2. 批量压缩解压文件.cs (Mainform) - 主窗体

**职责**:
- 用户界面主逻辑
- 文件/文件夹选择和管理
- 压缩/解压操作控制
- 进度显示和结果反馈

**核心类和结构**:

#### CompressionProgressInfo 类
```csharp
private class CompressionProgressInfo
{
    public string CurrentFile { get; set; }        // 当前处理的文件
    public int SuccessCount { get; set; }          // 成功数量
    public int FailCount { get; set; }             // 失败数量
    public int IgnoreCount { get; set; }           // 忽略数量
    public int NonExistCount { get; set; }         // 不存在文件数量
    public double CompressedSize { get; set; }     // 已压缩大小(GB)
    public string Message { get; set; }            // 消息内容
    public bool IsError { get; set; }              // 是否为错误消息
}
```

#### 主要方法

**异步操作方法**:
- `BtnRun_Click()` - 压缩按钮点击事件（异步）
- `Btndepress_Click()` - 解压按钮点击事件（异步）
- `Compression(CancellationToken)` - 压缩核心逻辑
- `Decompression(CancellationToken)` - 解压核心逻辑
- `ReportProgress()` - 进度报告更新UI

**文件管理方法**:
- `AddFileToListFromPath()` - 从文件夹添加文件到列表
- `AddFileToListFromTxt()` - 从TXT文件读取文件列表
- `RarPart()` - 处理分卷压缩文件

**UI交互方法**:
- `ButtonFrom_Click()` - 选择源文件/文件夹
- `Btn2_Click()` - 选择目标文件夹
- `BtnrRefresh_Click()` - 刷新文件列表
- `Btnreset_Click()` - 清除所有内容

### 3. API.cs - WinRAR接口

**职责**:
- 封装WinRAR命令行操作
- 处理压缩/解压执行
- 管理WinRAR进程

**核心方法**:

#### RarPath()
```csharp
public static string RarPath()
```
- 从注册表获取WinRAR安装路径
- 键值: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\winrar.exe`

#### CompressByRar()
```csharp
public static int CompressByRar(string shellArguments)
```
- 执行WinRAR命令
- 参数: WinRAR命令行参数
- 返回: WinRAR退出码
  - 0: 成功
  - 1: 警告
  - 2-11: 各种错误
  - -2: 未安装WinRAR
  - -3: 执行异常

#### CompressByRarAsync()
```csharp
public static async Task<int> CompressByRarAsync(string shellArguments)
```
- 异步执行压缩操作
- 使用 `Task.Run()` 包装同步方法

### 4. md5.cs - 密码生成工具

**职责**:
- 生成MD5哈希密码
- 支持多种编码格式

**方法**:
- `MD5gb2312()` - GB2312编码的MD5
- `MD5UTF874()` - UTF-8编码的MD5（取前4位）
- `MD5UTF878()` - UTF-8编码的MD5（取前7-8位）

**密码生成逻辑**:
```
密码 = MD5UTF878(文件名 + "592ptt1314") + MD5UTF878(文件名 + "592pnn1314")
```

### 5. Win32Utility.cs - Windows API工具

**职责**:
- 封装Windows Native API
- 提供UI增强功能

**功能**:
- `SetCueText()` - 设置文本框提示文字（水印）

## 数据流

### 压缩流程

```
用户选择源文件/文件夹
    ↓
添加到待压缩列表 (rtbSource)
    ↓
配置压缩选项（密码、分卷、压缩率等）
    ↓
点击【压缩】按钮
    ↓
异步执行 Compression()
    ↓
遍历文件列表
    ↓
生成WinRAR命令参数
    ↓
调用 API.CompressByRar()
    ↓
报告进度 (ReportProgress)
    ↓
处理压缩结果（移动/删除源文件）
    ↓
显示完成信息
```

### 解压流程

```
用户选择压缩文件源
    ↓
添加到待解压列表 (rtbSource)
    ↓
配置解压选项（密码、目标路径等）
    ↓
点击【解压】按钮
    ↓
异步执行 Decompression()
    ↓
遍历压缩文件列表
    ↓
处理分卷文件
    ↓
生成密码（随机或自定义）
    ↓
生成WinRAR解压命令
    ↓
调用 API.CompressByRar()
    ↓
报告进度
    ↓
处理解压结果（移动/删除源文件）
    ↓
显示完成信息
```

## 关键设计模式

### 1. 异步编程模式 (Async/Await)
- 使用 `async/await` 防止UI冻结
- `Task.Run()` 在后台线程执行耗时操作
- `Invoke()` 跨线程更新UI

### 2. 进度报告模式 (IProgress<T>)
- 使用 `IProgress<CompressionProgressInfo>` 报告进度
- 将进度信息从后台线程传递到UI线程
- 解耦业务逻辑和UI更新

### 3. 取消令牌模式 (CancellationToken)
- 使用 `CancellationTokenSource` 支持取消操作
- 在长时间循环中检查 `IsCancellationRequested`
- 优雅地中断操作

### 4. 外观模式 (Facade)
- `API.cs` 封装复杂的WinRAR命令行操作
- 简化主窗体的调用逻辑

## 线程模型

### UI线程
- 处理所有用户界面交互
- 响应按钮点击、文本输入等事件
- 显示进度和结果

### 后台线程
- 执行压缩/解压操作
- 处理文件系统操作
- 调用WinRAR进程

### 线程同步
- 使用 `Invoke()` 从后台线程更新UI
- 使用 `IProgress<T>` 报告进度到UI线程
- 所有UI控件访问都在UI线程上执行

## 错误处理

### 异常捕获
- 顶层 `try-catch` 捕获操作异常
- `OperationCanceledException` 处理用户取消
- 显示友好的错误消息给用户

### WinRAR返回码处理
- 0-1: 成功或警告
- 2-11: 各种错误情况
- -2: 未安装WinRAR
- -3: 执行异常

### 文件系统错误
- 检查文件/文件夹是否存在
- 处理权限不足
- 处理文件锁定

## 性能优化

### 1. UI响应性
- 所有耗时操作异步执行
- 使用 `Task.Run()` 避免阻塞UI线程

### 2. 批量操作
- 支持批量压缩/解压多个文件
- 逐个处理文件，支持中断

### 3. 进度更新
- 实时更新进度信息
- 显示当前处理文件和统计数据

## 安全考虑

### 1. 管理员权限
- 程序启动时检查权限
- 需要时请求管理员权限（UAC）

### 2. 密码保护
- 支持随机密码生成
- MD5哈希确保密码复杂性
- 每个文件独立密码

### 3. 文件操作
- 检查文件是否存在再操作
- 支持覆盖/跳过/更新选项
- 提供删除/移动源文件选项

## 扩展性

### 支持的压缩格式
- RAR
- ZIP
- 7Z（解压，不支持压缩）
- 其他WinRAR支持的格式

### 配置选项
- 压缩率（0-5级）
- 固实压缩
- 分卷大小
- 恢复记录
- 快速打开信息
- 注释文件
- 临时文件夹

## 依赖关系

### 外部依赖
- **WinRAR 5.0+**: 必须安装，用于实际压缩/解压操作
- **.NET 8.0 Runtime**: Windows Forms应用运行时

### 内部依赖
- `Mainform` → `API`
- `Mainform` → `MyMd5`
- `Mainform` → `Win32Utility`
- `API` → Windows Registry
- `Program` → `Mainform`

## 配置管理

### 注册表
- 读取WinRAR安装路径

### 用户配置
- 默认路径设置
- 压缩选项预设
- UI状态（窗口大小等）

## 日志和监控

### 进度显示
- 实时显示当前处理文件
- 成功/失败/忽略计数
- 已处理文件大小

### 结果输出
- 成功列表 (rtbOk)
- 失败列表 (rtbFail)
- 命令历史 (rtbCMD)

### 系统托盘
- 显示进度提示
- 显示完成状态
- 支持双击显示/隐藏窗口

## 总结

这是一个设计合理的Windows Forms应用程序，采用了现代的异步编程模式，具有良好的用户体验和错误处理机制。通过封装WinRAR命令行工具，实现了强大的批量压缩和解压功能。
