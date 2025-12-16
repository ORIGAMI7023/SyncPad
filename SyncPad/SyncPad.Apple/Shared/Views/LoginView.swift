import SwiftUI

// MARK: - Login View
struct LoginView: View {
    @StateObject private var viewModel = LoginViewModel()
    @State private var showPassword: Bool = false

    var body: some View {
        VStack(spacing: 20) {
            Spacer()

            // Logo / Title
            VStack(spacing: 8) {
                Image(systemName: "doc.text.fill")
                    .font(.system(size: 60))
                    .foregroundColor(.blue)

                Text("SyncPad")
                    .font(.largeTitle)
                    .fontWeight(.bold)
            }
            .padding(.bottom, 40)

            // Login Form
            VStack(spacing: 16) {
                // Username
                TextField("用户名", text: $viewModel.username)
                    .textFieldStyle(.roundedBorder)
                    #if os(iOS)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .keyboardType(.asciiCapable)
                    #endif

                // Password
                HStack {
                    if showPassword {
                        TextField("密码", text: $viewModel.password)
                            .textFieldStyle(.roundedBorder)
                    } else {
                        SecureField("密码", text: $viewModel.password)
                            .textFieldStyle(.roundedBorder)
                    }

                    Button(action: { showPassword.toggle() }) {
                        Image(systemName: showPassword ? "eye.slash" : "eye")
                            .foregroundColor(.gray)
                    }
                    .buttonStyle(.plain)
                }

                // Error Message
                if let error = viewModel.errorMessage {
                    Text(error)
                        .foregroundColor(.red)
                        .font(.caption)
                }

                // Login Button
                Button(action: {
                    Task {
                        await viewModel.login()
                    }
                }) {
                    if viewModel.isLoading {
                        ProgressView()
                            .frame(maxWidth: .infinity)
                    } else {
                        Text("登录")
                            .frame(maxWidth: .infinity)
                    }
                }
                .buttonStyle(.borderedProminent)
                .disabled(viewModel.isLoading || viewModel.username.isEmpty || viewModel.password.isEmpty)
                .controlSize(.large)
            }
            .frame(maxWidth: 300)

            Spacer()

            // Footer
            Text("© 2024 SyncPad")
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .padding()
        #if os(macOS)
        .frame(minWidth: 400, minHeight: 500)
        #endif
    }
}

#Preview {
    LoginView()
}
