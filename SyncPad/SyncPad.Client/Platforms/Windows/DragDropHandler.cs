using Microsoft.UI.Xaml;
using Windows.Storage;
using WinDataTransfer = Windows.ApplicationModel.DataTransfer;

namespace SyncPad.Client.Platforms.Windows;

/// <summary>
/// Windows 平台拖放处理器（仅支持外部文件拖入）
/// </summary>
public static class DragDropHandler
{
    /// <summary>
    /// 为 MAUI 控件设置 Windows 原生拖入支持
    /// </summary>
    public static void SetupDropTarget(Microsoft.Maui.Controls.View mauiView,
        Func<IReadOnlyList<StorageFile>, double, double, Task> onFilesDropped)
    {
        mauiView.HandlerChanged += (s, e) =>
        {
            if (mauiView.Handler?.PlatformView is UIElement uiElement)
            {
                uiElement.AllowDrop = true;

                uiElement.DragOver += (sender, args) =>
                {
                    if (args.DataView.Contains(WinDataTransfer.StandardDataFormats.StorageItems))
                    {
                        args.AcceptedOperation = WinDataTransfer.DataPackageOperation.Copy;
                        args.DragUIOverride.Caption = "上传文件";
                    }

                    args.Handled = true;
                };

                uiElement.Drop += async (sender, args) =>
                {
                    if (args.DataView.Contains(WinDataTransfer.StandardDataFormats.StorageItems))
                    {
                        var items = await args.DataView.GetStorageItemsAsync();
                        var files = items.OfType<StorageFile>().ToList();
                        if (files.Count > 0)
                        {
                            var position = args.GetPosition(sender as UIElement);
                            await onFilesDropped(files, position.X, position.Y);
                        }
                        args.Handled = true;
                    }
                };
            }
        };
    }
}
