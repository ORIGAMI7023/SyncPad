namespace SyncPad.Shared.Models;

/// <summary>
/// 通用 API 响应包装
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }

    public static ApiResponse<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    public static ApiResponse<T> Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// 无数据的 API 响应
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static ApiResponse Ok() => new() { Success = true };
    public static ApiResponse Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
