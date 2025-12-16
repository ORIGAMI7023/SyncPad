import Foundation

// MARK: - SignalR Client
/// SignalR 实时同步客户端
/// 使用原生 WebSocket + SignalR JSON 协议
@MainActor
class SignalRClient: NSObject, ObservableObject {
    static let shared = SignalRClient()

    @Published var isConnected: Bool = false

    // 事件回调
    var onTextUpdate: ((TextSyncMessage) -> Void)?
    var onFileUpdate: ((FileSyncMessage) -> Void)?
    var onFileList: (([FileItemDto]) -> Void)?
    var onFilePositionChanged: ((Int, Int, Int) -> Void)?
    var onConnectionStateChanged: ((Bool) -> Void)?

    private var webSocket: URLSessionWebSocketTask?
    private var session: URLSession?
    private var token: String?
    private let hubURL = "wss://syncpad.origami7023.net.cn/hubs/text"

    private var pingTimer: Timer?
    private var isHandshakeComplete = false

    private let decoder: JSONDecoder = {
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

            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Cannot decode date")
        }
        return decoder
    }()

    private override init() {
        super.init()
    }

    // MARK: - Connection

    /// 连接到 SignalR Hub
    func connect(token: String) async {
        self.token = token

        // 构建带 Token 的 URL
        guard var urlComponents = URLComponents(string: hubURL) else { return }
        urlComponents.queryItems = [URLQueryItem(name: "access_token", value: token)]
        guard let url = urlComponents.url else { return }

        // 创建 WebSocket
        let config = URLSessionConfiguration.default
        session = URLSession(configuration: config, delegate: self, delegateQueue: .main)
        webSocket = session?.webSocketTask(with: url)
        webSocket?.resume()

        // 发送 SignalR 握手
        await sendHandshake()

        // 开始接收消息
        receiveMessages()

        // 启动心跳
        startPing()
    }

    /// 断开连接
    func disconnect() {
        pingTimer?.invalidate()
        pingTimer = nil
        webSocket?.cancel(with: .normalClosure, reason: nil)
        webSocket = nil
        isConnected = false
        isHandshakeComplete = false
        onConnectionStateChanged?(false)
    }

    // MARK: - SignalR Protocol

    private func sendHandshake() async {
        // SignalR JSON 协议握手
        let handshake = "{\"protocol\":\"json\",\"version\":1}\u{1e}"
        try? await webSocket?.send(.string(handshake))
    }

    private func receiveMessages() {
        webSocket?.receive { [weak self] result in
            guard let self = self else { return }

            switch result {
            case .success(let message):
                switch message {
                case .string(let text):
                    Task { @MainActor in
                        self.handleMessage(text)
                    }
                case .data(let data):
                    if let text = String(data: data, encoding: .utf8) {
                        Task { @MainActor in
                            self.handleMessage(text)
                        }
                    }
                @unknown default:
                    break
                }

                // 继续接收
                self.receiveMessages()

            case .failure(let error):
                print("WebSocket receive error: \(error)")
                Task { @MainActor in
                    self.isConnected = false
                    self.onConnectionStateChanged?(false)
                }
            }
        }
    }

    private func handleMessage(_ text: String) {
        // SignalR 消息以 0x1e 分隔
        let messages = text.split(separator: "\u{1e}")

        for messageStr in messages {
            guard let data = String(messageStr).data(using: .utf8) else { continue }

            // 尝试解析为 SignalR 消息
            if let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                handleSignalRMessage(json)
            }
        }
    }

    private func handleSignalRMessage(_ json: [String: Any]) {
        // 握手响应
        if json["error"] == nil && !isHandshakeComplete {
            isHandshakeComplete = true
            isConnected = true
            onConnectionStateChanged?(true)
            print("SignalR handshake complete")
            return
        }

        // 检查消息类型
        guard let type = json["type"] as? Int else { return }

        switch type {
        case 1: // Invocation
            handleInvocation(json)
        case 6: // Ping
            sendPong()
        case 7: // Close
            disconnect()
        default:
            break
        }
    }

    private func handleInvocation(_ json: [String: Any]) {
        guard let target = json["target"] as? String,
              let arguments = json["arguments"] as? [Any] else { return }

        switch target {
        case "ReceiveTextUpdate":
            if let argData = try? JSONSerialization.data(withJSONObject: arguments[0]),
               let message = try? decoder.decode(TextSyncMessage.self, from: argData) {
                onTextUpdate?(message)
            }

        case "ReceiveFileUpdate":
            if let argData = try? JSONSerialization.data(withJSONObject: arguments[0]),
               let message = try? decoder.decode(FileSyncMessage.self, from: argData) {
                onFileUpdate?(message)
            }

        case "ReceiveFileList":
            if let argData = try? JSONSerialization.data(withJSONObject: arguments[0]),
               let files = try? decoder.decode([FileItemDto].self, from: argData) {
                onFileList?(files)
            }

        case "ReceiveFilePositionChanged":
            if arguments.count >= 3,
               let fileId = arguments[0] as? Int,
               let posX = arguments[1] as? Int,
               let posY = arguments[2] as? Int {
                onFilePositionChanged?(fileId, posX, posY)
            }

        default:
            print("Unknown SignalR target: \(target)")
        }
    }

    // MARK: - Send Methods

    /// 发送文本更新
    func sendTextUpdate(content: String) async {
        let message: [String: Any] = [
            "type": 1,
            "target": "SendTextUpdate",
            "arguments": [content]
        ]
        await sendInvocation(message)
    }

    /// 请求最新文本
    func requestLatestText() async {
        let message: [String: Any] = [
            "type": 1,
            "target": "RequestLatestText",
            "arguments": []
        ]
        await sendInvocation(message)
    }

    /// 请求文件列表
    func requestFileList() async {
        let message: [String: Any] = [
            "type": 1,
            "target": "RequestFileList",
            "arguments": []
        ]
        await sendInvocation(message)
    }

    /// 更新文件位置
    func updateFilePosition(fileId: Int, positionX: Int, positionY: Int) async {
        let message: [String: Any] = [
            "type": 1,
            "target": "UpdateFilePosition",
            "arguments": [fileId, positionX, positionY]
        ]
        await sendInvocation(message)
    }

    private func sendInvocation(_ message: [String: Any]) async {
        guard isConnected, let data = try? JSONSerialization.data(withJSONObject: message),
              var text = String(data: data, encoding: .utf8) else { return }

        text.append("\u{1e}")
        try? await webSocket?.send(.string(text))
    }

    // MARK: - Ping/Pong

    private func startPing() {
        pingTimer = Timer.scheduledTimer(withTimeInterval: 15, repeats: true) { [weak self] _ in
            Task { @MainActor in
                await self?.sendPing()
            }
        }
    }

    private func sendPing() async {
        let ping = "{\"type\":6}\u{1e}"
        try? await webSocket?.send(.string(ping))
    }

    private func sendPong() {
        Task {
            let pong = "{\"type\":6}\u{1e}"
            try? await webSocket?.send(.string(pong))
        }
    }
}

// MARK: - URLSessionWebSocketDelegate

extension SignalRClient: URLSessionWebSocketDelegate {
    nonisolated func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask, didOpenWithProtocol protocol: String?) {
        Task { @MainActor in
            print("WebSocket connected")
        }
    }

    nonisolated func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask, didCloseWith closeCode: URLSessionWebSocketTask.CloseCode, reason: Data?) {
        Task { @MainActor in
            print("WebSocket closed: \(closeCode)")
            self.isConnected = false
            self.onConnectionStateChanged?(false)
        }
    }
}
