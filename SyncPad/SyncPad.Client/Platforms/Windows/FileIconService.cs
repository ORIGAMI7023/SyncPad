using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices;
using Windows.Storage;

namespace SyncPad.Client.Platforms.Windows;

/// <summary>
/// Windows 平台文件图标提取服务
/// 使用 Shell API 获取真实的文件图标
/// </summary>
public class FileIconService
{
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
        public uint dwAttributes;
    }

    private static readonly Dictionary<string, BitmapImage> _iconCache = new();

    /// <summary>
    /// 获取文件的系统图标（异步版本）
    /// </summary>
    /// <param name="filePath">文件路径（可以是不存在的文件，只要扩展名正确）</param>
    /// <param name="large">是否获取大图标（true=32x32, false=16x16）</param>
    /// <returns>BitmapImage 或 null</returns>
    public static async Task<BitmapImage?> GetFileIconAsync(string filePath, bool large = true)
    {
        return await Task.Run(() => GetFileIcon(filePath, large));
    }

    /// <summary>
    /// 获取文件的系统图标
    /// </summary>
    /// <param name="filePath">文件路径（可以是不存在的文件，只要扩展名正确）</param>
    /// <param name="large">是否获取大图标（true=32x32, false=16x16）</param>
    /// <returns>BitmapImage 或 null</returns>
    public static BitmapImage? GetFileIcon(string filePath, bool large = true)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var cacheKey = $"{extension}_{large}";

            // 使用缓存
            if (_iconCache.TryGetValue(cacheKey, out var cachedIcon))
            {
                return cachedIcon;
            }

            SHFILEINFO shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            flags |= large ? SHGFI_LARGEICON : SHGFI_SMALLICON;

            IntPtr hImgSmall = SHGetFileInfo(
                filePath,
                0,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                flags);

            if (hImgSmall == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                // 将 HICON 转换为 BitmapImage
                var icon = System.Drawing.Icon.FromHandle(shinfo.hIcon);
                var bitmap = icon.ToBitmap();

                // 转换为 BitmapImage
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();
                    var randomAccessStream = memory.AsRandomAccessStream();

                    // 在 UI 线程上设置源
                    bitmapImage.SetSource(randomAccessStream);

                    // 缓存图标
                    _iconCache[cacheKey] = bitmapImage;

                    return bitmapImage;
                }
            }
            finally
            {
                // 释放图标句柄
                DestroyIcon(shinfo.hIcon);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取文件图标失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 根据文件名获取虚拟文件图标（不需要实际文件存在）
    /// </summary>
    public static async Task<BitmapImage?> GetIconForFileNameAsync(string fileName, bool large = true)
    {
        // 创建虚拟路径，Shell API 可以根据扩展名返回图标
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        return await GetFileIconAsync(tempPath, large);
    }

    /// <summary>
    /// 清除图标缓存
    /// </summary>
    public static void ClearCache()
    {
        _iconCache.Clear();
    }
}
