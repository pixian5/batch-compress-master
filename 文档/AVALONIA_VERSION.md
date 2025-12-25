# Avalonia 跨平台版本说明

## 项目结构

本仓库现在包含两个版本的批量压缩解压工具：

1. **原版 WinForms 版本**（根目录）
   - 仅支持 Windows
   - 使用 .NET 8 + Windows Forms
   - 文件：`批量压缩解压.csproj`、`批量压缩解压文件.cs` 等

2. **新版 Avalonia 版本**（`BatchCompress.Avalonia/` 目录）
   - 跨平台支持（Windows、Linux、macOS）
   - 使用 .NET 9 + Avalonia UI
   - 完整的 MVVM 架构
   - 功能与原版基本一致

## 快速开始

### 运行 Avalonia 版本

```bash
cd BatchCompress.Avalonia
dotnet restore
dotnet run
```

### 构建 Avalonia 版本

```bash
cd BatchCompress.Avalonia
dotnet build
```

### 发布独立应用

Windows:
```bash
cd BatchCompress.Avalonia
dotnet publish -c Release -r win-x64 --self-contained
```

Linux:
```bash
cd BatchCompress.Avalonia
dotnet publish -c Release -r linux-x64 --self-contained
```

macOS:
```bash
cd BatchCompress.Avalonia
dotnet publish -c Release -r osx-x64 --self-contained
# 或 ARM64 版本
dotnet publish -c Release -r osx-arm64 --self-contained
```

## 主要改进

### 跨平台支持
- ✅ Windows、Linux、macOS 都可以运行
- ✅ 自动检测 RAR/UnRAR 工具路径
- ✅ 平台特定功能抽象化

### 现代化架构
- ✅ MVVM 设计模式
- ✅ 依赖注入
- ✅ 接口抽象
- ✅ 清晰的代码组织

### 功能完整性
- ✅ 所有核心压缩/解压功能
- ✅ 密码生成和查询
- ✅ 批量处理
- ✅ 进度显示
- ✅ 多种压缩选项

## 技术栈

- **UI 框架**: Avalonia UI 11.3.10
- **MVVM**: CommunityToolkit.Mvvm 8.2.1
- **.NET**: .NET 9.0
- **压缩引擎**: RAR/WinRAR 命令行

## 系统要求

### 所有平台
- .NET 9.0 运行时
- RAR 或 UnRAR 命令行工具

### Windows
- Windows 7 或更高
- 推荐安装 WinRAR

### Linux
- 任何现代发行版
- 使用包管理器安装 rar/unrar

### macOS
- macOS 10.13 或更高
- 通过 Homebrew 安装 rar/unrar

## 文档

详细文档请参考：
- [Avalonia 版本 README](BatchCompress.Avalonia/README.md)
- [详细设计文档](详细设计文档.md)
- [架构文档](ARCHITECTURE.md)

## 迁移指南

如果你正在使用原版 WinForms 版本，可以直接切换到 Avalonia 版本：

1. 所有功能都已实现
2. 密码算法完全兼容
3. 文件格式完全兼容
4. UI 布局类似，易于上手

## 开发

### 前置条件
- .NET 9.0 SDK
- 支持 C# 的 IDE（VS、VSCode、Rider）

### 调试
```bash
cd BatchCompress.Avalonia
dotnet run
```

### 贡献
欢迎提交 PR 和 Issue！

## 未来计划

- [ ] 系统托盘支持
- [ ] 更完善的通知系统
- [ ] 拖放文件支持
- [ ] 热键支持
- [ ] 更多压缩格式支持（7z、tar.gz 等）
- [ ] 性能优化

## 许可证

与原项目相同

## 联系

- Email: qgkc520@Gmail.com
- QQ: 2027123419
