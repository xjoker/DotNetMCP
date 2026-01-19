using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Core.Context;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 实例管理 API 控制器 - 管理程序集实例
/// </summary>
[ApiController]
[Route("instance")]
public class InstanceController : ControllerBase
{
    private static readonly Dictionary<string, AssemblyContext> _contexts = new();
    private static readonly object _lock = new();
    private static string? _defaultInstance = null;

    /// <summary>
    /// 注册程序集上下文
    /// </summary>
    public static void RegisterContext(string key, AssemblyContext context)
    {
        lock (_lock)
        {
            _contexts[key] = context;
            // 如果没有默认实例，设置第一个为默认
            if (_defaultInstance == null)
            {
                _defaultInstance = key;
            }
        }
    }

    /// <summary>
    /// 获取所有实例
    /// </summary>
    public static IEnumerable<AssemblyContext> GetAllContexts()
    {
        lock (_lock)
        {
            return _contexts.Values.ToList();
        }
    }

    #region REST API

    /// <summary>
    /// 列出所有实例
    /// </summary>
    [HttpGet("list")]
    public IActionResult ListInstances()
    {
        lock (_lock)
        {
            var instances = _contexts.Select(kv => new
            {
                mvid = kv.Key,
                name = kv.Value.Name,
                version = kv.Value.Version?.ToString(),
                path = kv.Value.AssemblyPath,
                is_default = kv.Key == _defaultInstance,
                types_count = kv.Value.Assembly?.MainModule.Types.Count ?? 0
            }).ToList();

            return Ok(new
            {
                success = true,
                data = new
                {
                    instances = instances,
                    count = instances.Count,
                    default_instance = _defaultInstance
                }
            });
        }
    }

    /// <summary>
    /// 获取实例详情
    /// </summary>
    [HttpGet("{mvid}")]
    public IActionResult GetInstance(string mvid)
    {
        lock (_lock)
        {
            if (!_contexts.TryGetValue(mvid, out var context))
            {
                return NotFound(new { success = false, error_code = "INSTANCE_NOT_FOUND", message = $"Instance '{mvid}' not found" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    mvid = mvid,
                    name = context.Name,
                    version = context.Version?.ToString(),
                    path = context.AssemblyPath,
                    is_default = mvid == _defaultInstance,
                    types_count = context.Assembly?.MainModule.Types.Count ?? 0,
                    target_framework = context.Assembly?.MainModule.RuntimeVersion
                }
            });
        }
    }

    /// <summary>
    /// 设置默认实例
    /// </summary>
    [HttpPut("{mvid}/default")]
    public IActionResult SetDefault(string mvid)
    {
        lock (_lock)
        {
            if (!_contexts.ContainsKey(mvid))
            {
                return NotFound(new { success = false, error_code = "INSTANCE_NOT_FOUND", message = $"Instance '{mvid}' not found" });
            }

            _defaultInstance = mvid;
            return Ok(new
            {
                success = true,
                message = $"Default instance set to '{mvid}'"
            });
        }
    }

    /// <summary>
    /// 移除实例
    /// </summary>
    [HttpDelete("{mvid}")]
    public IActionResult RemoveInstance(string mvid)
    {
        lock (_lock)
        {
            if (!_contexts.TryGetValue(mvid, out var context))
            {
                return NotFound(new { success = false, error_code = "INSTANCE_NOT_FOUND", message = $"Instance '{mvid}' not found" });
            }

            _contexts.Remove(mvid);

            // 如果删除的是默认实例，重新设置默认
            if (_defaultInstance == mvid)
            {
                _defaultInstance = _contexts.Keys.FirstOrDefault();
            }

            // 同步删除其他控制器中的引用
            ModificationController.UnregisterContext(mvid);
            AnalysisController.UnregisterContext(mvid);

            return Ok(new
            {
                success = true,
                message = $"Instance '{mvid}' removed"
            });
        }
    }

    /// <summary>
    /// 获取分析状态
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus([FromQuery] string? mvid = null)
    {
        lock (_lock)
        {
            var targetMvid = mvid ?? _defaultInstance;
            if (targetMvid == null || !_contexts.TryGetValue(targetMvid, out var context))
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        loaded_count = _contexts.Count,
                        default_instance = _defaultInstance,
                        memory = GetMemoryInfo(),
                        status = "idle"
                    }
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    mvid = targetMvid,
                    name = context.Name,
                    loaded_count = _contexts.Count,
                    types_count = context.Assembly?.MainModule.Types.Count ?? 0,
                    memory = GetMemoryInfo(),
                    status = "ready"
                }
            });
        }
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    [HttpPost("cache/clear")]
    public IActionResult ClearCache([FromQuery] string? mvid = null)
    {
        // 目前没有实际缓存需要清除，返回成功
        GC.Collect();
        
        return Ok(new
        {
            success = true,
            message = "Cache cleared",
            memory_after = GetMemoryInfo()
        });
    }

    /// <summary>
    /// 健康检查所有实例
    /// </summary>
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        lock (_lock)
        {
            var instanceHealth = _contexts.Select(kv => new
            {
                mvid = kv.Key,
                name = kv.Value.Name,
                status = kv.Value.Assembly != null ? "healthy" : "error",
                is_default = kv.Key == _defaultInstance
            }).ToList();

            return Ok(new
            {
                success = true,
                data = new
                {
                    total = instanceHealth.Count,
                    healthy = instanceHealth.Count(i => i.status == "healthy"),
                    instances = instanceHealth
                }
            });
        }
    }

    #endregion

    #region Helpers

    private static object GetMemoryInfo()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        return new
        {
            working_set_mb = process.WorkingSet64 / 1024 / 1024,
            private_memory_mb = process.PrivateMemorySize64 / 1024 / 1024,
            gc_total_memory_mb = GC.GetTotalMemory(false) / 1024 / 1024
        };
    }

    #endregion
}
