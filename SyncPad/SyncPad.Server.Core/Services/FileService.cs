using System.IO.Hashing;
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
    private readonly TimeSpan _defaultTtl = TimeSpan.FromDays(7);

    public FileService(SyncPadDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _storagePath = configuration["FileStorage:Path"] ?? "data/files";

        Directory.CreateDirectory(_storagePath);
    }

    public async Task<List<FileItemDto>> GetFilesAsync(int userId)
    {
        var now = DateTime.UtcNow;
        return await _context.FileItems
            .Where(f => f.UserId == userId && f.Status == "active" && f.ExpiresAt > now)
            .OrderBy(f => f.UploadedAt).ThenBy(f => f.Id)
            .Select(f => new FileItemDto
            {
                Id = f.Id,
                FileName = f.FileName,
                FileSize = f.FileSize,
                Hash = f.Hash,
                MimeType = f.MimeType,
                UploadedAt = f.UploadedAt,
                ExpiresAt = f.ExpiresAt
            })
            .ToListAsync();
    }

    public async Task<CheckHashResult> CheckHashAsync(string hash)
    {
        var fileItem = await _context.FileItems
            .FirstOrDefaultAsync(f => f.Hash == hash);

        if (fileItem == null)
        {
            return new CheckHashResult { Exists = false, Status = null };
        }

        return new CheckHashResult { Exists = true, Status = fileItem.Status };
    }

    public async Task<FileUploadResponse> UploadFileAsync(
        int userId, string fileName, Stream stream, string? mimeType, string hash)
    {
        // 计算 XXHash64 验证
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var fileBytes = ms.ToArray();
        var computedHash = ComputeXxHash64(fileBytes);

        if (computedHash != hash)
        {
            return new FileUploadResponse
            {
                Success = false,
                ErrorMessage = "文件哈希校验失败"
            };
        }

        var fileSize = fileBytes.Length;

        // 检查是否已有同 hash 记录
        var existingItem = await _context.FileItems.FirstOrDefaultAsync(f => f.Hash == hash);
        if (existingItem != null)
        {
            if (existingItem.Status == "cached")
            {
                // 激活已有记录
                return await ActivateExistingAsync(userId, fileName, existingItem, mimeType, fileSize);
            }
            // status=active，同 hash 已存在
            return new FileUploadResponse
            {
                Success = false,
                ErrorMessage = "FILE_EXISTS"
            };
        }

        // 写入物理文件
        var filePath = GetFilePath(hash);
        if (!File.Exists(filePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllBytesAsync(filePath, fileBytes);
        }

        // 创建 FileItem 记录
        var now = DateTime.UtcNow;
        var fileItem = new FileItem
        {
            UserId = userId,
            FileName = fileName,
            FileSize = fileSize,
            Hash = hash,
            MimeType = mimeType,
            UploadedAt = now,
            ExpiresAt = now.Add(_defaultTtl),
            Status = "active"
        };

        _context.FileItems.Add(fileItem);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException sqliteEx
                  && sqliteEx.SqliteErrorCode == 19) // UNIQUE constraint failed
        {
            // 并发冲突：另一请求已插入同 hash
            _context.Entry(fileItem).State = EntityState.Detached;
            var concurrent = await _context.FileItems.FirstOrDefaultAsync(f => f.Hash == hash);
            if (concurrent != null && concurrent.Status == "cached")
            {
                return await ActivateExistingAsync(userId, fileName, concurrent, mimeType, fileSize);
            }
            return new FileUploadResponse
            {
                Success = false,
                ErrorMessage = "FILE_EXISTS"
            };
        }

        return new FileUploadResponse
        {
            Success = true,
            File = new FileItemDto
            {
                Id = fileItem.Id,
                FileName = fileItem.FileName,
                FileSize = fileItem.FileSize,
                Hash = fileItem.Hash,
                MimeType = fileItem.MimeType,
                UploadedAt = fileItem.UploadedAt,
                ExpiresAt = fileItem.ExpiresAt
            }
        };
    }

    public async Task<FileUploadResponse> InstantUploadAsync(int userId, string fileName, string hash)
    {
        var existingItem = await _context.FileItems.FirstOrDefaultAsync(f => f.Hash == hash);
        if (existingItem == null)
        {
            return new FileUploadResponse
            {
                Success = false,
                ErrorMessage = "HASH_NOT_FOUND"
            };
        }

        if (existingItem.Status == "cached")
        {
            return await ActivateExistingAsync(userId, fileName, existingItem, null, existingItem.FileSize);
        }

        // status=active
        return new FileUploadResponse
        {
            Success = false,
            ErrorMessage = "FILE_EXISTS"
        };
    }

    private async Task<FileUploadResponse> ActivateExistingAsync(
        int userId, string fileName, FileItem existingItem, string? mimeType, long fileSize)
    {
        var now = DateTime.UtcNow;
        existingItem.UserId = userId;
        existingItem.FileName = fileName;
        existingItem.FileSize = fileSize;
        existingItem.MimeType = mimeType;
        existingItem.UploadedAt = now;
        existingItem.ExpiresAt = now.Add(_defaultTtl);
        existingItem.Status = "active";

        await _context.SaveChangesAsync();

        return new FileUploadResponse
        {
            Success = true,
            File = new FileItemDto
            {
                Id = existingItem.Id,
                FileName = existingItem.FileName,
                FileSize = existingItem.FileSize,
                Hash = existingItem.Hash,
                MimeType = existingItem.MimeType,
                UploadedAt = existingItem.UploadedAt,
                ExpiresAt = existingItem.ExpiresAt
            }
        };
    }

    public async Task<(Stream? Stream, string? MimeType, string? FileName, long FileSize)> DownloadFileAsync(int userId, int fileId)
    {
        var now = DateTime.UtcNow;
        var fileItem = await _context.FileItems
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && f.Status == "active" && f.ExpiresAt > now);

        if (fileItem == null)
            return (null, null, null, 0);

        var filePath = GetFilePath(fileItem.Hash);
        if (!File.Exists(filePath))
            return (null, null, null, 0);

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, fileItem.MimeType, fileItem.FileName, fileItem.FileSize);
    }

    public async Task<bool> DeleteFileAsync(int userId, int fileId)
    {
        var fileItem = await _context.FileItems
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && f.Status == "active");

        if (fileItem == null)
            return false;

        fileItem.Status = "cached";
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<FileItemDto?> RenameFileAsync(int userId, int fileId, string newFileName)
    {
        var fileItem = await _context.FileItems
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && f.Status == "active");

        if (fileItem == null)
            return null;

        fileItem.FileName = newFileName;
        await _context.SaveChangesAsync();

        return new FileItemDto
        {
            Id = fileItem.Id,
            FileName = fileItem.FileName,
            FileSize = fileItem.FileSize,
            Hash = fileItem.Hash,
            MimeType = fileItem.MimeType,
            UploadedAt = fileItem.UploadedAt,
            ExpiresAt = fileItem.ExpiresAt
        };
    }

    public async Task CleanupExpiredFilesAsync()
    {
        var now = DateTime.UtcNow;

        // 清理 status=cached 且已过期的记录
        var expiredItems = await _context.FileItems
            .Where(f => f.Status == "cached" && f.ExpiresAt < now)
            .ToListAsync();

        foreach (var item in expiredItems)
        {
            // 检查是否有其他 active 记录引用同 hash
            var hasActive = await _context.FileItems
                .AnyAsync(f => f.Hash == item.Hash && f.Status == "active");

            if (!hasActive)
            {
                // 没有活跃引用，删除磁盘文件
                var filePath = GetFilePath(item.Hash);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            _context.FileItems.Remove(item);
        }

        // 清理 status=active 但已过期的记录（也改为 cached 再清理）
        var activeExpired = await _context.FileItems
            .Where(f => f.Status == "active" && f.ExpiresAt < now)
            .ToListAsync();

        foreach (var item in activeExpired)
        {
            item.Status = "cached";
        }

        await _context.SaveChangesAsync();
    }

    private string GetFilePath(string hash)
    {
        return Path.Combine(_storagePath, $"{hash}.dat");
    }

    private static string ComputeXxHash64(byte[] data)
    {
        var hash = XxHash64.HashToUInt64(data);
        return hash.ToString("x16");
    }
}
