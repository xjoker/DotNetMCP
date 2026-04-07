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
    private readonly bool _isProduction;
    private static readonly string[] _excludedPaths = ["/health", "/openapi"];

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

        // 检查是否为生产环境
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        _isProduction = environment.Equals("Production", StringComparison.OrdinalIgnoreCase);

        if (_authEnabled)
        {
            _logger.LogInformation("API Key authentication enabled with {Count} keys", _validApiKeys.Count);
        }
        else if (_isProduction)
        {
            _logger.LogCritical("SECURITY WARNING: No API keys configured in Production! All non-exempt endpoints will require authentication. Set API_KEYS environment variable.");
        }
        else
        {
            _logger.LogWarning("API Key authentication disabled - no API_KEYS configured (Development mode)");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 跳过排除路径（精确匹配根路径，或 StartsWithSegments 匹配子路径）
        var requestPath = context.Request.Path;
        if (requestPath.Equals("/") || _excludedPaths.Any(p => requestPath.Equals(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // 如果未启用认证：Production 拒绝请求，Development 允许
        if (!_authEnabled)
        {
            if (_isProduction)
            {
                _logger.LogWarning("Rejected unauthenticated request to {Path} in Production (no API keys configured)", requestPath);
                context.Response.StatusCode = 503;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""{"success":false,"error":"SERVICE_UNAVAILABLE","message":"API key authentication not configured. Set API_KEYS environment variable."}""");
                return;
            }
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
