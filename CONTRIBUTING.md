# Contributing to Batch Compress Master

感谢您对批量压缩解压工具的关注！我们欢迎各种形式的贡献。

Thank you for your interest in Batch Compress Master! We welcome contributions of all kinds.

## 如何贡献 (How to Contribute)

### 报告 Bug (Reporting Bugs)

如果您发现了 bug，请在 GitHub Issues 中创建新的 issue，并包含：
- 详细的问题描述
- 重现步骤
- 预期行为和实际行为
- 您的环境信息（操作系统、.NET 版本等）
- 相关的日志或截图

If you find a bug, please create a new issue on GitHub Issues with:
- Detailed description of the problem
- Steps to reproduce
- Expected vs actual behavior
- Your environment (OS, .NET version, etc.)
- Relevant logs or screenshots

### 提出新功能 (Suggesting Features)

我们欢迎新功能建议！请在 GitHub Issues 中描述：
- 功能的详细说明
- 为什么这个功能有用
- 可能的实现方案（可选）

We welcome feature suggestions! Please describe in GitHub Issues:
- Detailed description of the feature
- Why this feature would be useful
- Possible implementation approaches (optional)

### 提交代码 (Submitting Code)

1. **Fork 仓库** / **Fork the repository**
   
2. **创建特性分支** / **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **进行更改** / **Make your changes**
   - 遵循现有的代码风格
   - 添加必要的注释
   - 确保代码能够编译
   - Follow existing code style
   - Add necessary comments
   - Ensure code compiles

4. **测试您的更改** / **Test your changes**
   ```bash
   dotnet build
   dotnet run
   ```

5. **提交更改** / **Commit your changes**
   ```bash
   git add .
   git commit -m "Add feature: your feature description"
   ```

6. **推送到您的 Fork** / **Push to your fork**
   ```bash
   git push origin feature/your-feature-name
   ```

7. **创建 Pull Request** / **Create a Pull Request**
   - 提供清晰的 PR 描述
   - 引用相关的 Issues
   - Provide clear PR description
   - Reference related issues

## 代码规范 (Code Standards)

### C# 代码规范
- 使用 4 个空格缩进
- 遵循 Microsoft C# 编码约定
- 为公共 API 添加 XML 文档注释
- 使用有意义的变量和方法名

### C# Code Standards
- Use 4 spaces for indentation
- Follow Microsoft C# Coding Conventions
- Add XML documentation comments for public APIs
- Use meaningful variable and method names

### 提交信息 (Commit Messages)
- 使用清晰、描述性的提交信息
- 第一行简短概括（50 字符以内）
- 如需要，添加详细描述

- Use clear, descriptive commit messages
- First line: short summary (within 50 characters)
- Add detailed description if needed

## 开发环境设置 (Development Environment Setup)

### 前置条件 (Prerequisites)
- .NET 10.0 SDK 或更高版本
- 支持 C# 的 IDE（Visual Studio、VS Code、JetBrains Rider）
- Git

- .NET 10.0 SDK or higher
- IDE that supports C# (Visual Studio, VS Code, JetBrains Rider)
- Git

### 构建项目 (Building the Project)
```bash
# 克隆仓库 / Clone repository
git clone https://github.com/pixian5/batch-compress-master.git
cd batch-compress-master

# 恢复依赖 / Restore dependencies
dotnet restore

# 构建 / Build
dotnet build

# 运行 / Run
dotnet run
```

## 问题和讨论 (Questions and Discussions)

如有任何问题，欢迎：
- 在 GitHub Issues 中提问
- 通过 Email 联系：qgkc520@Gmail.com

If you have any questions:
- Ask in GitHub Issues
- Contact via Email: qgkc520@Gmail.com

## 许可证 (License)

通过贡献，您同意您的贡献将在 MIT 许可证下授权。

By contributing, you agree that your contributions will be licensed under the MIT License.

---

再次感谢您的贡献！/ Thank you again for your contributions!
