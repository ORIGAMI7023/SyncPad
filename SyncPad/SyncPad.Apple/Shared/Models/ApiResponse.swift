import Foundation

// MARK: - Generic API Response
struct ApiResponse<T: Codable>: Codable {
    let success: Bool
    let data: T?
    let errorMessage: String?
}

// MARK: - Simple API Response (no data)
struct SimpleApiResponse: Codable {
    let success: Bool
    let errorMessage: String?
}
