import Foundation

// MARK: - File Cache Manager
/// 文件缓存管理器，处理本地缓存
@MainActor
class FileCacheManager: ObservableObject {
    static let shared = FileCacheManager()

    @Published var fileStatuses: [Int: FileStatus] = [:]
    @Published var downloadProgress: [Int: Double] = [:]

    private let cacheDirectory: URL
    private let fileClient = FileClient.shared

    private init() {
        // 创建缓存目录
        let appSupport = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask).first!
        cacheDirectory = appSupport.appendingPathComponent("SyncPad/files", isDirectory: true)

        try? FileManager.default.createDirectory(at: cacheDirectory, withIntermediateDirectories: true)
    }

    // MARK: - Status Management

    /// 获取文件状态
    func getStatus(fileId: Int) -> FileStatus {
        return fileStatuses[fileId] ?? .remote
    }

    /// 设置文件状态
    func setStatus(fileId: Int, status: FileStatus) {
        fileStatuses[fileId] = status
    }

    /// 检查文件是否已缓存
    func isCached(fileId: Int, fileName: String) -> Bool {
        let cachePath = getCachePath(fileId: fileId, fileName: fileName)
        return FileManager.default.fileExists(atPath: cachePath.path)
    }

    // MARK: - Cache Path

    /// 获取缓存文件路径
    func getCachePath(fileId: Int, fileName: String) -> URL {
        let safeFileName = fileName.replacingOccurrences(of: "/", with: "_")
        return cacheDirectory.appendingPathComponent("\(fileId)_\(safeFileName)")
    }

    // MARK: - Download to Cache

    /// 下载文件到缓存
    func downloadToCache(file: FileItemDto, progressHandler: ((Double) -> Void)? = nil) async throws -> URL {
        let cachePath = getCachePath(fileId: file.id, fileName: file.fileName)

        // 如果已缓存，直接返回
        if FileManager.default.fileExists(atPath: cachePath.path) {
            setStatus(fileId: file.id, status: .cached)
            return cachePath
        }

        // 开始下载
        setStatus(fileId: file.id, status: .downloading)
        downloadProgress[file.id] = 0

        do {
            try await fileClient.downloadFile(
                fileId: file.id,
                fileName: file.fileName,
                destinationURL: cachePath
            ) { progress in
                Task { @MainActor in
                    self.downloadProgress[file.id] = progress
                    progressHandler?(progress)
                }
            }

            setStatus(fileId: file.id, status: .cached)
            downloadProgress[file.id] = 1.0
            return cachePath
        } catch {
            setStatus(fileId: file.id, status: .error)
            throw error
        }
    }

    // MARK: - Clear Cache

    /// 删除单个文件缓存
    func deleteCache(fileId: Int, fileName: String) {
        let cachePath = getCachePath(fileId: fileId, fileName: fileName)
        try? FileManager.default.removeItem(at: cachePath)
        fileStatuses.removeValue(forKey: fileId)
        downloadProgress.removeValue(forKey: fileId)
    }

    /// 清除所有缓存
    func clearAllCache() {
        try? FileManager.default.removeItem(at: cacheDirectory)
        try? FileManager.default.createDirectory(at: cacheDirectory, withIntermediateDirectories: true)
        fileStatuses.removeAll()
        downloadProgress.removeAll()
    }

    // MARK: - Cache Info

    /// 获取缓存大小
    func getCacheSize() -> Int64 {
        var totalSize: Int64 = 0
        let fileManager = FileManager.default

        if let enumerator = fileManager.enumerator(at: cacheDirectory, includingPropertiesForKeys: [.fileSizeKey]) {
            while let fileURL = enumerator.nextObject() as? URL {
                if let fileSize = try? fileURL.resourceValues(forKeys: [.fileSizeKey]).fileSize {
                    totalSize += Int64(fileSize)
                }
            }
        }

        return totalSize
    }

    /// 格式化缓存大小
    func formattedCacheSize() -> String {
        let size = getCacheSize()
        let formatter = ByteCountFormatter()
        formatter.countStyle = .file
        return formatter.string(fromByteCount: size)
    }
}
