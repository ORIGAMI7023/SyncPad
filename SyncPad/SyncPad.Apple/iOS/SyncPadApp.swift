import SwiftUI

@main
struct SyncPadApp: App {
    @StateObject private var authManager = AuthManager.shared

    var body: some Scene {
        WindowGroup {
            Group {
                if authManager.isLoggedIn {
                    iOSContentView()
                } else {
                    LoginView()
                }
            }
            .animation(.easeInOut, value: authManager.isLoggedIn)
        }
    }
}
