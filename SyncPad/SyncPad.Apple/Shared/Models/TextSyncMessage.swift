import Foundation

// MARK: - Text Sync Message
struct TextSyncMessage: Codable {
    let content: String
    let updatedAt: Date
    let senderId: Int

    enum CodingKeys: String, CodingKey {
        case content
        case updatedAt
        case senderId
    }
}
