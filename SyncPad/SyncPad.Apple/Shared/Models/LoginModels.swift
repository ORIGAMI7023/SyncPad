import Foundation

// MARK: - Login Request
struct LoginRequest: Codable {
    let username: String
    let password: String
}

// MARK: - Login Response
struct LoginResponse: Codable {
    let success: Bool
    let token: String?
    let username: String?
    let userId: Int
    let errorMessage: String?
}
