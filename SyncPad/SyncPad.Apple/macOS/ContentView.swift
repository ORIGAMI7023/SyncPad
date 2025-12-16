import SwiftUI
import AppKit
import UniformTypeIdentifiers

// MARK: - macOS Content View
struct macOSContentView: View {
    @StateObject private var viewModel = PadViewModel()
    @StateObject private var authManager = AuthManager.shared

    @State private var splitPosition: CGFloat = 0.5

    var body: some View {
        HSplitView {
            // 左侧：文本编辑区
            TextEditorContentView(
                text: $viewModel.textContent,
                onTextChanged: viewModel.onTextChanged
            )
            .frame(minWidth: 400, idealWidth: 600)
            .layoutPriority(1)

            // 右侧：文件区
            FileGridView(viewModel: viewModel, enableDragDrop: true)
                .frame(minWidth: 250, idealWidth: 350, maxWidth: 500)
        }
        .toolbar {
            ToolbarItem(placement: .automatic) {
                connectionStatus
            }

            ToolbarItem(placement: .automatic) {
                Button(action: {
                    Task { await viewModel.refreshFiles() }
                }) {
                    Image(systemName: "arrow.clockwise")
                }
                .help("刷新文件列表")
            }

            ToolbarItem(placement: .automatic) {
                Button(action: importFiles) {
                    Image(systemName: "plus")
                }
                .help("添加文件")
            }

            ToolbarItem(placement: .automatic) {
                Menu {
                    Text("用户: \(authManager.username ?? "未知")")
                        .foregroundColor(.secondary)

                    Divider()

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
        .task {
            await viewModel.connect()
        }
        .onDisappear {
            viewModel.disconnect()
        }
        .onDrop(of: [.fileURL], isTargeted: nil) { providers in
            handleFileDrop(providers: providers)
            return true
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

    // MARK: - Import Files

    private func importFiles() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.canChooseFiles = true

        if panel.runModal() == .OK {
            for url in panel.urls {
                Task {
                    await viewModel.uploadFile(url: url)
                }
            }
        }
    }

    // MARK: - File Drop

    private func handleFileDrop(providers: [NSItemProvider]) -> Bool {
        for provider in providers {
            provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier, options: nil) { item, error in
                guard error == nil,
                      let data = item as? Data,
                      let url = URL(dataRepresentation: data, relativeTo: nil) else {
                    return
                }

                Task { @MainActor in
                    await viewModel.uploadFile(url: url)
                }
            }
        }
        return true
    }
}

#Preview {
    macOSContentView()
}
