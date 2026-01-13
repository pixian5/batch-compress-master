# Security Policy

## Supported Versions

我们致力于为以下版本提供安全更新：

We are committed to providing security updates for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

### 中文 (Chinese)

如果您发现了安全漏洞，请**不要**在公开的 GitHub Issues 中报告。

请通过以下方式私下报告：

1. **优先方式**：发送邮件至 qgkc520@Gmail.com
   - 在邮件主题中包含 "[Security]" 标签
   - 详细描述漏洞
   - 提供重现步骤（如果可能）
   - 说明潜在影响

2. 如果可能，请使用 [GitHub Security Advisories](https://github.com/pixian5/batch-compress-master/security/advisories/new) 功能

### 处理流程

- 我们会在 48 小时内确认收到您的报告
- 我们会在 7 个工作日内评估并回应
- 如果确认是安全问题，我们会：
  1. 开发修复方案
  2. 在发布修复后公开披露（与您协商）
  3. 在发布说明中致谢（如果您同意）

### English

If you discover a security vulnerability, please **do not** report it in public GitHub Issues.

Please report it privately through:

1. **Preferred method**: Send email to qgkc520@Gmail.com
   - Include "[Security]" tag in the subject
   - Describe the vulnerability in detail
   - Provide reproduction steps (if possible)
   - Explain potential impact

2. If possible, use [GitHub Security Advisories](https://github.com/pixian5/batch-compress-master/security/advisories/new) feature

### Response Process

- We will acknowledge receipt within 48 hours
- We will assess and respond within 7 business days
- If confirmed as a security issue, we will:
  1. Develop a fix
  2. Publicly disclose after releasing the fix (in coordination with you)
  3. Credit you in the release notes (if you agree)

## Security Best Practices

### 使用建议 (Usage Recommendations)

1. **密码安全**：
   - 使用强密码保护压缩文件
   - 不要在共享环境中使用随机密码功能
   - 定期更新密码

2. **文件处理**：
   - 仅从可信来源解压文件
   - 验证解压后的文件
   - 注意恶意压缩包（路径遍历等）

3. **系统权限**：
   - 以最小权限运行应用
   - 不要以管理员/root权限运行（除非必要）

### Security Recommendations

1. **Password Security**:
   - Use strong passwords to protect archives
   - Don't use random password feature in shared environments
   - Regularly update passwords

2. **File Handling**:
   - Only extract files from trusted sources
   - Verify extracted files
   - Be aware of malicious archives (path traversal, etc.)

3. **System Permissions**:
   - Run the application with minimum required privileges
   - Don't run as administrator/root (unless necessary)

## Known Security Considerations

### MD5 Hash Usage

本项目使用 MD5 哈希生成密码，主要用于与旧版本兼容。MD5 不应被视为加密安全的哈希算法。

This project uses MD5 hashing for password generation, primarily for compatibility with legacy versions. MD5 should not be considered a cryptographically secure hashing algorithm.

**注意** / **Note**: 
- 这仅用于生成压缩包密码，不用于存储用户密码
- 建议使用自定义强密码替代随机密码功能
- This is only used for archive password generation, not for storing user passwords
- Consider using custom strong passwords instead of the random password feature

## Security Updates

安全更新将通过以下方式发布：

Security updates will be released through:

- GitHub Releases
- Security advisories
- CHANGELOG.md

请订阅 GitHub 仓库以接收通知。

Please subscribe to the GitHub repository to receive notifications.
