import Foundation

// MARK: - File Item DTO
struct FileItemDto: Codable, Identifiable {
    let id: Int
    let fileName: String
    let fileSize: Int64
    let mimeType: String?
    let uploadedAt: Date
    let expiresAt: Date
    var positionX: Int
    var positionY: Int

    enum CodingKeys: String, CodingKey {
        case id
        case fileName
        case fileSize
        case mimeType
        case uploadedAt
        case expiresAt
        case positionX
        case positionY
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
    let action: String  // "added", "deleted", "position_changed"
    let file: FileItemDto?
    let fileId: Int?
}

// MARK: - File Position Update Request
struct FilePositionUpdateRequest: Codable {
    let fileId: Int
    let positionX: Int
    let positionY: Int
}

// MARK: - File Status
enum FileStatus {
    case remote      // 仅在服务器
    case downloading // 下载中
    case cached      // 已缓存
    case error       // 出错
}
