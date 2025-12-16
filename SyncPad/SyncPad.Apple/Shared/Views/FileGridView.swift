import SwiftUI

// MARK: - File Grid View
struct FileGridView: View {
    @ObservedObject var viewModel: PadViewModel
    let enableDragDrop: Bool

    @State private var selectedFile: FileItemDto?
    @State private var showingDeleteAlert: Bool = false

    private let columns = [
        GridItem(.adaptive(minimum: 100, maximum: 120), spacing: 16)
    ]

    var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack {
                Text("文件")
                    .font(.headline)

                Spacer()

                if viewModel.isLoadingFiles {
                    ProgressView()
                        .scaleEffect(0.7)
                }

                Button(action: {
                    Task { await viewModel.refreshFiles() }
                }) {
                    Image(systemName: "arrow.clockwise")
                }
                .buttonStyle(.plain)
            }
            .padding()

            Divider()

            // File Grid
            if viewModel.files.isEmpty {
                emptyState
            } else {
                ScrollView {
                    LazyVGrid(columns: columns, spacing: 16) {
                        ForEach(viewModel.files) { file in
                            fileItem(file)
                        }
                    }
                    .padding()
                }
            }
        }
        .alert("确认删除", isPresented: $showingDeleteAlert) {
            Button("取消", role: .cancel) {}
            Button("删除", role: .destructive) {
                if let file = selectedFile {
                    Task { await viewModel.deleteFile(file) }
                }
            }
        } message: {
            if let file = selectedFile {
                Text("确定要删除 \"\(file.fileName)\" 吗？")
            }
        }
        #if os(macOS)
        .onDrop(of: [.fileURL], isTargeted: nil) { providers in
            guard enableDragDrop else { return false }
            handleDrop(providers: providers)
            return true
        }
        #endif
    }

    // MARK: - Empty State

    private var emptyState: some View {
        VStack(spacing: 12) {
            Image(systemName: "folder.badge.plus")
                .font(.system(size: 48))
                .foregroundColor(.secondary)

            Text("暂无文件")
                .foregroundColor(.secondary)

            #if os(macOS)
            if enableDragDrop {
                Text("拖放文件到此处上传")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            #endif
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - File Item

    private func fileItem(_ file: FileItemDto) -> some View {
        FileItemView(
            file: file,
            onTap: {
                Task {
                    if let url = await viewModel.downloadFile(file) {
                        #if os(macOS)
                        NSWorkspace.shared.open(url)
                        #else
                        // iOS: 使用 QuickLook 或 Share Sheet
                        #endif
                    }
                }
            },
            onDelete: {
                selectedFile = file
                showingDeleteAlert = true
            }
        )
        #if os(macOS)
        .onDrag {
            // 拖出文件
            if let cachedURL = viewModel.getCachedURL(file) {
                return NSItemProvider(contentsOf: cachedURL) ?? NSItemProvider()
            } else {
                // 如果未缓存，先下载
                Task {
                    _ = await viewModel.downloadFile(file)
                }
                return NSItemProvider()
            }
        }
        #endif
    }

    // MARK: - Drop Handler

    #if os(macOS)
    private func handleDrop(providers: [NSItemProvider]) {
        for provider in providers {
            provider.loadItem(forTypeIdentifier: "public.file-url", options: nil) { item, error in
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
    }
    #endif
}
