using Foundation;
using UIKit;

namespace SyncPad.Client.Platforms.MacCatalyst;

/// <summary>
/// Mac 端拖放处理器
/// 支持从 Finder 拖入文件到应用
/// </summary>
public static class DragDropHandler
{
    /// <summary>
    /// 为 MAUI 控件设置拖入支持（从 Finder 拖入文件）
    /// </summary>
    public static void SetupDropTarget(Microsoft.Maui.Controls.View mauiView,
        Func<IReadOnlyList<string>, Task> onFilesDropped)
    {
        mauiView.HandlerChanged += (s, e) =>
        {
            if (mauiView.Handler?.PlatformView is UIView uiView)
            {
                // 启用拖放交互
                var dropInteraction = new UIDropInteraction(new DropInteractionDelegate(onFilesDropped));
                uiView.AddInteraction(dropInteraction);

                System.Diagnostics.Debug.WriteLine("[MacDragDropHandler] 已设置拖放目标");
            }
        };
    }

    /// <summary>
    /// UIDropInteraction 委托实现
    /// </summary>
    private class DropInteractionDelegate : UIDropInteractionDelegate
    {
        private readonly Func<IReadOnlyList<string>, Task> _onFilesDropped;

        public DropInteractionDelegate(Func<IReadOnlyList<string>, Task> onFilesDropped)
        {
            _onFilesDropped = onFilesDropped;
        }

        public override bool CanHandleSession(UIDropInteraction interaction, IUIDropSession session)
        {
            // 检查是否包含文件
            return session.CanLoadObjects(new ObjCRuntime.Class(typeof(NSUrl)));
        }

        public override UIDropProposal SessionDidUpdate(UIDropInteraction interaction, IUIDropSession session)
        {
            // 返回复制操作提示
            return new UIDropProposal(UIDropOperation.Copy);
        }

        public override void PerformDrop(UIDropInteraction interaction, IUIDropSession session)
        {
            System.Diagnostics.Debug.WriteLine("[MacDragDropHandler] 执行拖放操作");

            session.LoadObjects<NSUrl>(urls =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var filePaths = new List<string>();

                    foreach (var url in urls)
                    {
                        if (url.IsFileUrl)
                        {
                            var path = url.Path;
                            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                            {
                                filePaths.Add(path);
                                System.Diagnostics.Debug.WriteLine($"[MacDragDropHandler] 检测到文件: {path}");
                            }
                        }
                    }

                    if (filePaths.Count > 0)
                    {
                        await _onFilesDropped(filePaths);
                    }
                });
            });
        }
    }
}
