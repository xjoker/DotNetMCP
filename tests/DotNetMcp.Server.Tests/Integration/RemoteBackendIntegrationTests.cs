extern alias BackendApp;
using System.Net;
using System.Net.Http.Json;
using DotNetMcp.Server.Backend;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetMcp.Server.Tests.Integration;

/// <summary>
/// RemoteBackend HTTP 集成测试
/// 使用 WebApplicationFactory&lt;Program&gt; 启动 Backend in-process，
/// 验证 RemoteBackend 的 HTTP 路由、序列化和中间件（API Key）行为。
/// </summary>
[Collection("RemoteBackend")]
public class RemoteBackendIntegrationTests : IClassFixture<BackendWebAppFactory>
{
    private readonly BackendWebAppFactory _factory;

    // 使用本测试程序集自身作为 fixture DLL（与 EndToEndWorkflowTests 相同策略）
    private static readonly string FixtureDllPath =
        typeof(RemoteBackendIntegrationTests).Assembly.Location;

    public RemoteBackendIntegrationTests(BackendWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// 创建一个连接到 in-process Backend 的 RemoteBackend 实例
    /// </summary>
    private RemoteBackend CreateRemoteBackend(string? apiKey = null)
    {
        // 不使用真实 HttpClientFactory，直接包装 WebApplicationFactory 提供的 HttpClient
        var httpClient = _factory.CreateClient();
        var factory = new SingletonHttpClientFactory(httpClient);
        var logger = NullLogger<RemoteBackend>.Instance;

        var backend = new RemoteBackend(
            id: "test-remote",
            name: "Test Remote Backend",
            endpoint: "http://localhost",
            httpClientFactory: factory,
            logger: logger
        );
        backend.ApiKey = apiKey;
        backend.TimeoutSeconds = 15;
        return backend;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 健康检查
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_WhenServerRunning_ReturnsTrue()
    {
        // Arrange
        var backend = CreateRemoteBackend();

        // Act
        var healthy = await backend.CheckHealthAsync();

        // Assert — GET /instance/health 应返回 200
        Assert.True(healthy);
        Assert.True(backend.IsHealthy);
        Assert.NotNull(backend.LastHealthCheck);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServerUnreachable_ReturnsFalse()
    {
        // Arrange: 指向不存在的端点
        var factory = new SingletonHttpClientFactory(new HttpClient
        {
            BaseAddress = new Uri("http://localhost:19999")
        });
        var backend = new RemoteBackend(
            id: "bad-remote", name: "Bad", endpoint: "http://localhost:19999",
            httpClientFactory: factory,
            logger: NullLogger<RemoteBackend>.Instance
        );

        // Act
        var healthy = await backend.CheckHealthAsync();

        // Assert
        Assert.False(healthy);
        Assert.False(backend.IsHealthy);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API Key 鉴权
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKeyAuth_WithCorrectKey_AllowsRequest()
    {
        // Arrange: 启动一个要求 API Key 的 factory
        using var securedFactory = BackendWebAppFactoryBase.WithApiKey("test-secret-key");
        var backend = CreateSecuredRemoteBackend(securedFactory, apiKey: "test-secret-key");

        // Act
        var healthy = await backend.CheckHealthAsync();

        // Assert: 健康检查走 /instance/health（需要 Auth）
        Assert.True(healthy);
    }

    [Fact]
    public async Task ApiKeyAuth_WithoutKey_HealthCheckExcluded_ReturnsOk()
    {
        // Arrange: GET / 和 /instance/health 应被 ApiKeyAuthMiddleware 排除
        var httpClient = _factory.CreateClient();

        // Act: 直接请求 /instance/health（不带 API Key，无需认证）
        var response = await httpClient.GetAsync("/instance/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyAuth_MissingKey_ProtectedEndpoint_Returns401()
    {
        // Arrange: 启动需要 API Key 的 factory，客户端不带 key
        using var securedFactory = BackendWebAppFactoryBase.WithApiKey("secret-key-xyz");
        var httpClient = securedFactory.CreateClient();

        // Act: 请求受保护的端点（不带 API Key）
        var response = await httpClient.GetAsync("/instance/list");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyAuth_WrongKey_Returns403()
    {
        // Arrange
        using var securedFactory = BackendWebAppFactoryBase.WithApiKey("correct-key");
        var httpClient = securedFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-API-Key", "wrong-key");

        // Act
        var response = await httpClient.GetAsync("/instance/list");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LoadAssembly
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAssemblyAsync_WithValidPath_ReturnsSuccessViaDirect()
    {
        // Arrange: 直接 POST /assembly/load 验证 HTTP 层正常工作
        var httpClient = _factory.CreateClient();
        var requestBody = new { path = FixtureDllPath };

        // Act
        var response = await httpClient.PostAsJsonAsync("/assembly/load", requestBody);
        var json = await response.Content.ReadFromJsonAsync<LoadAssemblyRawResponse>();

        // Assert: HTTP 层正确接受并加载了程序集
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(json);
        Assert.True(json!.Success);
        Assert.NotNull(json.Data?.Mvid);
        Assert.NotNull(json.Data?.Name);
    }

    [Fact]
    public async Task LoadAssemblyAsync_WithInvalidPath_ReturnsBadRequest()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var requestBody = new { path = "/nonexistent/path/assembly.dll" };

        // Act
        var response = await httpClient.PostAsJsonAsync("/assembly/load", requestBody);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LoadAssemblyAsync_ViaRemoteBackend_ReturnsSuccess()
    {
        // Arrange: RemoteBackend 正确解析 envelope 格式 {success, data: {mvid, name, version}}
        var backend = CreateRemoteBackend();

        // Act
        var result = await backend.LoadAssemblyAsync(FixtureDllPath);

        // Assert: envelope 反序列化正确，成功加载程序集
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.RemoteMvid);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ListAssemblies
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAssembliesAsync_ViaHttp_ReturnsOkAndInstances()
    {
        // Arrange: 先加载一个程序集，再列出
        var httpClient = _factory.CreateClient();
        await httpClient.PostAsJsonAsync("/assembly/load", new { path = FixtureDllPath });

        // Act
        var response = await httpClient.GetAsync("/instance/list");
        var json = await response.Content.ReadFromJsonAsync<ListInstancesRawResponse>();

        // Assert: HTTP 层正确
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(json?.Data?.Instances);
        Assert.NotEmpty(json!.Data!.Instances!);
    }

    [Fact]
    public async Task ListAssembliesAsync_ViaRemoteBackend_ReturnsLoadedAssembly()
    {
        // Arrange: 先通过 RemoteBackend 加载程序集，再列出，验证 envelope 反序列化正确
        var backend = CreateRemoteBackend();
        var loadResult = await backend.LoadAssemblyAsync(FixtureDllPath);
        Assert.True(loadResult.IsSuccess, $"Pre-condition: load should succeed, but got: {loadResult.ErrorMessage}");

        // Act
        var result = await backend.ListAssembliesAsync();

        // Assert: 正确解析 {data: {instances: [...]}}，能找到刚加载的程序集
        Assert.NotEmpty(result);
        Assert.Contains(result, a => a.Mvid == loadResult.RemoteMvid);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UnloadAssembly
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnloadAssemblyAsync_WithNonExistentMvid_ReturnsFalse()
    {
        // Arrange
        var backend = CreateRemoteBackend();
        var fakeMvid = Guid.NewGuid().ToString();

        // Act — DELETE /instance/{mvid} 返回 404, RemoteBackend 应返回 false
        var result = await backend.UnloadAssemblyAsync(fakeMvid);

        // Assert
        Assert.False(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SearchTypes 错误传播
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchTypesAsync_WithNoAssemblyLoaded_ReturnsErrorResult()
    {
        // Arrange: 使用独立 factory 确保没有加载的程序集
        using var emptyFactory = new BackendWebAppFactory(); // 无 API Key，无预加载程序集
        var httpClient = emptyFactory.CreateClient();
        var backendFactory = new SingletonHttpClientFactory(httpClient);
        var backend = new RemoteBackend(
            "search-test", "Search Test", "http://localhost",
            backendFactory, NullLogger<RemoteBackend>.Instance
        );

        // Act: 使用假 mvid 进行搜索
        var result = await backend.SearchTypesAsync(
            Guid.NewGuid().ToString(), "TestClass");

        // Assert: 无程序集时，RemoteBackend 应返回 IsSuccess=false 不抛异常
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 超时验证
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoteBackend_Timeout_CancelsRequestAfterTimeoutSeconds()
    {
        // Arrange: 使用一个会立即超时的 HttpClient（通过极短超时设置）
        var slowHandler = new SlowResponseHandler(delayMs: 3000);
        var slowHttpClient = new HttpClient(slowHandler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var backendFactory = new SingletonHttpClientFactory(slowHttpClient);
        var backend = new RemoteBackend(
            "slow-backend", "Slow Backend", "http://localhost",
            backendFactory, NullLogger<RemoteBackend>.Instance
        )
        {
            TimeoutSeconds = 1 // 1 秒超时，而响应 handler 延迟 3 秒
        };

        // Act: LoadAssembly 会因超时而取消
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await backend.LoadAssemblyAsync("/some/path.dll");
        sw.Stop();

        // Assert: 应在约 1 秒内失败，且返回 Failure 而不是抛异常
        Assert.False(result.IsSuccess);
        Assert.True(sw.Elapsed.TotalSeconds < 2.5,
            $"Expected timeout within 2.5s, actual: {sw.Elapsed.TotalSeconds:F2}s");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 错误传播（非存在 mvid）
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DecompileTypeAsync_WithUnknownMvid_ReturnsErrorMessage_NotException()
    {
        // Arrange
        var backend = CreateRemoteBackend();
        var fakeMvid = Guid.NewGuid().ToString();

        // Act — 不应抛异常，应返回带 ErrorMessage 的 Failure Result
        var result = await backend.DecompileTypeAsync(fakeMvid, "SomeNamespace.SomeType");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task SearchStringsAsync_WithUnknownMvid_ReturnsErrorResult_NotException()
    {
        // Arrange
        var backend = CreateRemoteBackend();
        var fakeMvid = Guid.NewGuid().ToString();

        // Act
        var result = await backend.SearchStringsAsync(fakeMvid, "hello");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 辅助方法
    // ─────────────────────────────────────────────────────────────────────────

    private static RemoteBackend CreateSecuredRemoteBackend(BackendWebAppFactoryBase factory, string? apiKey)
    {
        var httpClient = factory.CreateClient();
        if (apiKey != null)
            httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        var backendFactory = new SingletonHttpClientFactory(httpClient);
        var backend = new RemoteBackend(
            "secured-remote", "Secured Remote", "http://localhost",
            backendFactory, NullLogger<RemoteBackend>.Instance
        );
        backend.ApiKey = apiKey;
        backend.TimeoutSeconds = 15;
        return backend;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Test Infrastructure
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// WebApplicationFactory 封装（无 API Key）：供 IClassFixture 使用
/// </summary>
public class BackendWebAppFactory : BackendWebAppFactoryBase
{
    public BackendWebAppFactory() : base(apiKey: null) { }
}

/// <summary>
/// WebApplicationFactory 基类：启动 Backend in-process，支持可选 API Key 配置
/// </summary>
public abstract class BackendWebAppFactoryBase : WebApplicationFactory<BackendApp::Program>
{
    private readonly string? _apiKey;
    private readonly string? _originalEnvValue;

    protected BackendWebAppFactoryBase(string? apiKey = null)
    {
        _apiKey = apiKey;
        // ApiKeyAuthMiddleware 读取 Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        // 而非 IHostEnvironment，必须在进程级别设置才能让 middleware 读到
        _originalEnvValue = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        if (_apiKey != null)
        {
            // ApiKeyAuthMiddleware 优先读 IConfiguration["API_KEYS"]
            builder.UseSetting("API_KEYS", _apiKey);
        }

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders(); // 抑制测试日志噪音
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 恢复原始环境变量值
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _originalEnvValue);
        }
        base.Dispose(disposing);
    }

    /// <summary>创建带 API Key 的 factory 实例</summary>
    public static BackendWebAppFactoryBase WithApiKey(string key)
        => new SecuredBackendWebAppFactory(key);

    private sealed class SecuredBackendWebAppFactory : BackendWebAppFactoryBase
    {
        public SecuredBackendWebAppFactory(string apiKey) : base(apiKey) { }
    }
}

/// <summary>
/// 包装单个 HttpClient 的工厂（用于将 WebApplicationFactory 的测试 client 注入 RemoteBackend）
/// </summary>
internal class SingletonHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public SingletonHttpClientFactory(HttpClient client) => _client = client;

    public HttpClient CreateClient(string name) => _client;
}

/// <summary>
/// 模拟慢速响应的 HttpMessageHandler，用于超时测试
/// </summary>
internal class SlowResponseHandler : HttpMessageHandler
{
    private readonly int _delayMs;

    public SlowResponseHandler(int delayMs) => _delayMs = delayMs;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 等待指定时间，CancellationToken 取消时会抛出 OperationCanceledException
        await Task.Delay(_delayMs, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Raw response DTOs（用于直接 HTTP 验证，绕过 RemoteBackend 的反序列化问题）
// ─────────────────────────────────────────────────────────────────────────────

internal class LoadAssemblyRawResponse
{
    public bool Success { get; set; }
    public LoadAssemblyDataDto? Data { get; set; }
}

internal class LoadAssemblyDataDto
{
    public string? Mvid { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
}

internal class ListInstancesRawResponse
{
    public bool Success { get; set; }
    public ListInstancesDataDto? Data { get; set; }
}

internal class ListInstancesDataDto
{
    public List<InstanceDto>? Instances { get; set; }
    public int Count { get; set; }
}

internal class InstanceDto
{
    public string? Mvid { get; set; }
    public string? Name { get; set; }
}
