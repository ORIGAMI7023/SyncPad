import SwiftUI

// MARK: - Text Editor View
struct TextEditorContentView: View {
    @Binding var text: String
    let onTextChanged: (String) -> Void

    @State private var localText: String = ""

    var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack {
                Text("文本")
                    .font(.headline)

                Spacer()
            }
            .padding()

            Divider()

            // Text Editor
            #if os(macOS)
            TextEditor(text: $localText)
                .font(.body)
                .padding(8)
                .onChange(of: localText) { _, newValue in
                    onTextChanged(newValue)
                }
                .onChange(of: text) { _, newValue in
                    if localText != newValue {
                        localText = newValue
                    }
                }
            #else
            TextEditor(text: $localText)
                .font(.body)
                .padding(8)
                .onChange(of: localText) { _, newValue in
                    onTextChanged(newValue)
                }
                .onChange(of: text) { _, newValue in
                    if localText != newValue {
                        localText = newValue
                    }
                }
            #endif
        }
        .onAppear {
            localText = text
        }
    }
}
