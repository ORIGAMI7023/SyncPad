import SwiftUI

/// 主聊天界面
@available(macOS 10.15, iOS 13.0, *)
struct ChatView: View {
    @StateObject private var viewModel = ChatViewModel()
    @State private var inputText = ""
    @State private var showingFilePicker = false
    @State private var showingImageViewer = false
    @State private var selectedImage: ImageData?

    var body: some View {
        VStack(spacing: 0) {
            // 顶部工具栏
            topToolbar

            // 消息列表
            messageList

            // 输入区域
            inputArea
        }
        .onAppear {
            viewModel.loadInitialMessages()
        }
        .sheet(isPresented: $showingFilePicker) {
            DocumentPicker(fileURL: $viewModel.selectedFileURL)
        }
        .sheet(item: $selectedImage) { image in
            ImageViewer(image: image)
        }
    }

    private var topToolbar: some View {
        HStack {
            Text("SyncPad")
                .font(.headline)
                .foregroundColor(.primary)

            Spacer()

            if let username = viewModel.username {
                HStack(spacing: 4) {
                    Image(systemName: "person.circle.fill")
                        .foregroundColor(.secondary)
                    Text(username)
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                }
            }

            Button(action: {
                viewModel.loadHistoricalMessages()
            }) {
                Image(systemName: "arrow.clockwise")
                    .foregroundColor(.primary)
            }
            .disabled(viewModel.isLoading)

            Button(action: {
                viewModel.logout()
            }) {
                Image(systemName: "arrow.right.square")
                    .foregroundColor(.red)
            }
        }
        .padding()
        .background(Color(.controlBackgroundColor))
        .shadow(color: Color.black.opacity(0.1), radius: 2, x: 0, y: 1)
    }

    private var messageList: some View {
        ScrollViewReader { proxy in
            ScrollView {
                LazyVStack(spacing: 12) {
                    if viewModel.isLoading && viewModel.messages.isEmpty {
                        ProgressView("加载中...")
                            .padding()
                    } else if viewModel.messages.isEmpty {
                        VStack(spacing: 16) {
                            Image(systemName: "message.badge")
                                .font(.system(size: 60))
                                .foregroundColor(.secondary)
                            Text("暂无消息，开始聊天吧！")
                                .foregroundColor(.secondary)
                        }
                        .padding(.top, 100)
                    } else {
                        // 加载更多按钮
                        if viewModel.hasMoreMessages {
                            Button(action: {
                                viewModel.loadHistoricalMessages()
                            }) {
                                if viewModel.isLoading {
                                    ProgressView()
                                        .scaleEffect(0.8)
                                } else {
                                    Text("加载更多消息")
                                        .font(.subheadline)
                                        .foregroundColor(.blue)
                                }
                            }
                            .padding(.vertical, 8)
                        }

                        // 消息列表
                        ForEach(viewModel.messages) { message in
                            MessageBubbleView(message: message)
                                .id(message.id)
                                .onTapGesture {
                                    // 处理消息点击（如图片预览）
                                    handleMessageTap(message)
                                }
                        }
                    }
                }
                .padding()
            }
            .onChange(of: viewModel.messages) { _ in
                // 当有新消息时滚动到底部
                if let lastMessage = viewModel.messages.last {
                    withAnimation {
                        proxy.scrollTo(lastMessage.id, anchor: .bottom)
                    }
                }
            }
        }
        .background(Color(.systemGroupedBackground))
    }

    private var inputArea: some View {
        HStack(spacing: 12) {
            // 文件上传按钮
            Button(action: {
                showingFilePicker = true
            }) {
                Image(systemName: "paperclip")
                    .font(.system(size: 20))
                    .foregroundColor(.secondary)
                    .frame(width: 40, height: 40)
                    .background(Color(.controlBackgroundColor))
                    .cornerRadius(20)
            }

            // 文本输入框
            TextField("输入消息...", text: $inputText)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .onSubmit {
                    sendMessage()
                }

            // 发送按钮
            Button(action: sendMessage) {
                if viewModel.isSending {
                    ProgressView()
                        .scaleEffect(0.8)
                } else {
                    Image(systemName: "arrow.up.circle.fill")
                        .font(.system(size: 24))
                        .foregroundColor(inputText.isEmpty ? .secondary : .blue)
                }
            }
            .disabled(inputText.isEmpty || viewModel.isSending)
            .frame(width: 44, height: 44)
        }
        .padding()
        .background(Color(.controlBackgroundColor))
        .shadow(color: Color.black.opacity(0.1), radius: 2, x: 0, y: -1)
    }

    private func sendMessage() {
        guard !inputText.isEmpty else { return }

        Task {
            await viewModel.sendMessage(text: inputText)
            await MainActor.run {
                inputText = ""
            }
        }
    }

    private func handleMessageTap(_ message: ChatMessage) {
        // 如果是图片消息，显示图片查看器
        if message.type == .file, let fileInfo = message.fileInfo {
            // TODO: 实现图片预览
        }
    }
}

/// 文件选择器
@available(macOS 10.15, iOS 13.0, *)
struct DocumentPicker: View {
    @Binding var fileURL: URL?

    var body: some View {
        Text("文件选择器")
            .onAppear {
                // 实现文件选择逻辑
            }
    }
}

/// 图片查看器
@available(macOS 10.15, iOS 13.0, *)
struct ImageViewer: View {
    let image: ImageData

    var body: some View {
        Text("图片查看器")
        // TODO: 实现图片查看器
    }
}

struct ImageData: Identifiable {
    let id = UUID()
    let data: Data
    let mimeType: String
}

// 预览
@available(macOS 10.15, iOS 13.0, *)
struct ChatView_Previews: PreviewProvider {
    static var previews: some View {
        ChatView()
    }
}
