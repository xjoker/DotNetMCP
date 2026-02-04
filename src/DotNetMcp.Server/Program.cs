using DotNetMcp.Server.Backend;
using DotNetMcp.Server.Configuration;
using DotNetMcp.Server.Tools;
using DotNetMcp.Server.Prompts;
using DotNetMcp.Backend.Services;
using Microsoft.Extensions.Options;

// 解析命令行参数
var transportMode = "http";
var host = "localhost";
var port = 5000;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--stdio":
            transportMode = "stdio";
            break;
        case "--host" when i + 1 < args.Length:
            host = args[++i];
            break;
        case "--port" when i + 1 < args.Length:
            if (int.TryParse(args[++i], out var p)) port = p;
            break;
        case "-h":
        case "--help":
            Console.WriteLine("DotNetMcp.Server - .NET Assembly Analysis MCP Server");
            Console.WriteLine();
            Console.WriteLine("Usage: DotNetMcp.Server [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --stdio          Use stdio transport (for Claude Desktop)");
            Console.WriteLine("  --host <ip>      Bind to specified IP (default: localhost)");
            Console.WriteLine("  --port <port>    Bind to specified port (default: 5000)");
            Console.WriteLine("  -h, --help       Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  DotNetMcp.Server                      # HTTP mode on localhost:5000");
            Console.WriteLine("  DotNetMcp.Server --host 0.0.0.0       # HTTP mode on all interfaces");
            Console.WriteLine("  DotNetMcp.Server --port 8080          # HTTP mode on port 8080");
            Console.WriteLine("  DotNetMcp.Server --stdio              # Stdio mode for Claude Desktop");
            return;
    }
}

var builder = WebApplication.CreateBuilder(args);

// 配置绑定地址（HTTP 模式）
if (transportMode == "http")
{
    builder.WebHost.UseUrls($"http://{host}:{port}");
}

// 配置日志输出到 stderr（MCP 协议要求 stdout 用于 JSON-RPC）
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// 绑定配置
builder.Services.Configure<McpServerOptions>(
    builder.Configuration.GetSection(McpServerOptions.SectionName));

// 注册 Backend 服务
builder.Services.AddSingleton<IAssemblyManager, AssemblyManager>();
builder.Services.AddSingleton<AnalysisService>();
builder.Services.AddSingleton<ModificationService>();

// 注册后端抽象层
builder.Services.AddSingleton<IBackendRegistry, BackendRegistry>();
builder.Services.AddSingleton<LocalBackend>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<BackendHealthMonitor>();

// 配置 MCP Server
if (transportMode == "stdio")
{
    Console.Error.WriteLine("[MCP] Starting in stdio mode...");
    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "dotnet-mcp",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(AssemblyTools).Assembly)
    .WithPromptsFromAssembly(typeof(AnalysisPrompts).Assembly);
}
else
{
    Console.Error.WriteLine($"[MCP] Starting in HTTP mode on http://{host}:{port}");
    Console.Error.WriteLine($"[MCP] MCP endpoint: http://{host}:{port}/mcp");
    Console.Error.WriteLine($"[MCP] Health check: http://{host}:{port}/health");
    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "dotnet-mcp",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(AssemblyTools).Assembly)
    .WithPromptsFromAssembly(typeof(AnalysisPrompts).Assembly);
}

var app = builder.Build();

// 初始化后端
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DotNetMcp.Server");
var registry = app.Services.GetRequiredService<IBackendRegistry>();
var mcpOptions = app.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;

if (mcpOptions.EnableLocalBackend)
{
    var localBackend = app.Services.GetRequiredService<LocalBackend>();
    registry.Register(localBackend);
    registry.SetDefault(localBackend.Id);
    logger.LogInformation("Local backend registered and set as default");
}

// 注册配置的远程后端
foreach (var config in mcpOptions.RemoteBackends)
{
    var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
    var remoteLogger = app.Services.GetRequiredService<ILogger<RemoteBackend>>();
    var remoteBackend = new RemoteBackend(config.Id, config.Name, config.Endpoint, httpClientFactory, remoteLogger)
    {
        ApiKey = config.ApiKey,
        TimeoutSeconds = config.TimeoutSeconds
    };
    registry.Register(remoteBackend);
    logger.LogInformation("Remote backend registered: {Id} -> {Endpoint}", config.Id, config.Endpoint);
}

// HTTP 模式下映射 MCP 端点
if (transportMode == "http")
{
    app.MapMcp("/mcp");

    // 健康检查端点
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

    // 根路径提示
    app.MapGet("/", () => Results.Ok(new
    {
        name = "DotNetMcp.Server",
        version = "1.0.0",
        mcp_endpoint = "/mcp",
        health_endpoint = "/health",
        transport = "http"
    }));
}

logger.LogInformation("DotNetMcp.Server started successfully");
await app.RunAsync();
