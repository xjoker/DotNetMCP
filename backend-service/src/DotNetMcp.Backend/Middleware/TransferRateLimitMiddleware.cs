using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace DotNetMcp.Backend.Middleware;

/// <summary>
/// 传输速率限制中间件 - 限制文件上传/下载请求频率
/// </summary>
public class TransferRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimits = new();
    
    // 配置
    private const int MaxRequestsPerMinute = 60;
    private const long MaxUploadSizeBytes = 100 * 1024 * 1024; // 100 MB
    
    // 允许的文件扩展名
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".pdb", ".xml", ".json", ".config", ".snk", ".pfx"
    };

    public TransferRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 只检查传输端点
        if (!context.Request.Path.StartsWithSegments("/transfer"))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientId(context);
        
        // 速率限制检查
        if (!CheckRateLimit(clientId))
        {
            context.Response.StatusCode = 429; // Too Many Requests
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error_code = "RATE_LIMIT_EXCEEDED",
                message = $"Rate limit exceeded. Max {MaxRequestsPerMinute} requests per minute.",
                retry_after = 60
            });
            return;
        }

        // 上传大小检查
        if (context.Request.ContentLength > MaxUploadSizeBytes)
        {
            context.Response.StatusCode = 413; // Payload Too Large
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error_code = "PAYLOAD_TOO_LARGE",
                message = $"File too large. Maximum size is {MaxUploadSizeBytes / 1024 / 1024} MB."
            });
            return;
        }

        // 文件类型检查 (对于上传请求)
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync();
            foreach (var file in form.Files)
            {
                var extension = Path.GetExtension(file.FileName);
                if (!string.IsNullOrEmpty(extension) && !AllowedExtensions.Contains(extension))
                {
                    context.Response.StatusCode = 415; // Unsupported Media Type
                    await context.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        error_code = "UNSUPPORTED_FILE_TYPE",
                        message = $"File type '{extension}' not allowed.",
                        allowed_types = AllowedExtensions.ToList()
                    });
                    return;
                }
            }
        }

        // 记录请求
        RecordRequest(clientId);

        await _next(context);
    }

    private string GetClientId(HttpContext context)
    {
        // 优先使用 API Key，否则使用 IP
        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
        {
            return $"key:{apiKey}";
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ip}";
    }

    private bool CheckRateLimit(string clientId)
    {
        CleanupExpired();
        
        if (_rateLimits.TryGetValue(clientId, out var info))
        {
            if (info.WindowStart.AddMinutes(1) < DateTime.UtcNow)
            {
                // 窗口过期，重置
                info.WindowStart = DateTime.UtcNow;
                info.RequestCount = 0;
            }

            return info.RequestCount < MaxRequestsPerMinute;
        }

        return true;
    }

    private void RecordRequest(string clientId)
    {
        _rateLimits.AddOrUpdate(
            clientId,
            _ => new RateLimitInfo { WindowStart = DateTime.UtcNow, RequestCount = 1 },
            (_, info) =>
            {
                if (info.WindowStart.AddMinutes(1) < DateTime.UtcNow)
                {
                    info.WindowStart = DateTime.UtcNow;
                    info.RequestCount = 1;
                }
                else
                {
                    info.RequestCount++;
                }
                return info;
            }
        );
    }

    private void CleanupExpired()
    {
        var expired = _rateLimits
            .Where(kvp => kvp.Value.WindowStart.AddMinutes(5) < DateTime.UtcNow)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expired)
        {
            _rateLimits.TryRemove(key, out _);
        }
    }

    private class RateLimitInfo
    {
        public DateTime WindowStart { get; set; }
        public int RequestCount { get; set; }
    }
}

public static class TransferRateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseTransferRateLimit(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TransferRateLimitMiddleware>();
    }
}
