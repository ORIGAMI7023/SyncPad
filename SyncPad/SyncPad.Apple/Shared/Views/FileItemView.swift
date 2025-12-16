import SwiftUI

// MARK: - File Item View
struct FileItemView: View {
    let file: FileItemDto
    let onTap: () -> Void
    let onDelete: () -> Void

    @State private var isHovering: Bool = false
    @ObservedObject private var cacheManager = FileCacheManager.shared

    var body: some View {
        VStack(spacing: 8) {
            // File Icon
            ZStack {
                RoundedRectangle(cornerRadius: 8)
                    .fill(Color.gray.opacity(0.1))
                    .frame(width: 80, height: 80)

                fileIcon
                    .font(.system(size: 36))
                    .foregroundColor(iconColor)

                // Download Progress
                if cacheManager.getStatus(fileId: file.id) == .downloading {
                    ProgressView()
                        .scaleEffect(0.8)
                }
            }

            // File Name
            Text(file.fileName)
                .font(.caption)
                .lineLimit(2)
                .multilineTextAlignment(.center)
                .frame(width: 90)

            // File Size
            Text(formatFileSize(file.fileSize))
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
        .onTapGesture {
            onTap()
        }
        .contextMenu {
            Button(role: .destructive) {
                onDelete()
            } label: {
                Label("删除", systemImage: "trash")
            }
        }
    }

    // MARK: - File Icon

    private var fileIcon: Image {
        let ext = (file.fileName as NSString).pathExtension.lowercased()

        switch ext {
        case "jpg", "jpeg", "png", "gif", "bmp", "webp":
            return Image(systemName: "photo")
        case "pdf":
            return Image(systemName: "doc.richtext")
        case "doc", "docx":
            return Image(systemName: "doc.text")
        case "xls", "xlsx":
            return Image(systemName: "tablecells")
        case "ppt", "pptx":
            return Image(systemName: "rectangle.on.rectangle")
        case "mp3", "wav", "m4a":
            return Image(systemName: "music.note")
        case "mp4", "mov", "avi":
            return Image(systemName: "film")
        case "zip", "rar", "7z":
            return Image(systemName: "doc.zipper")
        case "txt":
            return Image(systemName: "doc.plaintext")
        default:
            return Image(systemName: "doc")
        }
    }

    private var iconColor: Color {
        let ext = (file.fileName as NSString).pathExtension.lowercased()

        switch ext {
        case "jpg", "jpeg", "png", "gif", "bmp", "webp":
            return .orange
        case "pdf":
            return .red
        case "doc", "docx":
            return .blue
        case "xls", "xlsx":
            return .green
        case "ppt", "pptx":
            return .orange
        case "mp3", "wav", "m4a":
            return .pink
        case "mp4", "mov", "avi":
            return .purple
        case "zip", "rar", "7z":
            return .yellow
        default:
            return .gray
        }
    }

    // MARK: - Helpers

    private func formatFileSize(_ bytes: Int64) -> String {
        let formatter = ByteCountFormatter()
        formatter.countStyle = .file
        return formatter.string(fromByteCount: bytes)
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
            expiresAt: Date().addingTimeInterval(86400 * 7),
            positionX: 0,
            positionY: 0
        ),
        onTap: {},
        onDelete: {}
    )
}
