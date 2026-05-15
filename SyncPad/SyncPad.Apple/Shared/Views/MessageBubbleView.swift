import SwiftUI

/// 消息气泡视图
@available(macOS 10.15, iOS 13.0, *)
struct MessageBubbleView: View {
    let message: ChatMessage
    @State private var isDecrypted = false
    @State private var decryptedText = ""
    @State private var image: UIImage? = nil

    var body: some View {
        HStack {
            if message.isOwn {
                Spacer(minLength: 60)
            }

            VStack(alignment: message.isOwn ? .trailing : .leading, spacing: 4) {
                // 用户名和时间
                HStack(spacing: 8) {
                    Text(message.username)
                        .font(.caption)
                        .fontWeight(.semibold)
                        .foregroundColor(message.isOwn ? .white.opacity(0.9) : .primary)

                    Text(message.createdAt, style: .time)
                        .font(.caption2)
                        .foregroundColor(message.isOwn ? .white.opacity(0.7) : .secondary)
                }

                // 消息内容
                if message.type == .text {
                    textMessageContent
                } else if message.type == .file {
                    fileMessageContent
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 8)
            .background(backgroundGradient)
            .foregroundColor(textColor)
            .cornerRadius(18)
            .shadow(color: Color.black.opacity(0.05), radius: 2, x: 0, y: 1)

            if !message.isOwn {
                Spacer(minLength: 60)
            }
        }
        .onAppear {
            decryptMessageIfNeeded()
        }
    }

    @ViewBuilder
    private var textMessageContent: some View {
        if message.isDeleted {
            Text("消息已删除")
                .font(.body)
                .strikethrough()
                .opacity(0.6)
        } else if isDecrypted {
            Text(decryptedText)
                .font(.body)
                .textSelection(.enabled)
        } else {
            HStack(spacing: 6) {
                ProgressView()
                    .scaleEffect(0.8)
                Text("解密中...")
                    .font(.caption)
                    .opacity(0.7)
            }
        }
    }

    @ViewBuilder
    private var fileMessageContent: some View {
        if let fileInfo = message.fileInfo {
            VStack(alignment: .leading, spacing: 8) {
                HStack(spacing: 12) {
                    // 文件图标
                    fileIcon(for: fileInfo.mimeType)
                        .font(.system(size: 32))
                        .frame(width: 44, height: 44)
                        .background(iconBackgroundColor)
                        .cornerRadius(8)

                    VStack(alignment: .leading, spacing: 2) {
                        Text(fileInfo.fileName)
                            .font(.subheadline)
                            .fontWeight(.medium)
                            .lineLimit(2)

                        Text(formatFileSize(fileInfo.fileSize))
                            .font(.caption)
                            .foregroundColor(.secondary)
                    }
                }

                // 下载按钮
                if message.isOwn {
                    Button(action: {
                        // TODO: 实现下载逻辑
                    }) {
                        Label("下载", systemImage: "arrow.down.doc")
                            .font(.caption)
                            .padding(.horizontal, 12)
                            .padding(.vertical, 6)
                            .background(Color.white.opacity(0.2))
                            .cornerRadius(12)
                    }
                }
            }
        }
    }

    private var backgroundGradient: some View {
        Group {
            if message.isOwn {
                LinearGradient(
                    gradient: Gradient(colors: [Color.blue, Color.blue.opacity(0.8)]),
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
            } else {
                Color(.systemBackground)
            }
        }
    }

    private var textColor: Color {
        message.isOwn ? .white : .primary
    }

    private var iconBackgroundColor: Color {
        message.isOwn ? Color.white.opacity(0.2) : Color.blue.opacity(0.1)
    }

    private func fileIcon(for mimeType: String?) -> some View {
        let iconName: String
        if mimeType?.starts(with: "image/") == true {
            iconName = "photo"
        } else if mimeType?.starts(with: "video/") == true {
            iconName = "video"
        } else if mimeType?.starts(with: "audio/") == true {
            iconName = "music.note"
        } else if mimeType == "application/pdf" {
            iconName = "doc.pdf"
        } else {
            iconName = "doc"
        }

        return Image(systemName: iconName)
    }

    private func formatFileSize(_ bytes: Int64) -> String {
        let units = ["B", "KB", "MB", "GB"]
        var size = Double(bytes)
        var unitIndex = 0

        while size >= 1024 && unitIndex < units.count - 1 {
            size /= 1024
            unitIndex += 1
        }

        return String(format: "%.1f %@", size, units[unitIndex])
    }

    private func decryptMessageIfNeeded() {
        guard message.type == .text,
              !message.isDeleted,
              let encryptedContent = message.encryptedContent else {
            return
        }

        Task {
            do {
                // 使用加密服务解密消息
                let crypto = E2EECrypto()
                let parts = encryptedContent.split(separator: ":")
                if parts.count == 2 {
                    let decrypted = try crypto.decrypt(
                        encryptedDataBase64: String(parts[0]),
                        ivBase64: String(parts[1])
                    )

                    await MainActor.run {
                        decryptedText = decrypted
                        isDecrypted = true
                    }
                }
            } catch {
                await MainActor.run {
                    decryptedText = "解密失败"
                    isDecrypted = true
                }
            }
        }
    }
}

/// 聊天消息模型
@available(macOS 10.15, iOS 13.0, *)
struct ChatMessage: Identifiable, Equatable {
    let id: Int64
    let userId: Int
    let username: String
    let type: MessageType
    let encryptedContent: String?
    let fileInfo: FileInfo?
    let createdAt: Date
    let isDeleted: Bool
    let isOwn: Bool

    enum MessageType {
        case text
        case file
    }

    struct FileInfo {
        let id: Int
        let fileName: String
        let fileSize: Int64
        let mimeType: String?
        let hash: String
    }
}

// 预览
@available(macOS 10.15, iOS 13.0, *)
struct MessageBubbleView_Previews: PreviewProvider {
    static var previews: some View {
        VStack(spacing: 16) {
            MessageBubbleView(message: ChatMessage(
                id: 1,
                userId: 1,
                username: "Alice",
                type: .text,
                encryptedContent: "encrypted:data:iv",
                fileInfo: nil,
                createdAt: Date(),
                isDeleted: false,
                isOwn: false
            ))

            MessageBubbleView(message: ChatMessage(
                id: 2,
                userId: 2,
                username: "Bob",
                type: .text,
                encryptedContent: "encrypted:data:iv",
                fileInfo: nil,
                createdAt: Date(),
                isDeleted: false,
                isOwn: true
            ))

            MessageBubbleView(message: ChatMessage(
                id: 3,
                userId: 1,
                username: "Alice",
                type: .file,
                encryptedContent: nil,
                fileInfo: ChatMessage.FileInfo(
                    id: 1,
                    fileName: "document.pdf",
                    fileSize: 1024 * 1024,
                    mimeType: "application/pdf",
                    hash: "abc123"
                ),
                createdAt: Date(),
                isDeleted: false,
                isOwn: false
            ))
        }
        .padding()
        .background(Color(.systemGroupedBackground))
    }
}
