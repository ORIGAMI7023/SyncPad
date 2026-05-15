import Foundation
import Combine

/// 聊天视图模型
@available(macOS 10.15, iOS 13.0, *)
@MainActor
class ChatViewModel: ObservableObject {
    @Published var messages: [ChatMessage] = []
    @Published var username: String?
    @Published var isLoading = false
    @Published var isSending = false
    @Published var hasMoreMessages = false
    @Published var selectedFileURL: URL?

    private var signalRClient: SignalRClient?
    private var cryptoService: E2EECrypto?
    private var oldestMessageId: Int64?
    private let currentUserId: Int

    // 用户相关
    private let userDefaults = UserDefaults.standard
    private let keychainService = KeychainService()

    init() {
        // 从用户设置中获取用户信息
        self.currentUserId = userDefaults.integer(forKey: "userId")
        self.username = userDefaults.string(forKey: "username")

        // 初始化加密服务
        initializeCrypto()

        // 设置SignalR客户端
        setupSignalRClient()
    }

    // MARK: - Initialization

    private func initializeCrypto() {
        cryptoService = E2EECrypto()

        // 尝试从Keychain加载密码并派生密钥
        do {
            if let password = try? keychainService.loadPassword(),
               let salt = userDefaults.string(forKey: "encryptionSalt") {
                try cryptoService?.deriveKey(password: password, saltBase64: salt)
            }
        } catch {
            print("加密初始化失败: \(error)")
        }
    }

    private func setupSignalRClient() {
        signalRClient = SignalRClient()

        // 设置回调
        signalRClient?.onReceiveMessage = { [weak self] message in
            Task { @MainActor in
                self?.handleReceiveMessage(message)
            }
        }

        signalRClient?.onReceiveMessages = { [weak self] messages in
            Task { @MainActor in
                self?.handleReceiveMessages(messages)
            }
        }

        signalRClient?.onMessageDeleted = { [weak self] messageId in
            Task { @MainActor in
                self?.handleMessageDeleted(messageId)
            }
        }

        // 连接
        signalRClient?.connect()
    }

    // MARK: - Message Loading

    func loadInitialMessages() {
        loadMessages(beforeId: nil, count: 50)
    }

    func loadHistoricalMessages() {
        guard let oldestId = oldestMessageId else { return }
        loadMessages(beforeId: oldestId, count: 50)
    }

    private func loadMessages(beforeId: Int64?, count: Int) {
        isLoading = true

        Task {
            do {
                let request = GetMessagesRequest(beforeId: beforeId, count: count)
                let response = try await signalRClient?.requestMessages(request)

                if let response = response {
                    handleReceiveMessages(response.messages)
                    hasMoreMessages = response.hasMore
                    oldestMessageId = response.oldestMessageId
                }
            } catch {
                print("加载消息失败: \(error)")
            }

            isLoading = false
        }
    }

    // MARK: - Sending Messages

    func sendMessage(text: String) {
        guard !text.isEmpty else { return }

        isSending = true

        Task {
            do {
                guard let crypto = cryptoService else {
                    throw CryptoError.keyNotInitialized
                }

                // 加密消息内容
                let encryptionResult = try crypto.encrypt(plaintext: text)
                let encryptedContent = "\(encryptionResult.encryptedData):\(encryptionResult.iv)"

                // 发送消息
                let request = SendMessageRequest(
                    type: .text,
                    encryptedContent: encryptedContent,
                    fileItemId: nil
                )

                try await signalRClient?.sendMessage(request)

            } catch {
                print("发送消息失败: \(error)")
            }

            isSending = false
        }
    }

    func sendFile(fileURL: URL) {
        isSending = true

        Task {
            do {
                guard let crypto = cryptoService else {
                    throw CryptoError.keyNotInitialized
                }

                // 读取文件数据
                let data = try Data(contentsOf: fileURL)

                // 加密文件
                let encryptedResult = try crypto.encryptFile(
                    fileData: data,
                    progressHandler: { progress in
                        print("文件加密进度: \(progress)%")
                    }
                )

                // 计算哈希
                let hash = computeHash(data: data)

                // 上传文件（假设有FileClient）
                // let fileItem = try await fileClient.uploadEncryptedFile(...)

                // 发送文件消息
                // let request = SendMessageRequest(type: .file, fileItemId: fileItem.id)
                // try await signalRClient?.sendMessage(request)

            } catch {
                print("发送文件失败: \(error)")
            }

            isSending = false
        }
    }

    // MARK: - Message Handlers

    private func handleReceiveMessage(_ message: ChatMessage) {
        messages.append(message)
    }

    private func handleReceiveMessages(_ newMessages: [ChatMessage]) {
        // 合并消息，去重并按时间排序
        var existingIds = Set(messages.map { $0.id })
        let uniqueNewMessages = newMessages.filter { !existingIds.contains($0.id) }

        messages.append(contentsOf: uniqueNewMessages)
        messages.sort { $0.createdAt < $1.createdAt }

        if let oldest = newMessages.first?.id {
            oldestMessageId = oldest
        }
    }

    private func handleMessageDeleted(_ messageId: Int64) {
        if let index = messages.firstIndex(where: { $0.id == messageId }) {
            messages[index].isDeleted = true
        }
    }

    // MARK: - Utilities

    private func computeHash(data: Data) -> String {
        // 使用XXHash64计算哈希
        // 这里需要实现或使用第三方库
        return UUID().uuidString // 临时实现
    }

    // MARK: - Authentication

    func logout() {
        // 清理Keychain
        try? keychainService.deletePassword()

        // 清理用户设置
        userDefaults.removeObject(forKey: "userId")
        userDefaults.removeObject(forKey: "username")
        userDefaults.removeObject(forKey: "sessionToken")

        // 断开SignalR连接
        signalRClient?.disconnect()

        // 导航到登录界面（需要在主界面处理）
    }
}

// MARK: - Supporting Types

struct SendMessageRequest {
    let type: MessageType
    let encryptedContent: String?
    let fileItemId: Int?
}

enum MessageType {
    case text
    case file
}

struct GetMessagesRequest {
    let beforeId: Int64?
    let count: Int
}

struct MessageListResponse {
    let messages: [ChatMessage]
    let hasMore: Bool
    let oldestMessageId: Int64?
}

// MARK: - Keychain Service

class KeychainService {
    private let service = "com.syncpad.keychain"

    func loadPassword() throws -> String {
        // 实现Keychain读取
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: "user_password",
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]

        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)

        guard status == errSecSuccess, let data = result as? Data else {
            throw CryptoError.keychainError(status)
        }

        guard let password = String(data: data, encoding: .utf8) else {
            throw CryptoError.decodingFailed
        }

        return password
    }

    func deletePassword() throws {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: "user_password"
        ]

        let status = SecItemDelete(query as CFDictionary)
        if status != errSecSuccess && status != errSecItemNotFound {
            throw CryptoError.keychainError(status)
        }
    }
}

enum CryptoError: Error {
    case keyNotInitialized
    case keychainError(OSStatus)
    case decodingFailed
}
