# 维护与重构指南

> 状态：2026-11-05 复核到 `VERSION 0.7.6`。约束仍然有效；
> 分层全貌见 [当前架构](ARCHITECTURE.md)，可复用经验见 [经验索引](../经验/README.md)。
> 项目是开发测试版，重构时无需考虑旧程序兼容性和旧数据，只要**先备份旧数据、合理重构**即可。

## 归档后端约束

RAR 使用 RAR/WinRAR，其余可创建格式使用官方 `7zz`。新增开关必须加入对应的
`WinRarCommandBuilder` 或 `SevenZipCommandBuilder`，并作为单独参数交给
`ArchiveProcessRunner` 的 `ArgumentList`；禁止重新拼接完整命令字符串或回退为同步读取输出。
新格式应新增独立引擎并接入 `ArchiveEngineRouter`，不要把不同工具的参数混在一个构建器中。

stdout（标准输出）/stderr（标准错误）必须并行异步读取，取消时终止整棵子进程树。
**不要对归档输出脱敏**：原始输出是诊断证据，用户明确要求保留密码文本；
任何展示或导出日志的新增渠道都应说明这一点，并同步提醒「日志不可公开」。

## 执行管线与来源约束

批处理循环、取消、计数、并发、大小闸门与关机只在
`Core/Services/Pipeline/BatchExecutionPipeline<T>` 里实现；压缩与解压的条目级差异
（预检、输出路径、分卷求和、失败回滚、消息文案）交给两个适配器，
不要在主管线里堆 `if (isCompress)` 分支。

「大小上限」只停**未启动**任务，「用户取消」才传播到全部在途引擎，两者用不同令牌；
共享状态由同一把锁保护并上报克隆快照；系统关机只在非取消的正常结束路径触发恰好一次。

所有来源收集必须经 `SourceCollector.NormalizePath`（去尾部斜杠、根路径守卫、保序去重），
新扫描入口必须调用 `SystemMetadataFileFilter.ShouldSkip`，以保持三平台元数据过滤一致。
收集阶段不做存在性过滤，缺失项交给服务层统一按「来源不存在」上报并计数。

## 跨平台约束

平台命令集中在 `SystemIntegrationService`，不得在 ViewModel 中直接执行 Shell。
新能力先定义接口，再提供 Windows、macOS、Linux 的行为或明确降级。
所有路径、用户文本和通知参数均须使用 `ArgumentList`。
macOS 打包要校验 `7zz` 为通用二进制、RAR 为 arm64，拒绝错误的 Linux ELF 文件。

## 界面与测试

保持 MVVM（模型-视图-视图模型）边界，避免把业务逻辑放进 AXAML 代码隐藏。
新增归档行为至少补充格式、密码、取消或退出码相关测试中受影响的项目；
新增服务类要记得在测试 csproj 里显式 `<Compile Include>` 登记（默认编译项已关闭）。
涉及 macOS 应用包的修改必须运行 `scripts/package-macos.sh` 并做安装启动验证。

验证顺序：先在小规模测试环境跑通 → Release 构建 0 警告 0 错误 → 59 项控制台测试 →
`测试夹具/经验验证/verify.sh` 15 项实测 → `scripts/smoke-test.sh` 冒烟 → 清理残留 → 提交推送。

## 注释与版本

维护注释使用中文，并以「模型名, YYYY-MM-DD: 为什么这样改」标识非显而易见逻辑的动机，
重点写被绕开的坑与取舍，不复述代码。
**只有修改源代码才把版本号加 `0.0.1`；改文档不触发加版本**（2026-11-05 用户确认），
版本号唯一来源是根目录 `VERSION`。
