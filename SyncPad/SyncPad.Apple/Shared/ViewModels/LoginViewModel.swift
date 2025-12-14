import Foundation
import SwiftUI

// MARK: - Login View Model
@MainActor
class LoginViewModel: ObservableObject {
    @Published var username: String = ""
    @Published var password: String = ""
    @Published var isLoading: Bool = false
    @Published var errorMessage: String?
    @Published var isLoggedIn: Bool = false

    private let authManager = AuthManager.shared

    init() {
        // 监听登录状态
        isLoggedIn = authManager.isLoggedIn
    }

    /// 执行登录
    func login() async {
        guard !username.isEmpty, !password.isEmpty else {
            errorMessage = "请输入用户名和密码"
            return
        }

        isLoading = true
        errorMessage = nil

        let success = await authManager.login(username: username, password: password)

        isLoading = false

        if success {
            isLoggedIn = true
        } else {
            errorMessage = authManager.errorMessage ?? "登录失败"
        }
    }

    /// 登出
    func logout() {
        authManager.logout()
        isLoggedIn = false
        username = ""
        password = ""
    }

    /// 检查是否已登录
    func checkLoginStatus() {
        isLoggedIn = authManager.isLoggedIn
    }
}
