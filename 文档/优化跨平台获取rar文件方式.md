你问：
@[/Users/x/code/trae/compress/rar目录获取方式.md] 根据文档来修改@[/Users/x/code/trae/compress/BatchCompress.Avalonia/Core/Services/RarArchiveEngine.cs] ，优化跨平台获取rar文件的方式

# 已完成的修改

- **按文档实现跨平台 RAR 可执行文件查找顺序**（仅检测 `rar`，Windows 额外允许 `winrar.exe`）：
  - **Windows**：
    - `AppContext.BaseDirectory/winrar/rar.exe`
    - `AppContext.BaseDirectory/winrar/winrar.exe`
    - 注册表 `HKLM/HKCU\...\App Paths\rar.exe` 与 `winrar.exe`（读取默认值与 `Path` 值）
    - 默认目录：
      - `C:\Program Files\WinRAR\rar.exe` / `winrar.exe`
      - `C:\Program Files (x86)\WinRAR\rar.exe` / `winrar.exe`
  - **macOS**：
    - `AppContext.BaseDirectory/rar`
    - `/opt/homebrew/bin/rar`、`/usr/local/bin/rar`、`/usr/bin/rar`
    - `which rar`
    - `~/rar/rar`、`~/.local/bin/rar`
  - **Linux**：
    - `AppContext.BaseDirectory/rarlinux`
    - `/usr/bin/rar`、`/usr/local/bin/rar`、`/bin/rar`
    - `which rar`
    - `~/rar/rar`、`~/.local/bin/rar`

- **统一校验策略**（与文档一致）：
  - **存在性**：存在且为文件
  - **可执行校验**：
    - Windows：必须是 `.exe`
    - macOS/Linux：检查执行位
  - **运行校验**：执行 `rar -?`，超时 2 秒；要求退出码 `0` 或 `1`；输出包含 `RAR/WinRAR/Copyright/Usage` 关键字

- **权限处理策略（与文档一致）**：
  - 默认不自动 `chmod`
  - 仅当 macOS/Linux 且目标位于 `AppContext.BaseDirectory` 下、且不可执行时，尝试通过 `File.SetUnixFileMode` 自动补齐执行位；否则给出“建议 chmod +x”的拒绝原因

- **诊断输出**：
  - 使用 `Debug.WriteLine` 输出 OS、BaseDirectory、候选列表、每个候选的拒绝原因、最终命中路径。

# 构建验证

- 已执行 `dotnet build BatchCompress.Avalonia/BatchCompress.Avalonia.csproj -c Release`：**成功**（仅剩现有 warning）。

# 涉及文件

- `BatchCompress.Avalonia/Core/Services/RarArchiveEngine.cs`

