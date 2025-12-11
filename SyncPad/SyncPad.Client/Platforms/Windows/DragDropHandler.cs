using Microsoft.UI.Xaml;
using Windows.Storage;
using WinDataTransfer = Windows.ApplicationModel.DataTransfer;

namespace SyncPad.Client.Platforms.Windows;

/// <summary>
/// Windows 平台拖放处理器
/// </summary>
public static class DragDropHandler
{
    /// <summary>
    /// 为 MAUI 控件设置 Windows 原生拖放支持（拖入）
    /// </summary>
    public static void SetupDropTarget(Microsoft.Maui.Controls.View mauiView,
        Func<IReadOnlyList<StorageFile>, Task> onFilesDropped,
        Func<int, double, double, Task>? onInternalDrop = null,
        Action<double, double>? onDragOver = null,
        Action? onDragLeave = null)
    {
        mauiView.HandlerChanged += (s, e) =>
        {
            if (mauiView.Handler?.PlatformView is UIElement uiElement)
            {
                uiElement.AllowDrop = true;

                uiElement.DragOver += (sender, args) =>
                {
                    // 简化判断：如果包含 Text 数据，就认为是内部拖动
                    // 因为只有内部拖动才会通过 FileDragDropBehavior 设置 Text 数据
                    // 外部文件拖入只包含 StorageItems，不包含 Text
                    bool hasTextData = args.DataView.Contains(WinDataTransfer.StandardDataFormats.Text);
                    bool hasStorageItems = args.DataView.Contains(WinDataTransfer.StandardDataFormats.StorageItems);

                    // 内部拖动优先处理（有 Text 数据表示是我们的内部拖动）
                    if (hasTextData)
                    {
                        args.AcceptedOperation = WinDataTransfer.DataPackageOperation.Copy;
                        args.DragUIOverride.Caption = "移动到此位置";

                        // 通知位置变化（显示指示器）
                        var position = args.GetPosition(sender as UIElement);
                        onDragOver?.Invoke(position.X, position.Y);
                    }
                    // 纯外部文件拖入（只有 StorageItems，没有 Text）
                    else if (hasStorageItems)
                    {
                        args.AcceptedOperation = WinDataTransfer.DataPackageOperation.Copy;
                        args.DragUIOverride.Caption = "上传文件";
                        onDragLeave?.Invoke();
                    }

                    args.Handled = true;
                };

                uiElement.DragLeave += (sender, args) =>
                {
                    onDragLeave?.Invoke();
                };

                uiElement.Drop += async (sender, args) =>
                {
                    onDragLeave?.Invoke();
                    System.Diagnostics.Debug.WriteLine($"[DragDropHandler] Drop事件触发");

                    // 检查是否是内部拖动（通过检查文本数据）
                    if (args.DataView.Contains(WinDataTransfer.StandardDataFormats.Text))
                    {
                        var text = await args.DataView.GetTextAsync();
                        System.Diagnostics.Debug.WriteLine($"[DragDropHandler] 检测到文本数据: {text}");

                        if (text.StartsWith("SyncPad-Internal-Drag-"))
                        {
                            System.Diagnostics.Debug.WriteLine($"[DragDropHandler] 识别为内部拖动");

                            // 提取文件ID
                            if (int.TryParse(text.Replace("SyncPad-Internal-Drag-", ""), out int fileId))
                            {
                                // 如果提供了内部拖放回调，调用它
                                if (onInternalDrop != null)
                                {
                                    // 获取拖放位置（相对于 uiElement）
                                    var position = args.GetPosition(sender as UIElement);
                                    System.Diagnostics.Debug.WriteLine($"[DragDropHandler] 内部拖动: FileId={fileId}, Position=({position.X},{position.Y})");
                                    await onInternalDrop(fileId, position.X, position.Y);
                                }
                            }

                            args.Handled = true;
                            return;
                        }
                    }

                    // 检查是否包含外部文件
                    if (args.DataView.Contains(WinDataTransfer.StandardDataFormats.StorageItems))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DragDropHandler] 检测到外部文件拖入");
                        var items = await args.DataView.GetStorageItemsAsync();
                        var files = items.OfType<StorageFile>().ToList();
                        if (files.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DragDropHandler] 处理 {files.Count} 个外部文件");
                            await onFilesDropped(files);
                        }
                        args.Handled = true;
                    }
                };
            }
        };
    }

    /// <summary>
    /// 为 MAUI 控件设置 Windows 原生拖出支持
    /// </summary>
    /// <param name="mauiView">MAUI 控件</param>
    /// <param name="getFilePath">获取要拖出的文件路径的函数，返回 null 表示不能拖出</param>
    public static void SetupDragSource(Microsoft.Maui.Controls.View mauiView, Func<string?> getFilePath)
    {
        mauiView.HandlerChanged += (s, e) =>
        {
            if (mauiView.Handler?.PlatformView is UIElement uiElement)
            {
                uiElement.CanDrag = true;
                uiElement.DragStarting += async (sender, args) =>
                {
                    var filePath = getFilePath();
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        try
                        {
                            // 使用 Deferral 来等待异步操作完成
                            var deferral = args.GetDeferral();
                            try
                            {
                                var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
                                args.Data.SetStorageItems(new[] { storageFile });
                                args.Data.RequestedOperation = WinDataTransfer.DataPackageOperation.Copy;
                            }
                            finally
                            {
                                deferral.Complete();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"设置拖出文件失败: {ex.Message}");
                            args.Cancel = true;
                        }
                    }
                    else
                    {
                        args.Cancel = true;
                    }
                };
            }
        };
    }

    /// <summary>
    /// 为 CollectionView 中的项设置拖出支持
    /// 需要在每个项的控件创建后调用
    /// </summary>
    public static void SetupItemDragSource(UIElement uiElement, Func<string?> getFilePath)
    {
        uiElement.CanDrag = true;
        uiElement.DragStarting += async (sender, args) =>
        {
            var filePath = getFilePath();
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    var deferral = args.GetDeferral();
                    try
                    {
                        var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
                        args.Data.SetStorageItems(new[] { storageFile });
                        args.Data.RequestedOperation = WinDataTransfer.DataPackageOperation.Copy;
                    }
                    finally
                    {
                        deferral.Complete();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"设置拖出文件失败: {ex.Message}");
                    args.Cancel = true;
                }
            }
            else
            {
                // 文件未缓存，取消系统拖出但允许内部拖动
                // 不设置 Cancel，让 MAUI 的拖放继续处理内部排序
            }
        };
    }
}
