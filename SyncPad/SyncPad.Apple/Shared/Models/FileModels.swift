import Foundation

// MARK: - File Item DTO
struct FileItemDto: Codable, Identifiable {
    let id: Int
    let fileName: String
    let fileSize: Int64
    let mimeType: String?
    let uploadedAt: Date
    let expiresAt: Date

    enum CodingKeys: String, CodingKey {
        case id
        case fileName
        case fileSize
        case mimeType
        case uploadedAt
        case expiresAt
    }
}

// MARK: - File List Response
struct FileListResponse: Codable {
    let files: [FileItemDto]
}

// MARK: - File Upload Response
struct FileUploadResponse: Codable {
    let success: Bool
    let file: FileItemDto?
    let errorMessage: String?
}

// MARK: - File Sync Message (SignalR)
struct FileSyncMessage: Codable {
    let action: String  // "added", "deleted"
    let file: FileItemDto?
    let fileId: Int?
}

// MARK: - File Status
enum FileStatus {
    case remote  // 仅在服务器
    case cached  // 已缓存
    case error   // 出错
}

// MARK: - File Type Helper
enum FileType: String {
    case image = "图片"
    case pdf = "PDF"
    case document = "文档"
    case spreadsheet = "表格"
    case presentation = "演示"
    case audio = "音频"
    case video = "视频"
    case archive = "压缩包"
    case text = "文本"
    case other = "其他"
}
