import AppKit
import Darwin

// GPT-5, 2026-08-06：独立状态栏帮助进程绕过当前 macOS 上失效的 Avalonia TrayIcon 后端。
final class StatusBarDelegate: NSObject, NSApplicationDelegate {
    private let parentProcessId: pid_t
    private let bundleIdentifier = "com.pixian.batchcompress"
    // GPT-5, 2026-09-30：菜单文案由父进程按当前界面语言传入，未提供时使用中文默认值。
    private let tooltipText: String
    private let showHideText: String
    private let exitText: String
    private var statusItem: NSStatusItem?

    init(parentProcessId: pid_t, tooltipText: String, showHideText: String, exitText: String) {
        self.parentProcessId = parentProcessId
        self.tooltipText = tooltipText
        self.showHideText = showHideText
        self.exitText = exitText
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)

        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        item.button?.title = "压"
        item.button?.toolTip = tooltipText

        let menu = NSMenu()
        let showHide = NSMenuItem(title: showHideText, action: #selector(toggleMainApplication), keyEquivalent: "")
        showHide.target = self
        menu.addItem(showHide)

        let quit = NSMenuItem(title: exitText, action: #selector(quitMainApplication), keyEquivalent: "")
        quit.target = self
        menu.addItem(quit)

        item.menu = menu
        statusItem = item

        Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in
            guard let self else { return }
            if kill(self.parentProcessId, 0) != 0 {
                NSApp.terminate(nil)
            }
        }
    }

    @objc private func toggleMainApplication() {
        for application in NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier) {
            if application.isHidden {
                application.activate(options: [.activateAllWindows])
            } else {
                application.hide()
            }
        }
    }

    @objc private func quitMainApplication() {
        for application in NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier) {
            application.terminate()
        }
        NSApp.terminate(nil)
    }
}

// GPT-5, 2026-09-30：第 1 个参数是父进程 PID；第 2/3/4 个（若存在且非空）
// 分别是 tooltip、显示/隐藏、退出菜单文案，缺失时回退到中文默认值。
let arguments = Array(CommandLine.arguments.dropFirst())
let parentProcessId = arguments.first.flatMap(pid_t.init) ?? 0

func menuArgument(at index: Int, fallback: String) -> String {
    guard index < arguments.count else { return fallback }
    let value = arguments[index]
    return value.isEmpty ? fallback : value
}

let delegate = StatusBarDelegate(
    parentProcessId: parentProcessId,
    tooltipText: menuArgument(at: 1, fallback: "批量压缩解压工具"),
    showHideText: menuArgument(at: 2, fallback: "显示/隐藏"),
    exitText: menuArgument(at: 3, fallback: "退出"))
NSApplication.shared.delegate = delegate
NSApplication.shared.run()
