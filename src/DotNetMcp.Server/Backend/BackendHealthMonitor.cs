using Microsoft.Extensions.Options;
using DotNetMcp.Server.Configuration;

namespace DotNetMcp.Server.Backend;

/// <summary>
/// 后端健康监控服务
/// </summary>
public class BackendHealthMonitor : BackgroundService
{
    private readonly IBackendRegistry _registry;
    private readonly IOptions<McpServerOptions> _options;
    private readonly ILogger<BackendHealthMonitor> _logger;

    public BackendHealthMonitor(
        IBackendRegistry registry,
        IOptions<McpServerOptions> options,
        ILogger<BackendHealthMonitor> logger)
    {
        _registry = registry;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.Value.HealthCheckIntervalSeconds);

        _logger.LogInformation("Backend health monitor started with interval: {Interval}s", interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllBackendsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check cycle");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task CheckAllBackendsAsync(CancellationToken cancellationToken)
    {
        var backends = _registry.GetAll();
        var tasks = backends.Select(b => CheckBackendAsync(b, cancellationToken));
        await Task.WhenAll(tasks);

        // 如果默认后端不健康，尝试故障转移
        var defaultId = _registry.DefaultBackendId;
        if (defaultId != null)
        {
            var defaultBackend = _registry.Get(defaultId);
            if (defaultBackend != null && !defaultBackend.IsHealthy)
            {
                var healthyBackend = backends.FirstOrDefault(b => b.IsHealthy && b.Id != defaultId);
                if (healthyBackend != null)
                {
                    _logger.LogWarning("Default backend {DefaultId} is unhealthy, failing over to {NewId}",
                        defaultId, healthyBackend.Id);
                    _registry.SetDefault(healthyBackend.Id);
                }
            }
        }
    }

    private async Task CheckBackendAsync(IBackend backend, CancellationToken cancellationToken)
    {
        try
        {
            var healthy = await backend.CheckHealthAsync(cancellationToken);
            if (!healthy)
            {
                _logger.LogWarning("Backend {Id} ({Name}) is unhealthy", backend.Id, backend.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for backend {Id}", backend.Id);
        }
    }
}
