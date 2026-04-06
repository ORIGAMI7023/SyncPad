import SwiftUI

// MARK: - File Grid View
struct FileGridView: View {
    @ObservedObject var viewModel: PadViewModel
    let enableDragDrop: Bool

    @State private var selectedFile: FileItemDto?
    @State private var showingDeleteAlert: Bool = false

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

            // File List
            if viewModel.files.isEmpty {
                emptyState
            } else {
                ScrollView {
                    LazyVStack(spacing: 0) {
                        ForEach(viewModel.files) { file in
                            fileItem(file)
                            if file.id != viewModel.files.last?.id {
                                Divider().padding(.leading, 44)
                            }
                        }
                    }
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
            onOpen: {
                Task {
                    await openFile(file)
                }
            },
            onDownload: {
                Task {
                    _ = await viewModel.downloadAndSaveToDownloads(file: file)
                }
            },
            onDelete: {
                selectedFile = file
                showingDeleteAlert = true
            },
            onRename: { newName in
                Task {
                    await viewModel.renameFile(file, newName: newName)
                }
            }
        )
    }

    // MARK: - Open File (统一流程：下载到缓存 → 复制到 Downloads → 用默认应用打开)

    private func openFile(_ file: FileItemDto) async {
        guard let finalURL = await viewModel.downloadAndSaveToDownloads(file: file) else { return }
        #if os(macOS)
        NSWorkspace.shared.open(finalURL)
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
