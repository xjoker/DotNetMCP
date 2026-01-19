using DotNetMcp.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// 添加控制器
builder.Services.AddControllers();

// 注册服务
builder.Services.AddSingleton<ModificationService>();
builder.Services.AddSingleton<AnalysisService>();

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

// 映射控制器路由
app.MapControllers();

// 健康检查端点
app.MapGet("/", () => new
{
    service = "DotNet MCP Backend",
    version = "0.2.0",
    status = "running"
});

app.Run();
