# 维护与重构指南

## 归档后端约束

继续使用 RAR/WinRAR。新增开关必须在 `WinRarCommandBuilder` 中作为单独参数添加，并由 `WinRarProcessRunner` 的 `ArgumentList` 传递；禁止重新拼接完整命令字符串或回退为同步读取输出。

不要对归档 stdout/stderr 脱敏。输出是原始诊断证据，用户明确要求保留密码文本。任何展示或导出日志的新增渠道都应说明这一点。

## 跨平台约束

平台命令集中在 `SystemIntegrationService`，不得在 ViewModel 中直接执行 Shell。新能力先定义接口，再提供 Windows、macOS、Linux 的行为或明确降级。所有路径、用户文本和通知参数均须使用 `ArgumentList`。

新扫描入口必须调用 `SystemMetadataFileFilter.ShouldSkip`，以保持对 Windows、macOS、Linux 元数据文件的过滤一致性。

## UI 与测试

保持 MVVM 边界，避免将业务逻辑放进 AXAML 代码隐藏。新增归档行为至少补充格式、密码、取消或退出码相关测试中受影响的项目。涉及 macOS 包的修改必须运行 `scripts/package-macos.sh` 并安装启动验证。

维护注释使用中文，并以 `GPT-5, YYYY-MM-DD` 标识新增或改写的非显而易见逻辑。
