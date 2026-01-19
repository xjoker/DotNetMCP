using System.Text;
using System.Text.Json;

namespace DotNetMcp.Backend.Core.Paging;

/// <summary>
/// 游标编解码器 - 分页状态的编解码
/// 
/// Cursor 格式: Base64(JSON({offset, version, timestamp}))
/// - offset: 当前偏移量
/// - version: 索引版本（用于检测数据变更）
/// - timestamp: 游标创建时间
/// </summary>
public static class CursorCodec
{
    /// <summary>
    /// 编码分页游标
    /// </summary>
    /// <param name="offset">偏移量</param>
    /// <param name="version">索引版本</param>
    /// <returns>Base64 编码的游标字符串</returns>
    public static string Encode(int offset, string version)
    {
        var cursorData = new CursorData
        {
            Offset = offset,
            Version = version,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var json = JsonSerializer.Serialize(cursorData);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 解码分页游标
    /// </summary>
    /// <param name="cursor">Base64 编码的游标字符串</param>
    /// <returns>解码结果</returns>
    public static CursorDecodeResult Decode(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return CursorDecodeResult.Failure("Cursor cannot be null or empty");
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var json = Encoding.UTF8.GetString(bytes);
            var data = JsonSerializer.Deserialize<CursorData>(json);

            if (data == null)
            {
                return CursorDecodeResult.Failure("Invalid cursor data");
            }

            return CursorDecodeResult.Success(data);
        }
        catch (FormatException)
        {
            return CursorDecodeResult.Failure("Invalid Base64 format");
        }
        catch (JsonException ex)
        {
            return CursorDecodeResult.Failure($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CursorDecodeResult.Failure($"Decode error: {ex.Message}");
        }
    }

    /// <summary>
    /// 验证游标是否有效
    /// </summary>
    /// <param name="cursor">游标字符串</param>
    /// <param name="currentVersion">当前索引版本</param>
    /// <param name="maxAgeSeconds">游标最大有效期（秒），默认 1 小时</param>
    /// <returns>验证结果</returns>
    public static CursorValidationResult Validate(string cursor, string currentVersion, int maxAgeSeconds = 3600)
    {
        var decodeResult = Decode(cursor);
        if (!decodeResult.IsSuccess)
        {
            return CursorValidationResult.Invalid(decodeResult.ErrorMessage!);
        }

        var data = decodeResult.Data!;

        // 检查版本
        if (data.Version != currentVersion)
        {
            return CursorValidationResult.Expired("Index version mismatch", CursorExpireReason.VersionMismatch);
        }

        // 检查时效
        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var age = currentTimestamp - data.Timestamp;
        if (age > maxAgeSeconds)
        {
            return CursorValidationResult.Expired($"Cursor expired ({age}s > {maxAgeSeconds}s)", CursorExpireReason.Timeout);
        }

        return CursorValidationResult.Valid(data);
    }
}

/// <summary>
/// 游标数据
/// </summary>
public record CursorData
{
    public int Offset { get; init; }
    public string Version { get; init; } = string.Empty;
    public long Timestamp { get; init; }
}

/// <summary>
/// 游标解码结果
/// </summary>
public record CursorDecodeResult
{
    public bool IsSuccess { get; init; }
    public CursorData? Data { get; init; }
    public string? ErrorMessage { get; init; }

    public static CursorDecodeResult Success(CursorData data)
        => new() { IsSuccess = true, Data = data };

    public static CursorDecodeResult Failure(string message)
        => new() { IsSuccess = false, ErrorMessage = message };
}

/// <summary>
/// 游标验证结果
/// </summary>
public record CursorValidationResult
{
    public bool IsValid { get; init; }
    public bool IsExpired { get; init; }
    public CursorData? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public CursorExpireReason? ExpireReason { get; init; }

    public static CursorValidationResult Valid(CursorData data)
        => new() { IsValid = true, Data = data };

    public static CursorValidationResult Invalid(string message)
        => new() { IsValid = false, ErrorMessage = message };

    public static CursorValidationResult Expired(string message, CursorExpireReason reason)
        => new() { IsValid = false, IsExpired = true, ErrorMessage = message, ExpireReason = reason };
}

/// <summary>
/// 游标过期原因
/// </summary>
public enum CursorExpireReason
{
    VersionMismatch,  // 索引版本不匹配
    Timeout           // 超时
}
