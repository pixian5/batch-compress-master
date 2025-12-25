### 
按以下顺序确定 `rarPath`：

自动发现候选路径（按平台）

#### Windows
候选顺序建议：

1. **程序运行目录下的winrar目录下找：
   - `rar.exe`
   - `winrar.exe`
2. **注册表 App Paths**（HKLM、HKCU 都查）：
   - `...\App Paths\rar.exe`（取默认值或 `Path`）
   - `...\App Paths\winrar.exe`
3. **默认安装目录**：
   - `C:\Program Files\WinRAR\rar.exe`
   - `C:\Program Files\WinRAR\winrar.exe`
   - `C:\Program Files (x86)\WinRAR\rar.exe`
   - `C:\Program Files (x86)\WinRAR\winrar.exe`

#### macOS
1. **程序运行目录**：`rar文件`
2. **固定路径**（避免 Finder 启动 PATH 不完整）：
   - `/opt/homebrew/bin/rar`
   - `/usr/local/bin/rar`
   - `/usr/bin/rar`（可选）
3. **PATH/which**：
   - 执行 `which rar` 获取绝对路径（注意 GUI PATH 不全，因此放在固定路径之后）
4. **用户目录兜底**：
   - `~/rar/rar`
   - `~/.local/bin/rar`

#### Linux
1. **程序运行目录**：`rarlinux`
2. **固定路径**：
   - `/usr/bin/rar`
   - `/usr/local/bin/rar`
   - `/bin/rar`
3. **PATH/which**：`which rar`
4. **用户目录兜底**：
   - `~/rar/rar`
   - `~/.local/bin/rar`

---

### 校验策略（统一）
对每个候选路径，做如下校验；第一个通过者即采用：

1. **存在性校验**：存在且是文件
2. **可执行校验**
   - Windows：可跳过（存在 exe 基本就行）
   - macOS/Linux：检查执行位（或直接进入第 3 步运行校验）
3. **运行校验（强烈建议）**
   - 执行：`<rarPath> -?`
   - 超时：2 秒
   - 判定通过：  
     - 进程成功启动且有输出（stdout 或 stderr），并且输出包含类似 `RAR`/`WinRAR`/`Copyright`/`Usage` 等关键字  
     - 要求 exit code == 0或1

> 这样能覆盖：权限问题、动态库缺失、架构不匹配、被隔离、文件损坏等情况。

---

### D. 权限处理策略（建议这样做）
- 默认：**不自动 chmod**
- 如果发现文件存在但不可执行：
  - 在日志提示“不可执行，建议 chmod +x …”
  - 只对程序目录内rar文件自动修复

---

### E. 诊断输出（建议必须做）
输出内容建议包括：

- 当前操作系统和版本、`AppContext.BaseDirectory`
- 候选列表（按顺序）
- 每个候选的校验结果（存在/可执行/运行校验摘要）
- 最终采用的路径

这样用户一贴日志，你就能秒定位。
