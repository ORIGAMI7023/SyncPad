import Foundation

// MARK: - File Cache Manager
/// 文件缓存管理器，使用 XXHash64 作为缓存键
@MainActor
class FileCacheManager: ObservableObject {
    static let shared = FileCacheManager()

    @Published var downloadProgress: [Int: Double] = [:]

    private let cacheDirectory: URL
    private let fileClient = FileClient.shared

    private init() {
        let appSupport = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask).first!
        cacheDirectory = appSupport.appendingPathComponent("SyncPad/files", isDirectory: true)

        try? FileManager.default.createDirectory(at: cacheDirectory, withIntermediateDirectories: true)
    }

    // MARK: - Cache Lookup

    /// 按 hash 查找缓存文件
    func findCachedFile(hash: String) -> URL? {
        let fileManager = FileManager.default
        guard let files = try? fileManager.contentsOfDirectory(at: cacheDirectory, includingPropertiesForKeys: nil) else {
            return nil
        }

        for file in files {
            let name = file.lastPathComponent
            if name.hasPrefix("\(hash)_") {
                return file
            }
        }

        return nil
    }

    /// 遍历缓存目录，按 XXHash64 前缀查找匹配 fileName 的缓存文件（兼容旧缓存）
    func findCachedFile(fileName: String) -> URL? {
        let safeFileName = fileName.replacingOccurrences(of: "/", with: "_")
        let fileManager = FileManager.default

        guard let files = try? fileManager.contentsOfDirectory(at: cacheDirectory, includingPropertiesForKeys: nil) else {
            return nil
        }

        for file in files {
            let name = file.lastPathComponent
            if name.hasSuffix("_\(safeFileName)") && name.count > safeFileName.count + 17 {
                let hexPart = String(name.prefix(16))
                if hexPart.allSatisfy({ $0.isHexDigit }) {
                    return file
                }
            }
        }

        return nil
    }

    /// 检查文件是否已缓存
    func isCached(hash: String) -> Bool {
        return findCachedFile(hash: hash) != nil
    }

    /// 检查文件是否已缓存（按文件名）
    func isCached(fileName: String) -> Bool {
        return findCachedFile(fileName: fileName) != nil
    }

    // MARK: - Hash

    /// 计算文件的 XXHash64 十六进制字符串
    func computeHash(url: URL) -> String? {
        return XXHash64.hashFile(url: url)
    }

    // MARK: - Download to Cache

    /// 下载文件到缓存，返回缓存 URL（XXHash64 命名）
    func downloadToCache(file: FileItemDto, progressHandler: ((Double) -> Void)? = nil) async throws -> URL {
        // 优先用 hash 查缓存
        if let existing = findCachedFile(hash: file.hash) {
            return existing
        }

        // 下载到临时路径
        let tempURL = cacheDirectory.appendingPathComponent("tmp_\(file.id)_\(file.fileName)")
        downloadProgress[file.id] = 0

        do {
            try await fileClient.downloadFile(
                fileId: file.id,
                fileName: file.fileName,
                destinationURL: tempURL
            ) { progress in
                Task { @MainActor in
                    self.downloadProgress[file.id] = progress
                    progressHandler?(progress)
                }
            }

            downloadProgress[file.id] = 1.0

            // 计算 XXHash64
            guard let hash = computeHash(url: tempURL) else {
                let safeFileName = file.fileName.replacingOccurrences(of: "/", with: "_")
                let fallbackName = "0000000000000000_\(safeFileName)"
                let fallbackURL = cacheDirectory.appendingPathComponent(fallbackName)
                try? FileManager.default.moveItem(at: tempURL, to: fallbackURL)
                return fallbackURL
            }

            // 以 {xxhash64}_{fileName} 保存
            let safeFileName = file.fileName.replacingOccurrences(of: "/", with: "_")
            let cacheName = "\(hash)_\(safeFileName)"
            let cacheURL = cacheDirectory.appendingPathComponent(cacheName)

            // 如果目标已存在（同内容），删除临时文件
            if FileManager.default.fileExists(atPath: cacheURL.path) {
                try? FileManager.default.removeItem(at: tempURL)
                return cacheURL
            }

            // 移动临时文件到最终缓存路径
            try FileManager.default.moveItem(at: tempURL, to: cacheURL)
            return cacheURL
        } catch {
            try? FileManager.default.removeItem(at: tempURL)
            throw error
        }
    }

    // MARK: - Delete Cache

    /// 删除指定文件的缓存（按文件名查找）
    func deleteCache(fileName: String) {
        if let url = findCachedFile(fileName: fileName) {
            try? FileManager.default.removeItem(at: url)
        }
    }

    /// 清除所有缓存
    func clearAllCache() {
        try? FileManager.default.removeItem(at: cacheDirectory)
        try? FileManager.default.createDirectory(at: cacheDirectory, withIntermediateDirectories: true)
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

    // MARK: - Cache Cleanup

    /// 清理过期缓存（默认7天未访问）
    func cleanupExpiredCache(expirationDays: Int = 7) {
        let fileManager = FileManager.default
        guard let files = try? fileManager.enumerator(at: cacheDirectory, includingPropertiesForKeys: [.contentAccessDateKey, .creationDateKey]) else {
            return
        }

        let expirationInterval = TimeInterval(expirationDays * 86400)
        let now = Date()

        while let fileURL = files.nextObject() as? URL {
            guard let resourceValues = try? fileURL.resourceValues(forKeys: [.contentAccessDateKey, .creationDateKey]) else {
                continue
            }

            // 优先使用访问时间，否则使用创建时间
            let lastAccess = resourceValues.contentAccessDate ?? resourceValues.creationDate ?? now
            let elapsed = now.timeIntervalSince(lastAccess)

            if elapsed > expirationInterval {
                try? fileManager.removeItem(at: fileURL)
            }
        }
    }
}
