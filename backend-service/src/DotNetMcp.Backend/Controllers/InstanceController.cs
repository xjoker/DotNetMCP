using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 实例管理 API 控制器 - 管理程序集实例
/// </summary>
[ApiController]
[Route("instance")]
public class InstanceController : ControllerBase
{
    private readonly IAssemblyManager _assemblyManager;

    public InstanceController(IAssemblyManager assemblyManager)
    {
        _assemblyManager = assemblyManager;
    }

    /// <summary>
    /// 列出所有实例
    /// </summary>
    [HttpGet("list")]
    public IActionResult ListInstances()
    {
        var contexts = _assemblyManager.GetAll();
        var defaultMvid = _assemblyManager.DefaultMvid;

        var instances = contexts.Select(ctx => new
        {
            mvid = ctx.Mvid.ToString(),
            name = ctx.Name,
            version = ctx.Version?.ToString(),
            path = ctx.AssemblyPath,
            is_default = ctx.Mvid.ToString() == defaultMvid,
            types_count = ctx.Assembly?.MainModule.Types.Count ?? 0
        }).ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                instances = instances,
                count = instances.Count,
                default_instance = defaultMvid
            }
        });
    }

    /// <summary>
    /// 获取实例详情
    /// </summary>
    [HttpGet("{mvid}")]
    public IActionResult GetInstance(string mvid)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
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
                is_default = mvid == _assemblyManager.DefaultMvid,
                types_count = context.Assembly?.MainModule.Types.Count ?? 0,
                target_framework = context.Assembly?.MainModule.RuntimeVersion
            }
        });
    }

    /// <summary>
    /// 设置默认实例
    /// </summary>
    [HttpPut("{mvid}/default")]
    public IActionResult SetDefault(string mvid)
    {
        if (!_assemblyManager.SetDefault(mvid))
        {
            return NotFound(new { success = false, error_code = "INSTANCE_NOT_FOUND", message = $"Instance '{mvid}' not found" });
        }

        return Ok(new
        {
            success = true,
            message = $"Default instance set to '{mvid}'"
        });
    }

    /// <summary>
    /// 获取默认实例
    /// </summary>
    [HttpGet("default")]
    public IActionResult GetDefault()
    {
        var defaultMvid = _assemblyManager.DefaultMvid;
        if (defaultMvid == null)
        {
            return Ok(new
            {
                success = true,
                data = new
                {
                    mvid = (string?)null,
                    message = "No default instance set"
                }
            });
        }

        var context = _assemblyManager.Get(defaultMvid);
        return Ok(new
        {
            success = true,
            data = new
            {
                mvid = defaultMvid,
                name = context?.Name,
                version = context?.Version?.ToString()
            }
        });
    }

    /// <summary>
    /// 移除实例
    /// </summary>
    [HttpDelete("{mvid}")]
    public IActionResult RemoveInstance(string mvid)
    {
        if (!_assemblyManager.Unload(mvid))
        {
            return NotFound(new { success = false, error_code = "INSTANCE_NOT_FOUND", message = $"Instance '{mvid}' not found" });
        }

        return Ok(new
        {
            success = true,
            message = $"Instance '{mvid}' removed"
        });
    }

    /// <summary>
    /// 获取分析状态
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus([FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);

        if (context == null)
        {
            return Ok(new
            {
                success = true,
                data = new
                {
                    loaded_count = _assemblyManager.Count,
                    default_instance = _assemblyManager.DefaultMvid,
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
                mvid = context.Mvid.ToString(),
                name = context.Name,
                loaded_count = _assemblyManager.Count,
                types_count = context.Assembly?.MainModule.Types.Count ?? 0,
                memory = GetMemoryInfo(),
                status = "ready"
            }
        });
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    [HttpPost("cache/clear")]
    public IActionResult ClearCache([FromQuery] string? mvid = null)
    {
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
        var contexts = _assemblyManager.GetAll();
        var defaultMvid = _assemblyManager.DefaultMvid;

        var instanceHealth = contexts.Select(ctx => new
        {
            mvid = ctx.Mvid.ToString(),
            name = ctx.Name,
            status = ctx.Assembly != null ? "healthy" : "error",
            is_default = ctx.Mvid.ToString() == defaultMvid
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
}
