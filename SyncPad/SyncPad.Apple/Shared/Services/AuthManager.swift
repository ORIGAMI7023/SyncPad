import Foundation
import Security

// MARK: - Auth Manager
/// 认证状态管理器，处理登录、登出和 Token 持久化
@MainActor
class AuthManager: ObservableObject {
    static let shared = AuthManager()

    @Published var isLoggedIn: Bool = false
    @Published var username: String?
    @Published var userId: Int?
    @Published var isLoading: Bool = false
    @Published var errorMessage: String?

    private let apiClient = ApiClient.shared
    private let keychainService = "net.origami7023.syncpad"

    private init() {
        // 尝试恢复会话
        Task {
            await restoreSession()
        }
    }

    // MARK: - Public Methods

    /// 用户登录
    func login(username: String, password: String) async -> Bool {
        isLoading = true
        errorMessage = nil

        do {
            let response = try await apiClient.login(username: username, password: password)

            if response.success, let token = response.token {
                self.username = response.username
                self.userId = response.userId
                self.isLoggedIn = true

                // 保存到 Keychain
                saveToKeychain(token: token, username: response.username ?? username, userId: response.userId)

                isLoading = false
                return true
            } else {
                errorMessage = response.errorMessage ?? "登录失败"
                isLoading = false
                return false
            }
        } catch {
            errorMessage = "网络错误: \(error.localizedDescription)"
            isLoading = false
            return false
        }
    }

    /// 登出
    func logout() {
        isLoggedIn = false
        username = nil
        userId = nil
        apiClient.setToken(nil)
        clearKeychain()
    }

    /// 尝试恢复会话
    func restoreSession() async {
        guard let (token, savedUsername, savedUserId) = loadFromKeychain() else {
            return
        }

        apiClient.setToken(token)
        self.username = savedUsername
        self.userId = savedUserId
        self.isLoggedIn = true
    }

    // MARK: - Keychain Operations

    private func saveToKeychain(token: String, username: String, userId: Int) {
        // 保存 Token
        let tokenData = token.data(using: .utf8)!
        let tokenQuery: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: "token",
            kSecValueData as String: tokenData
        ]
        SecItemDelete(tokenQuery as CFDictionary)
        SecItemAdd(tokenQuery as CFDictionary, nil)

        // 保存 Username
        let usernameData = username.data(using: .utf8)!
        let usernameQuery: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: "username",
            kSecValueData as String: usernameData
        ]
        SecItemDelete(usernameQuery as CFDictionary)
        SecItemAdd(usernameQuery as CFDictionary, nil)

        // 保存 UserId
        let userIdData = "\(userId)".data(using: .utf8)!
        let userIdQuery: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: "userId",
            kSecValueData as String: userIdData
        ]
        SecItemDelete(userIdQuery as CFDictionary)
        SecItemAdd(userIdQuery as CFDictionary, nil)
    }

    private func loadFromKeychain() -> (token: String, username: String, userId: Int)? {
        // 读取 Token
        guard let token = readKeychainItem(account: "token"),
              let username = readKeychainItem(account: "username"),
              let userIdStr = readKeychainItem(account: "userId"),
              let userId = Int(userIdStr) else {
            return nil
        }

        return (token, username, userId)
    }

    private func readKeychainItem(account: String) -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]

        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)

        if status == errSecSuccess, let data = result as? Data, let string = String(data: data, encoding: .utf8) {
            return string
        }
        return nil
    }

    private func clearKeychain() {
        let accounts = ["token", "username", "userId"]
        for account in accounts {
            let query: [String: Any] = [
                kSecClass as String: kSecClassGenericPassword,
                kSecAttrService as String: keychainService,
                kSecAttrAccount as String: account
            ]
            SecItemDelete(query as CFDictionary)
        }
    }
}
