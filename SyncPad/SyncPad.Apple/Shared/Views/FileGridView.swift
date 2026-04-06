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
                    if let cacheURL = await viewModel.downloadFile(file) {
                        #if os(macOS)
                        let downloadsDir = FileManager.default.urls(for: .downloadsDirectory, in: .userDomainMask).first!
                        let destURL = downloadsDir.appendingPathComponent(file.fileName)

                        // 处理同名文件
                        var finalURL = destURL
                        var counter = 1
                        while FileManager.default.fileExists(atPath: finalURL.path) {
                            let name = (file.fileName as NSString).deletingPathExtension
                            let ext = (file.fileName as NSString).pathExtension
                            let newName = ext.isEmpty ? "\(name) (\(counter))" : "\(name) (\(counter)).\(ext)"
                            finalURL = downloadsDir.appendingPathComponent(newName)
                            counter += 1
                        }

                        do {
                            try FileManager.default.copyItem(at: cacheURL, to: finalURL)
                        } catch {
                            let alert = NSAlert()
                            alert.messageText = "下载失败"
                            alert.informativeText = error.localizedDescription
                            alert.alertStyle = .warning
                            alert.addButton(withTitle: "确定")
                            alert.runModal()
                        }
                        #endif
                    }
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

    // MARK: - Open File

    private func openFile(_ file: FileItemDto) async {
        let cacheManager = FileCacheManager.shared
        if cacheManager.isCached(fileId: file.id, fileName: file.fileName) {
            let url = cacheManager.getCachePath(fileId: file.id, fileName: file.fileName)
            #if os(macOS)
            NSWorkspace.shared.open(url)
            #endif
        } else {
            #if os(macOS)
            let alert = NSAlert()
            alert.messageText = "文件未下载"
            alert.informativeText = "请先下载文件后再打开"
            alert.alertStyle = .informational
            alert.addButton(withTitle: "确定")
            alert.runModal()
            #endif
        }
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
