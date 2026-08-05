import AppKit
import Darwin

// GPT-5, 2026-08-06：独立状态栏帮助进程绕过当前 macOS 上失效的 Avalonia TrayIcon 后端。
final class StatusBarDelegate: NSObject, NSApplicationDelegate {
    private let parentProcessId: pid_t
    private let bundleIdentifier = "com.pixian.batchcompress"
    private var statusItem: NSStatusItem?

    init(parentProcessId: pid_t) {
        self.parentProcessId = parentProcessId
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)

        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        item.button?.title = "压"
        item.button?.toolTip = "批量压缩解压工具"

        let menu = NSMenu()
        let showHide = NSMenuItem(title: "显示/隐藏", action: #selector(toggleMainApplication), keyEquivalent: "")
        showHide.target = self
        menu.addItem(showHide)

        let quit = NSMenuItem(title: "退出", action: #selector(quitMainApplication), keyEquivalent: "")
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

let parentProcessId = CommandLine.arguments.dropFirst().first.flatMap(pid_t.init) ?? 0
let delegate = StatusBarDelegate(parentProcessId: parentProcessId)
NSApplication.shared.delegate = delegate
NSApplication.shared.run()
