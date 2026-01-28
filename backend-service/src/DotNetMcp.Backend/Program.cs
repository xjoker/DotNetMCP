using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 添加控制器
builder.Services.AddControllers();

// 注册服务
builder.Services.AddSingleton<IAssemblyManager, AssemblyManager>();
builder.Services.AddSingleton<ModificationService>();
builder.Services.AddSingleton<AnalysisService>();
builder.Services.AddSingleton<TransferTokenStore>();

// OpenAPI
builder.Services.AddOpenApi();

// 日志配置
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// 开发环境启用 OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// API Key 认证中间件
app.UseApiKeyAuth();

// 映射控制器路由
app.MapControllers();

// 健康检查端点
app.MapGet("/", () => new
{
    service = "DotNet MCP Backend",
    version = "0.3.0",
    status = "running",
    auth_enabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("API_KEYS"))
});

app.Run();

