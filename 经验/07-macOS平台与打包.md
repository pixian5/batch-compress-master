# macOS 平台与打包经验

> 本机是 Apple Silicon（arm64）Mac，macOS 27；与服务器/Linux 环境不要混用判断。

## 1. 图形界面启动前的显示链预检

显示器唤醒、切换、登录项启动时，CoreVideo 的 `CVDisplayLinkCreateWithActiveCGDisplays`
可能短暂返回 `-6661`，Avalonia 会在建窗前抛 `RenderTimer` 异常导致整个进程退出。
**做法**：只在 macOS、且只在进入图形界面之前，用同一个 API 做预检——
连续两次成功（中间隔约 100 毫秒）才继续，失败每 500 毫秒重试、每 10 秒提示一次。
命令行模式不经过这段。没有可用显示器时保持等待是合理的，因为此时系统本来也无法呈现窗口。

## 2. Avalonia 自带托盘在 macOS 不生成状态栏项

实测 `TrayIcon` 不创建系统状态栏项目，因此随应用包分发一个 Swift 编译的原生帮助进程
`BatchCompress.StatusBarHelper`，显示「压」字与显示/隐藏、退出菜单。
主进程退出后它自动结束。

## 3. 帮助进程的文案在启动时固定，切换语言必须重启它

传给它的参数是主进程 PID 加三条文案（提示文字、显示/隐藏、退出）；
`LocalizationService` 变化时主程序先结束旧进程再带新文案启动，其它平台则重建 `NativeMenu`。
判断「语言真的变了」要比较字符串实例，避免同一实例重复通知导致反复重建。

## 4. 发布用自包含单文件，装好后不依赖本机 .NET

`dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true`，
再用 `swiftc` 编译状态栏帮助进程，组装 `.app`（MacOS、Resources、Info.plist、icns、tools/7zip），
最后 `codesign` 签名。开发机运行才需要 .NET 10 开发工具包（SDK）。

## 5. 版本号写进 plist 后要回读校验

脚本从根目录 `VERSION` 读版本，写入 `CFBundleShortVersionString` 与 `CFBundleVersion`，
再用 `PlistBuddy` 读回来比对，不一致就报错退出，避免装出与源码不一致的版本。

## 6. 图标要两套容器

Avalonia 用 PNG，访达用 ICNS。先用 `sips` 把 ICO 转 PNG，生成各尺寸 iconset，
再 `iconutil` 产出 `.icns`。打包脚本会校验 `7zz` 是通用二进制、RAR 是 arm64，
拒绝错误的 Linux ELF 文件。

## 7. 不要用 `open -n` 启动应用

会强制创建新实例，在 Dock 的「最近使用」留下重复记录。正常用 `open /Applications/BatchCompress.Avalonia.app`。

## 8. 打包脚本的几个开关要记住

- 无参数：安装到 `/Applications`（可用 `BATCHCOMPRESS_APP_DIR` 改输出位置）。
- `--dist`：只产出 `.app.zip`，不动本机已安装版本，产物名与 `VERSION` 绑定。
- `--check`：只检查 `dotnet` / `swiftc` / `VERSION` 是否就绪。
- `BATCHCOMPRESS_RAR_DIR`：私有注入 RAR 时才设置，必须同时含 `rar` 与 `unrar`。

三平台另有 `package-linux.sh` / `package-windows.sh` / `package-all.sh`；
`.github/workflows/release.yml` 在推送 `v*` 标签时触发三平台发布。

---

**历史来源（仅追溯）**：
[修复 macOS 显示链启动竞态](../docs/20260810-1532-修复macOS显示链启动竞态-GPT-5.md)、
[归档发布与界面状态修复](../docs/20260810-2059-归档发布与界面状态修复-GPT-5.md)、
[六批次重构收官报告](../docs/20261104-0232-全项目六批次重构全面收官报告-GLM-5.3.md)。
⚠️ 早期文档写「版本 0.4.4、测试 24 项、macOS 26」都是当时环境，不适用于当前机器。
