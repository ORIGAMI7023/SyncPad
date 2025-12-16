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
    private var handshakeTimer: Timer?
    private var isHandshakeComplete = false
    private var reconnectAttempts = 0
    private let maxReconnectAttempts = 5
    private var messageBuffer = ""

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
        guard var urlComponents = URLComponents(string: hubURL) else {
            print("无法构建 WebSocket URL")
            return
        }
        urlComponents.queryItems = [URLQueryItem(name: "access_token", value: token)]
        guard let url = urlComponents.url else {
            print("无法构建 WebSocket URL")
            return
        }

        // 创建 WebSocket
        let config = URLSessionConfiguration.default
        session = URLSession(configuration: config, delegate: self, delegateQueue: .main)
        webSocket = session?.webSocketTask(with: url)
        webSocket?.resume()

        // 发送 SignalR 握手
        do {
            try await sendHandshake()

            // 启动握手超时检测（5秒）
            startHandshakeTimeout()

            // 开始接收消息
            receiveMessages()

            // 启动心跳
            startPing()
        } catch {
            print("SignalR 握手失败: \(error.localizedDescription)")
            await handleConnectionFailure()
        }
    }

    /// 断开连接
    func disconnect() {
        pingTimer?.invalidate()
        pingTimer = nil
        handshakeTimer?.invalidate()
        handshakeTimer = nil
        webSocket?.cancel(with: .normalClosure, reason: nil)
        webSocket = nil
        isConnected = false
        isHandshakeComplete = false
        reconnectAttempts = 0
        messageBuffer = ""
        onConnectionStateChanged?(false)
    }

    /// 处理连接失败
    private func handleConnectionFailure() async {
        isConnected = false
        isHandshakeComplete = false
        onConnectionStateChanged?(false)

        // 尝试重连
        if reconnectAttempts < maxReconnectAttempts {
            reconnectAttempts += 1
            let delay = min(Double(reconnectAttempts) * 2.0, 30.0) // 最大延迟30秒
            print("将在 \(delay) 秒后重连（第 \(reconnectAttempts) 次）")
            try? await Task.sleep(nanoseconds: UInt64(delay * 1_000_000_000))

            if let token = self.token {
                await connect(token: token)
            }
        } else {
            print("达到最大重连次数，停止重连")
            disconnect()
        }
    }

    // MARK: - SignalR Protocol

    private func sendHandshake() async throws {
        // SignalR JSON 协议握手
        let handshake = "{\"protocol\":\"json\",\"version\":1}\u{1e}"
        guard let webSocket = webSocket else {
            throw NSError(domain: "SignalRClient", code: -1, userInfo: [NSLocalizedDescriptionKey: "WebSocket 未初始化"])
        }
        try await webSocket.send(.string(handshake))
        print("SignalR 握手消息已发送")
    }

    private func startHandshakeTimeout() {
        handshakeTimer = Timer.scheduledTimer(withTimeInterval: 5.0, repeats: false) { [weak self] _ in
            Task { @MainActor in
                guard let self = self else { return }
                if !self.isHandshakeComplete {
                    print("SignalR 握手超时")
                    await self.handleConnectionFailure()
                }
            }
        }
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
                    await self?.handleConnectionFailure()
                }
            }
        }
    }

    private func handleMessage(_ text: String) {
        // SignalR 消息以 0x1e 分隔，使用缓冲区处理分帧
        messageBuffer.append(text)

        // 分割消息
        while let delimiterIndex = messageBuffer.firstIndex(of: "\u{1e}") {
            let messageStr = String(messageBuffer[..<delimiterIndex])
            messageBuffer.removeSubrange(...delimiterIndex)

            // 跳过空消息
            if messageStr.isEmpty {
                continue
            }

            guard let data = messageStr.data(using: .utf8) else { continue }

            // 尝试解析为 SignalR 消息
            if let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                handleSignalRMessage(json)
            } else {
                print("无法解析消息: \(messageStr)")
            }
        }
    }

    private func handleSignalRMessage(_ json: [String: Any]) {
        // 检查是否有错误
        if let error = json["error"] as? String {
            print("SignalR 错误: \(error)")
            Task {
                await handleConnectionFailure()
            }
            return
        }

        // 握手响应（空消息表示握手成功）
        if !isHandshakeComplete && json.isEmpty {
            handshakeTimer?.invalidate()
            handshakeTimer = nil
            isHandshakeComplete = true
            isConnected = true
            reconnectAttempts = 0  // 重置重连计数
            onConnectionStateChanged?(true)
            print("SignalR 握手完成")
            return
        }

        // 检查消息类型
        guard let type = json["type"] as? Int else { return }

        switch type {
        case 1: // Invocation
            handleInvocation(json)
        case 3: // StreamItem (暂不处理)
            break
        case 6: // Ping
            sendPong()
        case 7: // Close
            if let errorMsg = json["error"] as? String {
                print("服务端关闭连接: \(errorMsg)")
            }
            disconnect()
        default:
            print("未知 SignalR 消息类型: \(type)")
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
        guard isHandshakeComplete, isConnected else {
            print("SignalR 未连接或握手未完成，无法发送消息")
            return
        }

        guard let data = try? JSONSerialization.data(withJSONObject: message),
              var text = String(data: data, encoding: .utf8) else {
            print("无法序列化消息")
            return
        }

        text.append("\u{1e}")

        do {
            try await webSocket?.send(.string(text))
        } catch {
            print("发送消息失败: \(error.localizedDescription)")
            await handleConnectionFailure()
        }
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
        guard isConnected else { return }
        let ping = "{\"type\":6}\u{1e}"
        do {
            try await webSocket?.send(.string(ping))
        } catch {
            print("发送 Ping 失败: \(error.localizedDescription)")
            await handleConnectionFailure()
        }
    }

    private func sendPong() {
        Task {
            guard isConnected else { return }
            let pong = "{\"type\":6}\u{1e}"
            do {
                try await webSocket?.send(.string(pong))
            } catch {
                print("发送 Pong 失败: \(error.localizedDescription)")
                await handleConnectionFailure()
            }
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
