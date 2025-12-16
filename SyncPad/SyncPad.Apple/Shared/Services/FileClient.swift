import Foundation

// MARK: - File Client
/// 文件操作客户端，处理上传、下载、删除
@MainActor
class FileClient: ObservableObject {
    static let shared = FileClient()

    private let baseURL = "https://syncpad.origami7023.net.cn"
    private let apiClient = ApiClient.shared

    private init() {}

    // MARK: - File List

    /// 获取文件列表
    func getFiles() async throws -> [FileItemDto] {
        let url = URL(string: "\(baseURL)/api/files")!
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        addAuthHeader(&request)

        let (data, response) = try await URLSession.shared.data(for: request)

        guard let httpResponse = response as? HTTPURLResponse else {
            throw ApiError.invalidResponse
        }

        if httpResponse.statusCode == 200 {
            let decoder = createDecoder()
            let apiResponse = try decoder.decode(ApiResponse<FileListResponse>.self, from: data)
            return apiResponse.data?.files ?? []
        } else if httpResponse.statusCode == 401 {
            throw ApiError.unauthorized
        } else {
            throw ApiError.httpError(statusCode: httpResponse.statusCode)
        }
    }

    // MARK: - File Exists Check

    /// 检查同名文件是否存在
    func fileExists(fileName: String) async throws -> Bool {
        let encodedName = fileName.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? fileName
        let url = URL(string: "\(baseURL)/api/files/exists?fileName=\(encodedName)")!
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        addAuthHeader(&request)

        let (data, response) = try await URLSession.shared.data(for: request)

        guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
            return false
        }

        let decoder = createDecoder()
        let apiResponse = try decoder.decode(ApiResponse<Bool>.self, from: data)
        return apiResponse.data ?? false
    }

    // MARK: - Upload

    /// 上传文件
    func uploadFile(fileName: String, data: Data, mimeType: String?, overwrite: Bool = false) async throws -> FileUploadResponse {
        let boundary = UUID().uuidString
        let url = URL(string: "\(baseURL)/api/files?overwrite=\(overwrite)")!

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("multipart/form-data; boundary=\(boundary)", forHTTPHeaderField: "Content-Type")
        addAuthHeader(&request)

        var body = Data()

        // 添加文件数据
        body.append("--\(boundary)\r\n".data(using: .utf8)!)
        body.append("Content-Disposition: form-data; name=\"file\"; filename=\"\(fileName)\"\r\n".data(using: .utf8)!)
        body.append("Content-Type: \(mimeType ?? "application/octet-stream")\r\n\r\n".data(using: .utf8)!)
        body.append(data)
        body.append("\r\n".data(using: .utf8)!)
        body.append("--\(boundary)--\r\n".data(using: .utf8)!)

        request.httpBody = body

        let (responseData, response) = try await URLSession.shared.data(for: request)

        guard let httpResponse = response as? HTTPURLResponse else {
            throw ApiError.invalidResponse
        }

        if httpResponse.statusCode == 200 {
            let decoder = createDecoder()
            return try decoder.decode(FileUploadResponse.self, from: responseData)
        } else if httpResponse.statusCode == 401 {
            throw ApiError.unauthorized
        } else {
            throw ApiError.httpError(statusCode: httpResponse.statusCode)
        }
    }

    // MARK: - Download

    /// 获取下载 URL
    func getDownloadURL(fileId: Int) -> URL? {
        guard let token = apiClient.getToken() else { return nil }
        let encodedToken = token.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? token
        return URL(string: "\(baseURL)/api/files/\(fileId)?token=\(encodedToken)")
    }

    /// 下载文件到本地
    func downloadFile(fileId: Int, fileName: String, destinationURL: URL, progressHandler: ((Double) -> Void)? = nil) async throws {
        guard let downloadURL = getDownloadURL(fileId: fileId) else {
            throw ApiError.unauthorized
        }

        let (tempURL, response) = try await URLSession.shared.download(from: downloadURL)

        guard let httpResponse = response as? HTTPURLResponse else {
            throw ApiError.invalidResponse
        }

        if httpResponse.statusCode == 200 {
            // 移动到目标位置
            let fileManager = FileManager.default
            if fileManager.fileExists(atPath: destinationURL.path) {
                try fileManager.removeItem(at: destinationURL)
            }
            try fileManager.moveItem(at: tempURL, to: destinationURL)
        } else if httpResponse.statusCode == 401 {
            throw ApiError.unauthorized
        } else {
            throw ApiError.httpError(statusCode: httpResponse.statusCode)
        }
    }

    // MARK: - Delete

    /// 删除文件
    func deleteFile(fileId: Int) async throws -> Bool {
        let url = URL(string: "\(baseURL)/api/files/\(fileId)")!
        var request = URLRequest(url: url)
        request.httpMethod = "DELETE"
        addAuthHeader(&request)

        let (data, response) = try await URLSession.shared.data(for: request)

        guard let httpResponse = response as? HTTPURLResponse else {
            throw ApiError.invalidResponse
        }

        if httpResponse.statusCode == 200 {
            let decoder = createDecoder()
            let apiResponse = try decoder.decode(SimpleApiResponse.self, from: data)
            return apiResponse.success
        } else if httpResponse.statusCode == 401 {
            throw ApiError.unauthorized
        } else {
            return false
        }
    }

    // MARK: - Helpers

    private func addAuthHeader(_ request: inout URLRequest) {
        if let token = apiClient.getToken() {
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }
    }

    private func createDecoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        let formatter = DateFormatter()
        formatter.timeZone = TimeZone(identifier: "UTC")

        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let dateString = try container.decode(String.self)

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

            if let date = ISO8601DateFormatter().date(from: dateString) {
                return date
            }

            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Cannot decode date: \(dateString)")
        }
        return decoder
    }
}
