import Foundation
import CryptoKit

/// 端到端加密服务（iOS/Mac）
@available(macOS 10.15, iOS 13.0, *)
public class E2EECrypto {

    // MARK: - Properties

    private var key: SymmetricKey?
    private let keyTag = "com.syncpad.encryption.key"

    // MARK: - Key Derivation

    /// 从密码和盐值派生密钥
    /// - Parameters:
    ///   - password: 用户密码
    ///   - saltBase64: Base64编码的盐值
    /// - Returns: 派生的密钥
    public func deriveKey(password: String, saltBase64: String) throws -> SymmetricKey {
        guard let saltData = Data(base64Encoded: saltBase64) else {
            throw CryptoError.invalidSalt
        }

        guard let passwordData = password.data(using: .utf8) else {
            throw CryptoError.invalidPassword
        }

        // 使用 PBKDF2 派生密钥
        let derivedKey = deriveKeyUsingPBKDF2(
            password: passwordData,
            salt: saltData,
            keyLength: 32, // 256 bits
            rounds: 100_000
        )

        let symmetricKey = SymmetricKey(data: derivedKey)
        self.key = symmetricKey
        return symmetricKey
    }

    // MARK: - Encryption/Decryption

    /// 加密文本
    /// - Parameter plaintext: 明文
    /// - Returns: 加密结果（加密数据和IV）
    public func encrypt(plaintext: String) throws -> EncryptionResult {
        guard let key = self.key else {
            throw CryptoError.keyNotInitialized
        }

        guard let plaintextData = plaintext.data(using: .utf8) else {
            throw CryptoError.invalidPlaintext
        }

        // 生成随机IV（12字节，AES-GCM推荐）
        let iv = AES.GCM.Nonce()
        let sealedBox = try AES.GCM.seal(plaintextData, using: key, nonce: iv)

        return EncryptionResult(
            encryptedData: sealedBox.ciphertext.base64EncodedString(),
            iv: Data(iv).base64EncodedString()
        )
    }

    /// 解密文本
    /// - Parameters:
    ///   - encryptedDataBase64: Base64编码的加密数据
    ///   - ivBase64: Base64编码的IV
    /// - Returns: 解密后的明文
    public func decrypt(encryptedDataBase64: String, ivBase64: String) throws -> String {
        guard let key = self.key else {
            throw CryptoError.keyNotInitialized
        }

        guard let encryptedData = Data(base64Encoded: encryptedDataBase64) else {
            throw CryptoError.invalidEncryptedData
        }

        guard let ivData = Data(base64Encoded: ivBase64) else {
            throw CryptoError.invalidIV
        }

        // 组合ciphertext和tag
        var sealedBox: AES.GCM.SealedBox
        do {
            sealedBox = try AES.GCM.SealedBox(nonce: AES.GCM.Nonce(data: ivData), ciphertext: encryptedData)
        } catch {
            throw CryptoError.decryptionFailed(error)
        }

        let decryptedData = try AES.GCM.open(sealedBox, using: key)

        guard let decryptedText = String(data: decryptedData, encoding: .utf8) else {
            throw CryptoError.decodingFailed
        }

        return decryptedText
    }

    /// 加密文件
    /// - Parameters:
    ///   - fileData: 文件数据
    ///   - progressHandler: 进度回调（百分比）
    /// - Returns: 加密结果
    public func encryptFile(fileData: Data, progressHandler: ((Double) -> Void)?) throws -> EncryptionResult {
        guard let key = self.key else {
            throw CryptoError.keyNotInitialized
        }

        // 对于大文件，分块加密（每块100MB）
        let chunkSize = 100 * 1024 * 1024 // 100MB
        let totalChunks = (fileData.count + chunkSize - 1) / chunkSize

        var encryptedChunks: Data = Data()
        let iv = AES.GCM.Nonce()

        for i in 0..<totalChunks {
            let start = i * chunkSize
            let end = min(start + chunkSize, fileData.count)
            let chunk = fileData[start..<end]

            let sealedBox = try AES.GCM.seal(chunk, using: key, nonce: iv)
            encryptedChunks.append(sealedBox.ciphertext)

            let progress = Double(i + 1) / Double(totalChunks) * 100.0
            DispatchQueue.main.async {
                progressHandler?(progress)
            }
        }

        return EncryptionResult(
            encryptedData: encryptedChunks.base64EncodedString(),
            iv: Data(iv).base64EncodedString()
        )
    }

    /// 解密文件
    /// - Parameters:
    ///   - encryptedDataBase64: Base64编码的加密数据
    ///   - ivBase64: Base64编码的IV
    ///   - progressHandler: 进度回调（百分比）
    /// - Returns: 解密后的文件数据
    public func decryptFile(encryptedDataBase64: String, ivBase64: String, progressHandler: ((Double) -> Void)?) throws -> Data {
        guard let key = self.key else {
            throw CryptoError.keyNotInitialized
        }

        guard let encryptedData = Data(base64Encoded: encryptedDataBase64) else {
            throw CryptoError.invalidEncryptedData
        }

        guard let ivData = Data(base64Encoded: ivBase64) else {
            throw CryptoError.invalidIV
        }

        let nonce = AES.GCM.Nonce(data: ivData)

        // 对于大文件，分块解密（每块100MB）
        let chunkSize = 100 * 1024 * 1024 // 100MB (注意：加密后的数据会稍大)
        let totalChunks = (encryptedData.count + chunkSize - 1) / chunkSize

        var decryptedChunks: Data = Data()

        for i in 0..<totalChunks {
            let start = i * chunkSize
            let end = min(start + chunkSize, encryptedData.count)
            let chunk = encryptedData[start..<end]

            let sealedBox = try AES.GCM.SealedBox(nonce: nonce, ciphertext: chunk)
            let decryptedChunk = try AES.GCM.open(sealedBox, using: key)
            decryptedChunks.append(decryptedChunk)

            let progress = Double(i + 1) / Double(totalChunks) * 100.0
            DispatchQueue.main.async {
                progressHandler?(progress)
            }
        }

        return decryptedChunks
    }

    // MARK: - Key Storage (Keychain)

    /// 保存密码到Keychain
    /// - Parameter password: 用户密码
    public func savePasswordToKeychain(password: String) throws {
        let data = password.data(using: .utf8)!

        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: "syncpad_user_password",
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleWhenUnlocked
        ]

        // 先删除旧的
        SecItemDelete(query as CFDictionary)

        // 添加新的
        let status = SecItemAdd(query as CFDictionary, nil)
        guard status == errSecSuccess else {
            throw CryptoError.keychainError(status)
        }
    }

    /// 从Keychain加载密码
    /// - Returns: 用户密码
    public func loadPasswordFromKeychain() throws -> String {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: "syncpad_user_password",
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

    /// 从Keychain删除密码
    public func deletePasswordFromKeychain() throws {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: "syncpad_user_password"
        ]

        let status = SecItemDelete(query as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw CryptoError.keychainError(status)
        }
    }

    // MARK: - Helper Methods

    /// 使用PBKDF2派生密钥
    private func deriveKeyUsingPBKDF2(password: Data, salt: Data, keyLength: Int, rounds: Int) -> Data {
        // 使用CommonCrypto进行PBKDF2派生
        var derivedKeyData = Data(repeating: 0, count: keyLength)
        let derivationStatus: Int32 = derivedKeyData.withUnsafeMutableBytes { derivedKeyBytes in
            password.withUnsafeBytes { passwordBytes in
                salt.withUnsafeBytes { saltBytes in
                    CCKeyDerivationPBKDF(
                        CCPBKDFAlgorithm(kCCPBKDF2),
                        passwordBytes.baseAddress?.assumingMemoryBound(to: Int8.self),
                        password.count,
                        saltBytes.baseAddress?.assumingMemoryBound(to: UInt8.self),
                        salt.count,
                        CCPseudoRandomAlgorithm(kCCPRFHmacAlgSHA256),
                        UInt32(rounds),
                        derivedKeyBytes.baseAddress?.assumingMemoryBound(to: UInt8.self),
                        keyLength
                    )
                }
            }
        }

        guard derivationStatus == kCCSuccess else {
            return Data(repeating: 0, count: keyLength)
        }

        return derivedKeyData
    }

    /// 检查密钥是否已初始化
    public var isKeyInitialized: Bool {
        return key != nil
    }
}

// MARK: - Supporting Types

/// 加密结果
public struct EncryptionResult {
    public let encryptedData: String
    public let iv: String
}

/// 加密错误
public enum CryptoError: Error, LocalizedError {
    case invalidSalt
    case invalidPassword
    case invalidPlaintext
    case invalidEncryptedData
    case invalidIV
    case keyNotInitialized
    case decryptionFailed(Error)
    case decodingFailed
    case keychainError(OSStatus)

    public var errorDescription: String? {
        switch self {
        case .invalidSalt:
            return "无效的盐值"
        case .invalidPassword:
            return "无效的密码"
        case .invalidPlaintext:
            return "无效的明文"
        case .invalidEncryptedData:
            return "无效的加密数据"
        case .invalidIV:
            return "无效的初始化向量"
        case .keyNotInitialized:
            return "密钥未初始化"
        case .decryptionFailed(let error):
            return "解密失败: \(error.localizedDescription)"
        case .decodingFailed:
            return "解码失败"
        case .keychainError(let status):
            return "Keychain错误: \(status)"
        }
    }
}
