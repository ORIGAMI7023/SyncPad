import Foundation
import SwiftUI
import Combine

// MARK: - Pad View Model
@MainActor
class PadViewModel: ObservableObject {
    // MARK: - Published Properties

    // 文本
    @Published var textContent: String = ""
    @Published var isTextSyncing: Bool = false

    // 文件
    @Published var files: [FileItemDto] = []
    @Published var isLoadingFiles: Bool = false

    // 连接状态
    @Published var isConnected: Bool = false

    // 错误
    @Published var errorMessage: String?

    // MARK: - Private Properties

    private let authManager = AuthManager.shared
    private let signalR = SignalRClient.shared
    private let fileClient = FileClient.shared
    private let cacheManager = FileCacheManager.shared

    private var textUpdateTask: Task<Void, Never>?
    private var lastSentContent: String = ""
    private var isReceivingUpdate: Bool = false

    // MARK: - Initialization

    init() {
        setupSignalRCallbacks()
    }

    // MARK: - Setup

    private func setupSignalRCallbacks() {
        signalR.onConnectionStateChanged = { [weak self] connected in
            Task { @MainActor in
                self?.isConnected = connected
                if connected {
                    // 连接成功后请求数据
                    await self?.signalR.requestLatestText()
                    await self?.signalR.requestFileList()
                }
            }
        }

        signalR.onTextUpdate = { [weak self] message in
            Task { @MainActor in
                guard let self = self else { return }
                // 避免回显自己的更新
                if message.senderId != self.authManager.userId {
                    self.isReceivingUpdate = true
                    self.textContent = message.content
                    self.lastSentContent = message.content
                    self.isReceivingUpdate = false
                }
            }
        }

        signalR.onFileList = { [weak self] files in
            Task { @MainActor in
                self?.files = files
            }
        }

        signalR.onFileUpdate = { [weak self] message in
            Task { @MainActor in
                guard let self = self else { return }
                switch message.action {
                case "added":
                    if let file = message.file {
                        // 检查是否已存在
                        if !self.files.contains(where: { $0.id == file.id }) {
                            self.files.append(file)
                        }
                    }
                case "deleted":
                    if let fileId = message.fileId {
                        self.files.removeAll { $0.id == fileId }
                    }
                default:
                    break
                }
            }
        }

        signalR.onFilePositionChanged = { [weak self] fileId, posX, posY in
            Task { @MainActor in
                if let index = self?.files.firstIndex(where: { $0.id == fileId }) {
                    self?.files[index].positionX = posX
                    self?.files[index].positionY = posY
                }
            }
        }
    }

    // MARK: - Connection

    /// 连接到服务器
    func connect() async {
        guard let token = ApiClient.shared.getToken() else {
            errorMessage = "未登录"
            return
        }

        await signalR.connect(token: token)
    }

    /// 断开连接
    func disconnect() {
        signalR.disconnect()
    }

    // MARK: - Text Operations

    /// 文本内容变化时调用（防抖）
    func onTextChanged(_ newContent: String) {
        guard !isReceivingUpdate else { return }
        guard newContent != lastSentContent else { return }

        // 取消之前的任务
        textUpdateTask?.cancel()

        // 防抖：300ms 后发送
        textUpdateTask = Task {
            try? await Task.sleep(nanoseconds: 300_000_000)
            guard !Task.isCancelled else { return }

            await sendTextUpdate(newContent)
        }
    }

    /// 发送文本更新
    private func sendTextUpdate(_ content: String) async {
        guard isConnected else { return }

        lastSentContent = content
        isTextSyncing = true

        await signalR.sendTextUpdate(content: content)

        isTextSyncing = false
    }

    // MARK: - File Operations

    /// 刷新文件列表
    func refreshFiles() async {
        isLoadingFiles = true

        do {
            files = try await fileClient.getFiles()
        } catch {
            errorMessage = "加载文件失败: \(error.localizedDescription)"
        }

        isLoadingFiles = false
    }

    /// 上传文件
    func uploadFile(url: URL) async {
        do {
            let data = try Data(contentsOf: url)
            let fileName = url.lastPathComponent
            let mimeType = getMimeType(for: url)

            let response = try await fileClient.uploadFile(
                fileName: fileName,
                data: data,
                mimeType: mimeType
            )

            if response.success, let file = response.file {
                // SignalR 会通知更新，这里不需要手动添加
                print("File uploaded: \(file.fileName)")
            } else {
                errorMessage = response.errorMessage ?? "上传失败"
            }
        } catch {
            errorMessage = "上传失败: \(error.localizedDescription)"
        }
    }

    /// 上传文件（从 Data）
    func uploadFile(fileName: String, data: Data, mimeType: String?) async {
        do {
            let response = try await fileClient.uploadFile(
                fileName: fileName,
                data: data,
                mimeType: mimeType
            )

            if !response.success {
                errorMessage = response.errorMessage ?? "上传失败"
            }
        } catch {
            errorMessage = "上传失败: \(error.localizedDescription)"
        }
    }

    /// 删除文件
    func deleteFile(_ file: FileItemDto) async {
        do {
            let success = try await fileClient.deleteFile(fileId: file.id)
            if success {
                // SignalR 会通知更新
                cacheManager.deleteCache(fileId: file.id, fileName: file.fileName)
            }
        } catch {
            errorMessage = "删除失败: \(error.localizedDescription)"
        }
    }

    /// 下载文件到缓存
    func downloadFile(_ file: FileItemDto) async -> URL? {
        do {
            return try await cacheManager.downloadToCache(file: file)
        } catch {
            errorMessage = "下载失败: \(error.localizedDescription)"
            return nil
        }
    }

    /// 获取文件缓存路径（如果已缓存）
    func getCachedURL(_ file: FileItemDto) -> URL? {
        let path = cacheManager.getCachePath(fileId: file.id, fileName: file.fileName)
        if FileManager.default.fileExists(atPath: path.path) {
            return path
        }
        return nil
    }

    /// 更新文件位置
    func updateFilePosition(_ file: FileItemDto, x: Int, y: Int) async {
        guard isConnected else { return }
        await signalR.updateFilePosition(fileId: file.id, positionX: x, positionY: y)
    }

    // MARK: - Helpers

    private func getMimeType(for url: URL) -> String {
        let ext = url.pathExtension.lowercased()
        let mimeTypes: [String: String] = [
            "jpg": "image/jpeg",
            "jpeg": "image/jpeg",
            "png": "image/png",
            "gif": "image/gif",
            "pdf": "application/pdf",
            "txt": "text/plain",
            "doc": "application/msword",
            "docx": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xls": "application/vnd.ms-excel",
            "xlsx": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "zip": "application/zip",
            "mp3": "audio/mpeg",
            "mp4": "video/mp4"
        ]
        return mimeTypes[ext] ?? "application/octet-stream"
    }
}
