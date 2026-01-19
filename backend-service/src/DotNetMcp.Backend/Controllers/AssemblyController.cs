using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Core.Context;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 程序集控制器 - 提供程序集加载和基本信息查询
/// </summary>
[ApiController]
[Route("assembly")]
public class AssemblyController : ControllerBase
{
    private readonly ILogger<AssemblyController> _logger;
    
    // TODO: 后续改为注入服务管理多个 AssemblyContext
    private static readonly Dictionary<string, AssemblyContext> _contexts = new();
    private static readonly object _lock = new();

    public AssemblyController(ILogger<AssemblyController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 加载程序集
    /// </summary>
    [HttpPost("load")]
    public async Task<IActionResult> LoadAssembly([FromBody] LoadAssemblyRequest request)
    {
        try
        {
            var context = new AssemblyContext(request.Path, request.SearchPaths);
            var result = await context.LoadAsync();

            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to load assembly: {Message}", result.ErrorMessage);
                return BadRequest(new
                {
                    success = false,
                    error_code = result.ErrorCode,
                    message = result.ErrorMessage
                });
            }

            lock (_lock)
            {
                var key = context.Mvid.ToString();
                _contexts[key] = context;
                
                // 注册到其他控制器以便共享
                ModificationController.RegisterContext(key, context);
                AnalysisController.RegisterContext(key, context);
            }

            return Ok(new
            {
                success = true,
                mvid = context.Mvid.ToString(),
                name = context.Name,
                version = context.Version.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while loading assembly");
            return StatusCode(500, new
            {
                success = false,
                error_code = "INTERNAL_ERROR",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// 获取程序集信息
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetInfo([FromQuery] string? mvid = null)
    {
        try
        {
            AssemblyContext? context;

            lock (_lock)
            {
                if (mvid != null)
                {
                    if (!_contexts.TryGetValue(mvid, out context))
                    {
                        return NotFound(new
                        {
                            success = false,
                            error_code = "ASSEMBLY_NOT_FOUND",
                            message = $"Assembly with MVID {mvid} not found"
                        });
                    }
                }
                else
                {
                    // 返回第一个加载的程序集（临时逻辑）
                    context = _contexts.Values.FirstOrDefault();
                    if (context == null)
                    {
                        return NotFound(new
                        {
                            success = false,
                            error_code = "NO_ASSEMBLY_LOADED",
                            message = "No assembly loaded"
                        });
                    }
                }
            }

            var info = context.GetInfo();
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting assembly info");
            return StatusCode(500, new
            {
                success = false,
                error_code = "INTERNAL_ERROR",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// 健康检查
    /// </summary>
    [HttpGet("/health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            success = true,
            service = "DotNet MCP Backend",
            version = "0.1.0",
            loaded_assemblies = _contexts.Count
        });
    }
}

/// <summary>
/// 加载程序集请求
/// </summary>
public record LoadAssemblyRequest
{
    public required string Path { get; init; }
    public List<string>? SearchPaths { get; init; }
}
