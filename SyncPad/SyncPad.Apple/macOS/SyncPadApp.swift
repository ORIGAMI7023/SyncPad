import SwiftUI

@main
struct SyncPadApp: App {
    @StateObject private var authManager = AuthManager.shared

    var body: some Scene {
        WindowGroup {
            Group {
                if authManager.isLoggedIn {
                    macOSContentView()
                } else {
                    LoginView()
                }
            }
            .animation(.easeInOut, value: authManager.isLoggedIn)
        }
        .windowStyle(.titleBar)
        .windowToolbarStyle(.unified)
        .commands {
            // 文件菜单
            CommandGroup(after: .newItem) {
                Button("导入文件...") {
                    importFiles()
                }
                .keyboardShortcut("o", modifiers: .command)
            }

            // 账户菜单
            CommandMenu("账户") {
                if authManager.isLoggedIn {
                    Text("当前用户: \(authManager.username ?? "未知")")

                    Divider()

                    Button("登出") {
                        authManager.logout()
                    }
                } else {
                    Text("未登录")
                }
            }
        }
    }

    private func importFiles() {
        guard authManager.isLoggedIn else { return }

        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.canChooseFiles = true

        if panel.runModal() == .OK {
            // 通过通知或其他方式传递给 ContentView
            NotificationCenter.default.post(
                name: .importFilesRequested,
                object: panel.urls
            )
        }
    }
}

// MARK: - Notifications

extension Notification.Name {
    static let importFilesRequested = Notification.Name("importFilesRequested")
}
