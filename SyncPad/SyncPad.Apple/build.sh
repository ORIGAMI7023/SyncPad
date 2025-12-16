#!/bin/bash

# SyncPad macOS 快速构建脚本（无需密码）
# 使用方法：./build.sh [debug|release]

CONFIG=${1:-Debug}  # 默认使用 Debug 配置

echo "🔨 构建 SyncPad-macOS ($CONFIG)..."

xcodebuild \
  -scheme SyncPad-macOS \
  -configuration "$CONFIG" \
  -destination 'platform=macOS' \
  CODE_SIGN_IDENTITY="" \
  CODE_SIGNING_REQUIRED=NO \
  CODE_SIGNING_ALLOWED=NO \
  build

if [ $? -eq 0 ]; then
  echo "✅ 构建成功！"
  echo "📦 应用位置: ~/Library/Developer/Xcode/DerivedData/SyncPad-*/Build/Products/$CONFIG/SyncPad-macOS.app"

  # 询问是否运行
  read -p "是否运行应用？(y/n) " -n 1 -r
  echo
  if [[ $REPLY =~ ^[Yy]$ ]]; then
    APP_PATH=$(find ~/Library/Developer/Xcode/DerivedData/SyncPad-*/Build/Products/$CONFIG/SyncPad-macOS.app -type d 2>/dev/null | head -1)
    if [ -n "$APP_PATH" ]; then
      echo "🚀 启动应用..."
      open "$APP_PATH"
    fi
  fi
else
  echo "❌ 构建失败"
  exit 1
fi
