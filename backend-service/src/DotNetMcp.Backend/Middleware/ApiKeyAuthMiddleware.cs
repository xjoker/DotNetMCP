using System.Text;

namespace DotNetMcp.Backend.Middleware;

/// <summary>
/// API Key 认证中间件
/// 支持 X-API-Key 头和 Authorization: Bearer 头
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly HashSet<string> _validApiKeys;
    private readonly bool _authEnabled;
    private static readonly string[] _excludedPaths = ["/", "/health", "/openapi"];

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;

        // 从环境变量或配置加载 API Keys
        var apiKeysConfig = configuration.GetValue<string>("API_KEYS") 
            ?? Environment.GetEnvironmentVariable("API_KEYS")
            ?? "";
        
        _validApiKeys = apiKeysConfig
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .ToHashSet();

        _authEnabled = _validApiKeys.Count > 0;
        
        if (_authEnabled)
        {
            _logger.LogInformation("API Key authentication enabled with {Count} keys", _validApiKeys.Count);
        }
        else
        {
            _logger.LogWarning("API Key authentication disabled - no API_KEYS configured");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 跳过排除路径
        if (_excludedPaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // 如果未启用认证，允许所有请求
        if (!_authEnabled)
        {
            await _next(context);
            return;
        }

        // 提取 API Key
        string? apiKey = null;
        
        // 尝试从 X-API-Key 头获取
        if (context.Request.Headers.TryGetValue("X-API-Key", out var xApiKey))
        {
            apiKey = xApiKey.FirstOrDefault();
        }
        // 尝试从 Authorization: Bearer 头获取
        else if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authValue = authHeader.FirstOrDefault();
            if (authValue?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                apiKey = authValue[7..];
            }
        }

        // 验证 API Key
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Missing API Key from {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"success":false,"error":"UNAUTHORIZED","message":"API Key required"}""");
            return;
        }

        if (!_validApiKeys.Contains(apiKey))
        {
            _logger.LogWarning("Invalid API Key from {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"success":false,"error":"FORBIDDEN","message":"Invalid API Key"}""");
            return;
        }

        // 将 API Key 添加到 HttpContext.Items 供下游使用
        context.Items["ApiKey"] = apiKey;
        
        await _next(context);
    }
}

/// <summary>
/// 扩展方法
/// </summary>
public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthMiddleware>();
    }
}
