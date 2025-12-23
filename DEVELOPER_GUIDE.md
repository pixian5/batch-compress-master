# 开发者指南

## 目录

1. [环境配置](#环境配置)
2. [构建项目](#构建项目)
3. [调试技巧](#调试技巧)
4. [代码贡献](#代码贡献)
5. [常见问题](#常见问题)

## 环境配置

### 必需软件

#### 1. Visual Studio 2022 或更高版本

**下载**: https://visualstudio.microsoft.com/

**必需工作负载**:
- .NET 桌面开发
- .NET 8.0 SDK

**安装步骤**:
```bash
# 使用 Visual Studio Installer
1. 运行 Visual Studio Installer
2. 选择"修改"现有安装或"安装"新实例
3. 勾选".NET 桌面开发"工作负载
4. 在"单个组件"中确保选中".NET 8.0 Runtime"
5. 点击"修改"或"安装"
```

#### 2. WinRAR 5.0 或更高版本

**下载**: https://www.rarlab.com/

**注意**: 
- 应用程序运行时需要 WinRAR
- 开发时用于测试压缩/解压功能
- 默认安装到 `C:\Program Files\WinRAR\`

#### 3. .NET 8.0 SDK

**下载**: https://dotnet.microsoft.com/download/dotnet/8.0

**验证安装**:
```bash
dotnet --version
# 应该显示 8.0.x
```

### 可选工具

#### Git
版本控制工具

```bash
# 安装 Git for Windows
winget install Git.Git

# 或从官网下载
# https://git-scm.com/download/win
```

#### Windows Terminal
更好的命令行体验

```bash
# 从 Microsoft Store 安装
winget install Microsoft.WindowsTerminal
```

### IDE配置

#### Visual Studio 设置

1. **代码样式**
   - 工具 → 选项 → 文本编辑器 → C# → 代码样式
   - 使用项目中的 `.editorconfig` 文件

2. **扩展推荐**
   - ReSharper (代码分析和重构)
   - CodeMaid (代码清理)
   - Productivity Power Tools (提高效率)

3. **快捷键**
   - `F5`: 启动调试
   - `Ctrl+F5`: 运行不调试
   - `F9`: 设置/取消断点
   - `F10`: 单步跳过
   - `F11`: 单步进入
   - `Shift+F5`: 停止调试

## 构建项目

### 克隆仓库

```bash
# 使用 HTTPS
git clone https://github.com/pixian5/batch-compress-master.git

# 或使用 SSH
git clone git@github.com:pixian5/batch-compress-master.git

# 进入项目目录
cd batch-compress-master
```

### 使用 Visual Studio 构建

#### 方法1: 通过解决方案

1. 打开 `批量压缩.sln`
2. 选择配置
   - **Debug**: 用于开发和调试
   - **Release**: 用于发布
3. 选择平台
   - **AnyCPU**: 适用于任何CPU架构
   - **x64**: 仅64位系统
4. 点击"生成" → "生成解决方案" (Ctrl+Shift+B)

#### 方法2: 通过项目文件

1. 在解决方案资源管理器中右键点击项目
2. 选择"生成"

#### 输出位置

```
bin/
├── Debug/
│   └── net8.0-windows7.0/
│       ├── 批量压缩与解压.exe
│       ├── 批量压缩与解压.dll
│       ├── 批量压缩与解压.pdb
│       └── ... (其他依赖文件)
└── Release/
    └── net8.0-windows7.0/
        └── ... (发布文件)
```

### 使用命令行构建

#### 基本构建

```bash
# 恢复 NuGet 包
dotnet restore

# 构建项目 (Debug)
dotnet build

# 构建项目 (Release)
dotnet build -c Release
```

#### 清理构建

```bash
# 清理构建输出
dotnet clean

# 清理并重新构建
dotnet clean && dotnet build
```

#### 发布应用

```bash
# 发布为自包含应用 (包含运行时)
dotnet publish -c Release -r win-x64 --self-contained true

# 发布为框架依赖应用 (需要安装 .NET 8.0)
dotnet publish -c Release -r win-x64 --self-contained false

# 发布为单文件
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

### 构建脚本

创建 `build.cmd` 方便构建:

```batch
@echo off
echo 批量压缩解压工具 - 构建脚本
echo ================================

echo [1/4] 清理旧的构建输出...
dotnet clean --nologo

echo [2/4] 恢复 NuGet 包...
dotnet restore --nologo

echo [3/4] 构建 Debug 版本...
dotnet build -c Debug --nologo

echo [4/4] 构建 Release 版本...
dotnet build -c Release --nologo

echo.
echo 构建完成！
echo Debug 输出: bin\Debug\net8.0-windows7.0\
echo Release 输出: bin\Release\net8.0-windows7.0\
pause
```

使用:
```bash
build.cmd
```

## 调试技巧

### 常规调试

#### 设置断点

1. **行断点**: 在代码行左侧边距点击或按 F9
2. **条件断点**: 右键断点 → "条件"
   ```csharp
   // 仅当 successFile > 10 时中断
   if (successFile > 10)
   ```
3. **数据断点**: 监视变量值的变化

#### 监视变量

1. **自动窗口**: 调试 → 窗口 → 自动
2. **局部变量**: 调试 → 窗口 → 局部变量
3. **监视窗口**: 调试 → 窗口 → 监视
4. **即时窗口**: Ctrl+Alt+I

```csharp
// 在即时窗口执行表达式
? successFile + failFile
? File.Exists(@"C:\test.txt")
```

### 异步调试

#### 任务窗口

调试 → 窗口 → 任务

**显示信息**:
- 当前运行的任务
- 任务状态 (运行中、已完成、已取消)
- 任务ID和名称

#### 并行堆栈

调试 → 窗口 → 并行堆栈

**用途**:
- 可视化多个线程的调用堆栈
- 识别死锁和竞态条件

### UI调试

#### 实时可视化树

调试 → 窗口 → 实时可视化树

**功能**:
- 查看UI元素层次结构
- 实时修改属性
- 查看数据绑定

#### 输出窗口

调试 → 窗口 → 输出

```csharp
// 输出调试信息
Debug.WriteLine($"当前处理文件: {fileName}");
Debug.WriteLine($"压缩进度: {successFile}/{totalFiles}");
```

### WinRAR调试

#### 命令行测试

```bash
# 手动测试 WinRAR 命令
"C:\Program Files\WinRAR\WinRAR.exe" A -p"test123" test.rar test.txt

# 查看退出码
echo %ERRORLEVEL%
```

#### 日志记录

在开发时启用详细日志:

```csharp
private void Compression(CancellationToken cancellationToken)
{
    foreach (string file in fileLines)
    {
        Debug.WriteLine($"=== 开始处理: {file} ===");
        
        var command = BuildCommand(file);
        Debug.WriteLine($"命令: {command}");
        
        var exitCode = API.CompressByRar(command);
        Debug.WriteLine($"退出码: {exitCode}");
        
        Debug.WriteLine($"=== 完成处理: {file} ===\n");
    }
}
```

### 性能分析

#### 性能探查器

调试 → 性能探查器

**工具**:
- CPU 使用率
- 内存使用率
- .NET 对象分配跟踪

#### 诊断工具

调试时自动显示

**显示**:
- CPU 使用率
- 内存使用率
- 事件（断点、异常等）

## 代码贡献

### 分支策略

```
main (主分支，稳定版本)
  ├── develop (开发分支)
  │     ├── feature/新功能名称
  │     ├── bugfix/问题描述
  │     └── refactor/重构说明
  └── hotfix/紧急修复
```

### 工作流程

#### 1. Fork 仓库

在 GitHub 上 Fork 项目到你的账户

#### 2. 克隆 Fork

```bash
git clone https://github.com/your-username/batch-compress-master.git
cd batch-compress-master
```

#### 3. 添加上游仓库

```bash
git remote add upstream https://github.com/pixian5/batch-compress-master.git
```

#### 4. 创建功能分支

```bash
# 从 develop 分支创建
git checkout develop
git pull upstream develop
git checkout -b feature/my-new-feature
```

#### 5. 进行开发

```bash
# 编写代码
# 测试功能
# 提交更改

git add .
git commit -m "feat: 添加新功能描述"
```

#### 6. 推送到 Fork

```bash
git push origin feature/my-new-feature
```

#### 7. 创建 Pull Request

1. 在 GitHub 上进入你的 Fork
2. 点击"New Pull Request"
3. 选择 base: develop ← compare: feature/my-new-feature
4. 填写 PR 标题和描述
5. 提交 PR

### 提交信息规范

使用 [Conventional Commits](https://www.conventionalcommits.org/) 规范:

```
<类型>(<范围>): <简短描述>

<详细描述>

<脚注>
```

**类型**:
- `feat`: 新功能
- `fix`: 修复bug
- `docs`: 文档更新
- `style`: 代码格式（不影响功能）
- `refactor`: 重构（不是新功能也不是修复）
- `perf`: 性能优化
- `test`: 添加测试
- `chore`: 构建过程或辅助工具的变动

**示例**:

```bash
# 新功能
git commit -m "feat(compression): 添加并行压缩支持"

# 修复bug
git commit -m "fix(ui): 修复进度条显示错误"

# 文档
git commit -m "docs: 更新README安装说明"

# 重构
git commit -m "refactor: 提取WinRAR命令构建器"
```

### 代码审查清单

提交 PR 前检查:

- [ ] 代码符合项目编码规范
- [ ] 添加必要的注释
- [ ] 更新相关文档
- [ ] 所有测试通过
- [ ] 没有编译警告
- [ ] 功能按预期工作
- [ ] 考虑了边界情况
- [ ] 处理了可能的异常
- [ ] 提交信息清晰明确

### 编码规范

#### 命名约定

```csharp
// 类名: PascalCase
public class CompressionService { }

// 接口: I + PascalCase
public interface IWinRarWrapper { }

// 方法: PascalCase
public void CompressFiles() { }

// 私有字段: _camelCase
private string _fileName;

// 属性: PascalCase
public string FileName { get; set; }

// 常量: PascalCase
private const int MaxRetryCount = 3;

// 局部变量: camelCase
int fileCount = 0;
```

#### 代码格式

```csharp
// 使用花括号（即使单行）
if (condition)
{
    DoSomething();
}

// 每行一个声明
string firstName;
string lastName;

// 适当的空行分隔逻辑块
public void Method()
{
    // 初始化
    var service = new Service();
    var options = new Options();
    
    // 处理
    var result = service.Process(options);
    
    // 返回
    return result;
}
```

#### 注释

```csharp
/// <summary>
/// 压缩指定的文件列表
/// </summary>
/// <param name="files">要压缩的文件列表</param>
/// <param name="outputPath">输出路径</param>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>压缩结果</returns>
public async Task<CompressionResult> CompressAsync(
    IEnumerable<string> files,
    string outputPath,
    CancellationToken cancellationToken)
{
    // 实现
}
```

## 常见问题

### 构建问题

#### Q: 构建失败：找不到 .NET 8.0

**A**: 安装 .NET 8.0 SDK

```bash
# 下载并安装
https://dotnet.microsoft.com/download/dotnet/8.0

# 验证
dotnet --list-sdks
```

#### Q: NuGet包恢复失败

**A**: 清理 NuGet 缓存

```bash
# 清理缓存
dotnet nuget locals all --clear

# 重新恢复
dotnet restore
```

#### Q: 引用错误

**A**: 重新加载项目

```
Visual Studio → 右键项目 → 卸载项目
→ 右键项目 → 重新加载项目
```

### 运行问题

#### Q: 启动时提示需要管理员权限

**A**: 以管理员身份运行 Visual Studio

```
右键 Visual Studio → 以管理员身份运行
```

或修改应用清单:

```xml
<!-- Properties/app.manifest -->
<requestedExecutionLevel level="requireAdministrator" />
```

#### Q: 提示找不到 WinRAR

**A**: 
1. 安装 WinRAR 5.0+
2. 确保安装到默认路径: `C:\Program Files\WinRAR\`
3. 检查注册表项:
   ```
   HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\winrar.exe
   ```

#### Q: 压缩/解压失败

**A**: 检查:
1. WinRAR 是否正确安装
2. 文件路径是否存在
3. 是否有足够的磁盘空间
4. 是否有文件访问权限
5. 查看输出窗口的调试信息

### 调试问题

#### Q: 断点不命中

**A**: 
1. 确保在 Debug 模式构建
2. 清理解决方案并重新构建
3. 检查是否启用了"仅我的代码"调试
   - 工具 → 选项 → 调试 → 常规 → 启用"仅我的代码"

#### Q: 无法查看变量值

**A**: 
1. 检查是否启用了优化 (Debug模式应禁用)
2. 在项目属性中禁用优化:
   - 右键项目 → 属性 → 生成 → 优化代码 (取消勾选)

#### Q: 调试异步代码困难

**A**: 
1. 使用"任务"窗口查看所有任务
2. 使用"并行堆栈"可视化线程
3. 在 `catch` 块设置断点捕获异常

### 代码问题

#### Q: 如何添加新的压缩格式？

**A**: 修改 `exTension` 控件和相关逻辑:

```csharp
// 1. 在UI添加新选项
exTension.Items.Add("7z");

// 2. 在命令构建中处理
string extension = "." + exTension.Text.Trim();

// 3. WinRAR会根据扩展名自动选择格式
```

#### Q: 如何自定义密码算法？

**A**: 修改 `MyMd5` 类或创建新的密码生成器:

```csharp
public static class CustomPasswordGenerator
{
    public static string Generate(string fileName, string salt)
    {
        // 自定义算法
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(fileName + salt);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
```

#### Q: 如何添加新的进度信息？

**A**: 扩展 `CompressionProgressInfo` 类:

```csharp
private class CompressionProgressInfo
{
    // 添加新属性
    public double CompressionRatio { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
    
    // 在 ReportProgress 中处理
}
```

### 性能问题

#### Q: 处理大量文件时UI卡顿

**A**: 确保使用异步方法:

```csharp
// 错误：同步执行
private void Button_Click()
{
    Compression(); // 阻塞UI
}

// 正确：异步执行
private async void Button_Click()
{
    await Task.Run(() => Compression()); // 不阻塞UI
}
```

#### Q: 内存占用过高

**A**: 
1. 使用流而不是一次性加载所有文件
2. 及时释放资源
3. 使用 `using` 语句管理IDisposable对象

```csharp
// 使用流读取大文件列表
await foreach (var file in ReadFilesAsync())
{
    await ProcessAsync(file);
}
```

## 有用的资源

### 官方文档

- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [Windows Forms Documentation](https://docs.microsoft.com/dotnet/desktop/winforms/)
- [WinRAR Command Line](https://www.rarlab.com/rar/rarreg.key)

### 学习资源

- [C# Programming Guide](https://docs.microsoft.com/dotnet/csharp/programming-guide/)
- [Async/Await Best Practices](https://docs.microsoft.com/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
- [Clean Code Principles](https://www.amazon.com/Clean-Code-Handbook-Software-Craftsmanship/dp/0132350882)

### 社区

- [GitHub Issues](https://github.com/pixian5/batch-compress-master/issues)
- [Stack Overflow](https://stackoverflow.com/questions/tagged/c%23)
- [.NET Discord](https://discord.gg/dotnet)

## 联系方式

- **问题报告**: [GitHub Issues](https://github.com/pixian5/batch-compress-master/issues)
- **功能请求**: [GitHub Discussions](https://github.com/pixian5/batch-compress-master/discussions)
- **邮件**: qgkc520@gmail.com

---

感谢你对批量压缩解压工具的贡献！
