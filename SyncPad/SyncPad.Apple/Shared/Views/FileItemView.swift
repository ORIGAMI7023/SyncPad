import SwiftUI

// MARK: - File Item View
struct FileItemView: View {
    let file: FileItemDto
    let onOpen: () -> Void
    let onDownload: () -> Void
    let onDelete: () -> Void
    let onRename: ((String) -> Void)?

    @State private var isHovering: Bool = false
    @State private var showRenameDialog: Bool = false
    @State private var newFileName: String = ""

    var body: some View {
        #if os(macOS)
        macOSListItem
        #else
        iOSGridItem
        #endif
    }

    // MARK: - macOS List Item

    #if os(macOS)
    private var macOSListItem: some View {
        HStack(spacing: 12) {
            // File Icon
            ZStack {
                RoundedRectangle(cornerRadius: 6)
                    .fill(iconColor.opacity(0.15))
                    .frame(width: 32, height: 32)

                fileIcon
                    .font(.system(size: 16))
                    .foregroundColor(iconColor)
            }

            // File Name
            Text(file.fileName)
                .font(.body)
                .lineLimit(1)
                .truncationMode(.middle)

            Spacer()

            // File Size & Type
            HStack(spacing: 4) {
                Text(formatFileSize(file.fileSize))
                Text("·")
                Text(fileType.rawValue)
            }
            .font(.caption)
            .foregroundColor(.secondary)

            // Upload Time
            Text(formatDate(file.uploadedAt))
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(
            RoundedRectangle(cornerRadius: 6)
                .fill(isHovering ? Color.blue.opacity(0.1) : Color.clear)
        )
        .onHover { hovering in
            isHovering = hovering
        }
        .contextMenu {
            Button {
                onOpen()
            } label: {
                Label("打开", systemImage: "doc.text")
            }

            Button {
                onDownload()
            } label: {
                Label("下载", systemImage: "arrow.down.doc")
            }

            Button {
                newFileName = file.fileName
                showRenameDialog = true
            } label: {
                Label("重命名", systemImage: "pencil")
            }

            Divider()

            Button(role: .destructive) {
                onDelete()
            } label: {
                Label("删除", systemImage: "trash")
            }
        }
        .alert("重命名文件", isPresented: $showRenameDialog) {
            TextField("新文件名", text: $newFileName)
            Button("取消", role: .cancel) {}
            Button("确定") {
                if !newFileName.isEmpty {
                    onRename?(newFileName)
                }
            }
        } message: {
            Text("请输入新的文件名")
        }
    }
    #endif

    // MARK: - iOS Grid Item

    private var iOSGridItem: some View {
        VStack(spacing: 8) {
            // File Icon
            ZStack {
                RoundedRectangle(cornerRadius: 8)
                    .fill(Color.gray.opacity(0.1))
                    .frame(width: 80, height: 80)

                fileIcon
                    .font(.system(size: 36))
                    .foregroundColor(iconColor)
            }

            // File Name
            Text(file.fileName)
                .font(.caption)
                .lineLimit(2)
                .multilineTextAlignment(.center)
                .frame(width: 90)

            // File Size & Type
            HStack(spacing: 4) {
                Text(formatFileSize(file.fileSize))
                Text("·")
                Text(fileType.rawValue)
            }
            .font(.caption2)
            .foregroundColor(.secondary)

            // Upload Time
            Text(formatDate(file.uploadedAt))
                .font(.caption2)
                .foregroundColor(.secondary)
        }
        .padding(8)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(isHovering ? Color.blue.opacity(0.1) : Color.clear)
        )
        .onHover { hovering in
            isHovering = hovering
        }
        .contextMenu {
            Button {
                onOpen()
            } label: {
                Label("打开", systemImage: "doc.text")
            }

            Button {
                onDownload()
            } label: {
                Label("下载", systemImage: "arrow.down.doc")
            }

            Button {
                newFileName = file.fileName
                showRenameDialog = true
            } label: {
                Label("重命名", systemImage: "pencil")
            }

            Divider()

            Button(role: .destructive) {
                onDelete()
            } label: {
                Label("删除", systemImage: "trash")
            }
        }
        .alert("重命名文件", isPresented: $showRenameDialog) {
            TextField("新文件名", text: $newFileName)
            Button("取消", role: .cancel) {}
            Button("确定") {
                if !newFileName.isEmpty {
                    onRename?(newFileName)
                }
            }
        } message: {
            Text("请输入新的文件名")
        }
    }

    // MARK: - File Type

    private var fileType: FileType {
        let ext = (file.fileName as NSString).pathExtension.lowercased()
        switch ext {
        case "jpg", "jpeg", "png", "gif", "bmp", "webp":
            return .image
        case "pdf":
            return .pdf
        case "doc", "docx":
            return .document
        case "xls", "xlsx":
            return .spreadsheet
        case "ppt", "pptx":
            return .presentation
        case "mp3", "wav", "m4a":
            return .audio
        case "mp4", "mov", "avi":
            return .video
        case "zip", "rar", "7z":
            return .archive
        case "txt":
            return .text
        default:
            return .other
        }
    }

    // MARK: - File Icon

    private var fileIcon: Image {
        switch fileType {
        case .image:
            return Image(systemName: "photo")
        case .pdf:
            return Image(systemName: "doc.richtext")
        case .document:
            return Image(systemName: "doc.text")
        case .spreadsheet:
            return Image(systemName: "tablecells")
        case .presentation:
            return Image(systemName: "rectangle.on.rectangle")
        case .audio:
            return Image(systemName: "music.note")
        case .video:
            return Image(systemName: "film")
        case .archive:
            return Image(systemName: "doc.zipper")
        case .text:
            return Image(systemName: "doc.plaintext")
        case .other:
            return Image(systemName: "doc")
        }
    }

    private var iconColor: Color {
        switch fileType {
        case .image:    return .orange
        case .pdf:      return .red
        case .document: return .blue
        case .spreadsheet: return .green
        case .presentation: return .orange
        case .audio:    return .pink
        case .video:    return .purple
        case .archive:  return .yellow
        case .text:     return .gray
        case .other:    return .gray
        }
    }

    // MARK: - Helpers

    private func formatFileSize(_ bytes: Int64) -> String {
        let formatter = ByteCountFormatter()
        formatter.countStyle = .file
        return formatter.string(fromByteCount: bytes)
    }

    private func formatDate(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy/MM/dd"
        return formatter.string(from: date)
    }
}

#Preview {
    FileItemView(
        file: FileItemDto(
            id: 1,
            fileName: "test.pdf",
            fileSize: 1024 * 1024,
            mimeType: "application/pdf",
            uploadedAt: Date(),
            expiresAt: Date().addingTimeInterval(86400 * 7)
        ),
        onOpen: {
            print("打开文件")
        },
        onDownload: {},
        onDelete: {},
        onRename: { newName in
            print("重命名: \(newName)")
        }
    )
}
