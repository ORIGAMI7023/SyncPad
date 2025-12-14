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
            TextEditor(text: $localText)
                .font(.body)
                .padding(8)
                .onChange(of: localText) { newValue in
                    onTextChanged(newValue)
                }
                .onChange(of: text) { newValue in
                    if localText != newValue {
                        localText = newValue
                    }
                }
        }
        .onAppear {
            localText = text
        }
    }
}
