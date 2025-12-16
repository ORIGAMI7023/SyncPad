import Foundation

// MARK: - API Client
/// HTTP API 客户端，负责与服务器通信
@MainActor
class ApiClient: ObservableObject {
    static let shared = ApiClient()

    private let baseURL = "https://syncpad.origami7023.net.cn"
    private var token: String?

    private let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        // 配置日期解析策略
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSSSSS"
        formatter.timeZone = TimeZone(identifier: "UTC")
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let dateString = try container.decode(String.self)

            // 尝试多种格式
            let formats = [
                "yyyy-MM-dd'T'HH:mm:ss.SSSSSS",
                "yyyy-MM-dd'T'HH:mm:ss.SSS",
                "yyyy-MM-dd'T'HH:mm:ss",
                "yyyy-MM-dd'T'HH:mm:ssZ"
            ]

            for format in formats {
                formatter.dateFormat = format
                if let date = formatter.date(from: dateString) {
                    return date
                }
            }

            // ISO8601 fallback
            if let date = ISO8601DateFormatter().date(from: dateString) {
                return date
            }

            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Cannot decode date: \(dateString)")
        }
        return decoder
    }()

    private let encoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        return encoder
    }()

    private init() {}

    // MARK: - Token Management

    func setToken(_ token: String?) {
        self.token = token
    }

    func getToken() -> String? {
        return token
    }

    // MARK: - Authentication

    /// 用户登录
    func login(username: String, password: String) async throws -> LoginResponse {
        let request = LoginRequest(username: username, password: password)
        let url = URL(string: "\(baseURL)/api/auth/login")!

        var urlRequest = URLRequest(url: url)
        urlRequest.httpMethod = "POST"
        urlRequest.setValue("application/json", forHTTPHeaderField: "Content-Type")
        urlRequest.httpBody = try encoder.encode(request)

        let (data, response) = try await URLSession.shared.data(for: urlRequest)

        guard let httpResponse = response as? HTTPURLResponse else {
            throw ApiError.invalidResponse
        }

        if httpResponse.statusCode == 200 {
            let loginResponse = try decoder.decode(LoginResponse.self, from: data)
            if loginResponse.success, let token = loginResponse.token {
                self.token = token
            }
            return loginResponse
        } else {
            throw ApiError.httpError(statusCode: httpResponse.statusCode)
        }
    }

    // MARK: - Text API

    /// 获取文本内容
    func getText() async throws -> ApiResponse<TextSyncMessage> {
        return try await get("/api/text")
    }

    /// 更新文本内容
    func updateText(_ message: TextSyncMessage) async throws -> ApiResponse<TextSyncMessage> {
        return try await post("/api/text", body: message)
    }

    // MARK: - Generic HTTP Methods

    private func get<T: Codable>(_ path: String) async throws -> T {
        let url = URL(string: "\(baseURL)\(path)")!
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        addAuthHeader(&request)

        let (data, response) = try await URLSession.shared.data(for: request)

        guard let httpResponse = response as? HTTPURLResponse else {
            throw ApiError.invalidResponse
        }

        if httpResponse.statusCode == 200 {
            return try decoder.decode(T.self, from: data)
        } else if httpResponse.statusCode == 401 {
            throw ApiError.unauthorized
        } else {
            throw ApiError.httpError(statusCode: httpResponse.statusCode)
        }
    }

    private func post<T: Codable, R: Codable>(_ path: String, body: T) async throws -> R {
        let url = URL(string: "\(baseURL)\(path)")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try encoder.encode(body)
        addAuthHeader(&request)

        let (data, response) = try await URLSession.shared.data(for: request)

        guard let httpResponse = response as? HTTPURLResponse else {
            throw ApiError.invalidResponse
        }

        if httpResponse.statusCode == 200 {
            return try decoder.decode(R.self, from: data)
        } else if httpResponse.statusCode == 401 {
            throw ApiError.unauthorized
        } else {
            throw ApiError.httpError(statusCode: httpResponse.statusCode)
        }
    }

    private func addAuthHeader(_ request: inout URLRequest) {
        if let token = token {
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }
    }
}

// MARK: - API Errors

enum ApiError: Error, LocalizedError {
    case invalidResponse
    case unauthorized
    case httpError(statusCode: Int)
    case networkError(Error)

    var errorDescription: String? {
        switch self {
        case .invalidResponse:
            return "无效的服务器响应"
        case .unauthorized:
            return "未授权，请重新登录"
        case .httpError(let code):
            return "HTTP 错误: \(code)"
        case .networkError(let error):
            return "网络错误: \(error.localizedDescription)"
        }
    }
}
