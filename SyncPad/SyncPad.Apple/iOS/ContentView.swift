import SwiftUI

// MARK: - iOS Content View
/// iPhone: TabView 切换文本/文件
/// iPad: 左右分栏
struct iOSContentView: View {
    @StateObject private var viewModel = PadViewModel()
    @StateObject private var authManager = AuthManager.shared

    @Environment(\.horizontalSizeClass) var horizontalSizeClass

    var body: some View {
        Group {
            if horizontalSizeClass == .regular {
                // iPad: 分栏布局
                iPadLayout
            } else {
                // iPhone: Tab 布局
                iPhoneLayout
            }
        }
        .task {
            await viewModel.connect()
        }
        .onDisappear {
            viewModel.disconnect()
        }
        .toolbar {
            ToolbarItem(placement: .navigationBarTrailing) {
                connectionStatus
            }

            ToolbarItem(placement: .navigationBarTrailing) {
                Menu {
                    Button(role: .destructive) {
                        authManager.logout()
                    } label: {
                        Label("登出", systemImage: "rectangle.portrait.and.arrow.right")
                    }
                } label: {
                    Image(systemName: "person.circle")
                }
            }
        }
    }

    // MARK: - iPhone Layout (TabView)

    private var iPhoneLayout: some View {
        TabView {
            // 文本 Tab
            NavigationStack {
                TextEditorContentView(
                    text: $viewModel.textContent,
                    onTextChanged: viewModel.onTextChanged
                )
                .navigationTitle("SyncPad")
                .navigationBarTitleDisplayMode(.inline)
            }
            .tabItem {
                Label("文本", systemImage: "doc.text")
            }

            // 文件 Tab
            NavigationStack {
                FileGridView(viewModel: viewModel, enableDragDrop: false)
                    .navigationTitle("文件")
                    .navigationBarTitleDisplayMode(.inline)
            }
            .tabItem {
                Label("文件", systemImage: "folder")
            }
        }
    }

    // MARK: - iPad Layout (Split View)

    private var iPadLayout: some View {
        NavigationStack {
            HStack(spacing: 0) {
                // 左侧：文本
                TextEditorContentView(
                    text: $viewModel.textContent,
                    onTextChanged: viewModel.onTextChanged
                )
                .frame(minWidth: 300)

                Divider()

                // 右侧：文件
                FileGridView(viewModel: viewModel, enableDragDrop: true)
                    .frame(minWidth: 300)
            }
            .navigationTitle("SyncPad")
            .navigationBarTitleDisplayMode(.inline)
        }
    }

    // MARK: - Connection Status

    private var connectionStatus: some View {
        HStack(spacing: 4) {
            Circle()
                .fill(viewModel.isConnected ? Color.green : Color.red)
                .frame(width: 8, height: 8)

            Text(viewModel.isConnected ? "已连接" : "未连接")
                .font(.caption)
                .foregroundColor(.secondary)
        }
    }
}

#Preview {
    iOSContentView()
}
