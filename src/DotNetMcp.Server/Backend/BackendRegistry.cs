using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server.Backend;

/// <summary>
/// 后端注册表实现
/// </summary>
public class BackendRegistry : IBackendRegistry
{
    private readonly ConcurrentDictionary<string, IBackend> _backends = new();
    private readonly object _defaultLock = new();
    private string? _defaultBackendId;
    private readonly ILogger<BackendRegistry> _logger;

    public BackendRegistry(ILogger<BackendRegistry> logger)
    {
        _logger = logger;
    }

    public void Register(IBackend backend)
    {
        if (_backends.TryAdd(backend.Id, backend))
        {
            _logger.LogInformation("Registered backend: {Id} ({Name}, {Type})", backend.Id, backend.Name, backend.Type);

            // 第一个注册的后端自动设为默认
            lock (_defaultLock)
            {
                if (_defaultBackendId == null)
                {
                    _defaultBackendId = backend.Id;
                    _logger.LogInformation("Set default backend to: {Id}", backend.Id);
                }
            }
        }
        else
        {
            _logger.LogWarning("Backend already registered: {Id}", backend.Id);
        }
    }

    public bool Unregister(string backendId)
    {
        if (!_backends.TryRemove(backendId, out _))
        {
            return false;
        }

        lock (_defaultLock)
        {
            if (_defaultBackendId == backendId)
            {
                _defaultBackendId = _backends.Keys.FirstOrDefault();
                _logger.LogInformation("Default backend changed to: {Id}", _defaultBackendId ?? "(none)");
            }
        }

        _logger.LogInformation("Unregistered backend: {Id}", backendId);
        return true;
    }

    public IBackend? Get(string? backendId = null)
    {
        if (backendId != null)
        {
            return _backends.TryGetValue(backendId, out var backend) ? backend : null;
        }

        lock (_defaultLock)
        {
            if (_defaultBackendId != null && _backends.TryGetValue(_defaultBackendId, out var defaultBackend))
            {
                return defaultBackend;
            }
        }

        return _backends.Values.FirstOrDefault();
    }

    public IReadOnlyList<IBackend> GetAll()
    {
        return _backends.Values.ToList().AsReadOnly();
    }

    public bool SetDefault(string backendId)
    {
        if (!_backends.ContainsKey(backendId))
        {
            _logger.LogWarning("Cannot set default to non-existent backend: {Id}", backendId);
            return false;
        }

        lock (_defaultLock)
        {
            _defaultBackendId = backendId;
        }

        _logger.LogInformation("Default backend set to: {Id}", backendId);
        return true;
    }

    public string? DefaultBackendId
    {
        get
        {
            lock (_defaultLock)
            {
                return _defaultBackendId;
            }
        }
    }

    public int Count => _backends.Count;
}
