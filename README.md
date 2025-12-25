# 批量压缩解压工具

## 📚 完整文档

本项目提供了详细的文档，涵盖所有方面：

- **[📖 文档索引](DOCUMENTATION_INDEX.md)** - 所有文档的总览和使用指南
- **[🏗️ 架构文档](ARCHITECTURE.md)** - 项目架构和技术设计详解
- **[🖥️ UI文档](UI_DOCUMENTATION.md)** - 完整的用户界面说明（每个按钮、流程、步骤）
- **[🔧 重构指南](REFACTORING_GUIDE.md)** - 代码重构和现代化建议
- **[👨‍💻 开发者指南](DEVELOPER_GUIDE.md)** - 开发环境、构建、调试、贡献指南
- **[⚡ 快速参考](QUICK_REFERENCE.md)** - 常用操作速查手册

**文档总计**: 52,000+ 字符，涵盖100%功能和代码

## 🚀 快速开始

### 压缩文件
1. 点击【从哪儿来】选择源文件夹
2. 点击【到哪儿去】选择目标文件夹
3. 勾选【随机密码】
4. 点击【压缩！】

### 解压文件
1. 选择"从此txt读取"，点击【选txt】选择包含文件名和密码的txt
2. 点击【到哪儿去】选择解压目标
3. 点击【解压！】

详细使用说明请查看 **[快速参考指南](QUICK_REFERENCE.md)**

## ✨ 最新优化

1. **UI与后台处理分离** - 使用异步任务处理，确保UI不会在压缩/解压过程中冻结
2. **进度报告** - 实时显示压缩/解压进度
3. **操作可取消** - 添加取消按钮，支持中断长时间运行的操作
4. **多线程支持** - 利用Task和异步/等待模式提高性能
5. **稳定性增强** - 适当的错误处理和资源管理
6. **完整文档** - 详细的架构、UI、重构和开发文档

## 🎯 主要功能

### 压缩功能
- ✅ 批量压缩文件或文件夹
- ✅ 随机密码生成或自定义密码
- ✅ 5级压缩率可调（不压缩到极限压缩）
- ✅ 固实压缩支持
- ✅ 分卷压缩（可自定义大小）
- ✅ 支持多种格式（RAR, ZIP等）
- ✅ 压缩后处理（删除/移动源文件）

### 解压功能
- ✅ 批量解压文件
- ✅ 自动处理分卷文件
- ✅ 密码管理（从txt或随机生成）
- ✅ 解压后处理（删除/移动源文件）

### 用户体验
- ✅ 实时进度显示
- ✅ 支持操作取消
- ✅ 系统托盘集成
- ✅ 拖放支持
- ✅ 详细日志输出

详细功能列表请查看 **[UI文档](UI_DOCUMENTATION.md)**

## 📋 系统要求

- **操作系统**: Windows 7/8/10/11 (x64)
- **依赖软件**: WinRAR 5.0 或更高版本
- **运行时**: .NET 8.0 Runtime
- **权限**: 管理员权限

## 🛠️ 开发环境

- Windows操作系统
- Visual Studio 2022或更高版本
- .NET 8.0 SDK
- WinRAR 5.0+

详细环境配置请查看 **[开发者指南](DEVELOPER_GUIDE.md)**

## 📦 编译说明

### 使用 Visual Studio
1. 打开项目解决方案 `批量压缩.sln`
2. 选择 Debug 或 Release 配置
3. 点击"生成" → "生成解决方案" (Ctrl+Shift+B)

### 使用命令行
```bash
# 恢复依赖
dotnet restore

# 构建项目
dotnet build -c Release

# 发布应用
dotnet publish -c Release -r win-x64
```

详细构建说明请查看 **[开发者指南 - 构建项目](DEVELOPER_GUIDE.md#构建项目)**

## 🏗️ 项目架构

本项目采用 Windows Forms + 异步编程模式：

- **Program.cs** - 应用入口，管理员权限处理
- **批量压缩解压文件.cs** - 主窗体和业务逻辑
- **API.cs** - WinRAR API 封装
- **md5.cs** - 密码生成工具
- **Win32Utility.cs** - Windows API 工具

详细架构说明请查看 **[架构文档](ARCHITECTURE.md)**

## 🤝 贡献指南

欢迎贡献！请遵循以下步骤：

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'feat: Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开 Pull Request

详细贡献指南请查看 **[开发者指南 - 代码贡献](DEVELOPER_GUIDE.md#代码贡献)**

## 📝 提交规范

使用 [Conventional Commits](https://www.conventionalcommits.org/) 规范：
- `feat`: 新功能
- `fix`: 修复bug
- `docs`: 文档更新
- `refactor`: 代码重构
- `test`: 测试相关

## 🔄 重构计划

项目有详细的重构计划，包括：
- 代码重组和分层
- 提取工具类
- 配置管理
- 国际化支持
- 单元测试
- MVVM架构

详细重构计划请查看 **[重构指南](REFACTORING_GUIDE.md)**

## 📞 联系方式

- **邮件**: qgkc520@Gmail.com
- **微信**: i17269637581
- **QQ**: 2027123419
- **Issues**: [GitHub Issues](https://github.com/pixian5/batch-compress-master/issues)

## 📄 许可证


## 🙏 致谢

