using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SyncPad.Server.Data;
using SyncPad.Server.Data.Entities;
using SyncPad.Shared.Models;

namespace SyncPad.Server.Core.Services;

public class FileService : IFileService
{
    private readonly SyncPadDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly string _storagePath;
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(24);

    public FileService(SyncPadDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _storagePath = configuration["FileStorage:Path"] ?? "data/files";

        // 确保存储目录存在
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<List<FileItemDto>> GetFilesAsync(int userId)
    {
        return await _context.FileItems
            .Where(f => f.UserId == userId && !f.IsDeleted)
            .OrderBy(f => f.PositionY)
            .ThenBy(f => f.PositionX)
            .Select(f => new FileItemDto
            {
                Id = f.Id,
                FileName = f.FileName,
                FileSize = f.FileSize,
                MimeType = f.MimeType,
                UploadedAt = f.UploadedAt,
                ExpiresAt = f.ExpiresAt,
                PositionX = f.PositionX,
                PositionY = f.PositionY
            })
            .ToListAsync();
    }

    public async Task<bool> FileExistsAsync(int userId, string fileName)
    {
        return await _context.FileItems
            .AnyAsync(f => f.UserId == userId && f.FileName == fileName && !f.IsDeleted);
    }

    public async Task<FileUploadResponse> UploadFileAsync(
        int userId, string fileName, Stream stream, string? mimeType, bool overwrite = false)
    {
        // 检查同名文件
        var existingFile = await _context.FileItems
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FileName == fileName && !f.IsDeleted);

        if (existingFile != null && !overwrite)
        {
            return new FileUploadResponse
            {
                Success = false,
                ErrorMessage = "FILE_EXISTS" // 客户端据此弹窗确认
            };
        }

        // 计算 hash 并保存文件
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var fileBytes = ms.ToArray();
        var hash = ComputeSha256Hash(fileBytes);
        var fileSize = fileBytes.Length;

        // 检查内容是否已存在（包括已删除文件的内容，实现秒传）
        var fileContent = await _context.FileContents
            .FirstOrDefaultAsync(fc => fc.ContentHash == hash);

        if (fileContent == null)
        {
            // 写入物理文件
            var filePath = GetFilePath(hash);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllBytesAsync(filePath, fileBytes);

            // 创建 FileContent 记录
            fileContent = new FileContent
            {
                ContentHash = hash,
                ReferenceCount = 0,
                FileSize = fileSize,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };
            _context.FileContents.Add(fileContent);
        }
        else
        {
            // 复用已有内容（秒传），验证物理文件存在
            var filePath = GetFilePath(hash);
            if (!File.Exists(filePath))
            {
                // 物理文件丢失，重新写入
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await File.WriteAllBytesAsync(filePath, fileBytes);
            }
        }

        // 更新引用计数
        fileContent.ReferenceCount++;
        fileContent.LastAccessedAt = DateTime.UtcNow;

        // 如果覆盖，先逻辑删除旧文件
        if (existingFile != null && overwrite)
        {
            await DeleteFileInternalAsync(existingFile);
        }

        // 获取下一个可用位置（网格坐标）
        var (posX, posY) = await GetNextAvailablePositionAsync(userId);

        // 创建 FileItem 记录
        var now = DateTime.UtcNow;
        var fileItem = new FileItem
        {
            UserId = userId,
            FileName = fileName,
            FileSize = fileSize,
            ContentHash = hash,
            MimeType = mimeType,
            UploadedAt = now,
            ExpiresAt = now.Add(_defaultTtl),
            IsDeleted = false,
            PositionX = posX,
            PositionY = posY
        };

        _context.FileItems.Add(fileItem);
        await _context.SaveChangesAsync();

        return new FileUploadResponse
        {
            Success = true,
            File = new FileItemDto
            {
                Id = fileItem.Id,
                FileName = fileItem.FileName,
                FileSize = fileItem.FileSize,
                MimeType = fileItem.MimeType,
                UploadedAt = fileItem.UploadedAt,
                ExpiresAt = fileItem.ExpiresAt,
                PositionX = fileItem.PositionX,
                PositionY = fileItem.PositionY
            }
        };
    }

    public async Task<(Stream? Stream, string? MimeType, string? FileName, long FileSize)> DownloadFileAsync(int userId, int fileId)
    {
        var fileItem = await _context.FileItems
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && !f.IsDeleted);

        if (fileItem == null)
            return (null, null, null, 0);

        var filePath = GetFilePath(fileItem.ContentHash);
        if (!File.Exists(filePath))
            return (null, null, null, 0);

        // 更新最后访问时间
        var fileContent = await _context.FileContents
            .FirstOrDefaultAsync(fc => fc.ContentHash == fileItem.ContentHash);
        if (fileContent != null)
        {
            fileContent.LastAccessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, fileItem.MimeType, fileItem.FileName, fileItem.FileSize);
    }

    public async Task<bool> DeleteFileAsync(int userId, int fileId)
    {
        var fileItem = await _context.FileItems
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && !f.IsDeleted);

        if (fileItem == null)
            return false;

        await DeleteFileInternalAsync(fileItem);
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task DeleteFileInternalAsync(FileItem fileItem)
    {
        fileItem.IsDeleted = true;
        fileItem.DeletedAt = DateTime.UtcNow;

        // 减少引用计数
        var fileContent = await _context.FileContents
            .FirstOrDefaultAsync(fc => fc.ContentHash == fileItem.ContentHash);

        if (fileContent != null)
        {
            fileContent.ReferenceCount--;
        }
    }

    public async Task CleanupExpiredFilesAsync()
    {
        var now = DateTime.UtcNow;

        // 清理过期的 FileItem（已过期或已删除超过 7 天，便于去重复用）
        var expiredItems = await _context.FileItems
            .Where(f => f.ExpiresAt < now || (f.IsDeleted && f.DeletedAt < now.AddDays(-7)))
            .ToListAsync();

        foreach (var item in expiredItems)
        {
            var fileContent = await _context.FileContents
                .FirstOrDefaultAsync(fc => fc.ContentHash == item.ContentHash);

            if (fileContent != null)
            {
                fileContent.ReferenceCount--;
            }

            _context.FileItems.Remove(item);
        }

        // 清理引用计数为 0 且超过 TTL 的物理文件（保留 7 天）
        var orphanContents = await _context.FileContents
            .Where(fc => fc.ReferenceCount <= 0 && fc.LastAccessedAt < now.AddDays(-7))
            .ToListAsync();

        foreach (var content in orphanContents)
        {
            var filePath = GetFilePath(content.ContentHash);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            _context.FileContents.Remove(content);
        }

        await _context.SaveChangesAsync();
    }

    private string GetFilePath(string hash)
    {
        var dir1 = hash[..2];
        var dir2 = hash[2..4];
        return Path.Combine(_storagePath, dir1, dir2, $"{hash}.bin");
    }

    private static string ComputeSha256Hash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<bool> UpdateFilePositionAsync(int userId, int fileId, int positionX, int positionY)
    {
        var fileItem = await _context.FileItems
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && !f.IsDeleted);

        if (fileItem == null)
            return false;

        fileItem.PositionX = positionX;
        fileItem.PositionY = positionY;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(int X, int Y)> GetNextAvailablePositionAsync(int userId)
    {
        const int maxColumns = 4; // 固定 4 列

        // 获取用户所有文件的位置（包括已删除的，避免位置冲突）
        var occupiedPositions = await _context.FileItems
            .Where(f => f.UserId == userId && !f.IsDeleted)
            .Select(f => new { f.PositionX, f.PositionY })
            .ToListAsync();

        var occupied = new HashSet<(int, int)>(
            occupiedPositions.Select(p => (p.PositionX, p.PositionY))
        );

        // 左优先、上优先遍历查找第一个空位
        for (int y = 0; y < 1000; y++) // 最多 1000 行
        {
            for (int x = 0; x < maxColumns; x++)
            {
                if (!occupied.Contains((x, y)))
                {
                    return (x, y);
                }
            }
        }

        // 如果没有空位（理论上不会发生），返回 (0, 0)
        return (0, 0);
    }
}
