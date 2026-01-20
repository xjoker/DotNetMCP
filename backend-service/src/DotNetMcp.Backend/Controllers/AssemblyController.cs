using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 程序集控制器 - 提供程序集加载和基本信息查询
/// </summary>
[ApiController]
[Route("assembly")]
public class AssemblyController : ControllerBase
{
    private readonly ILogger<AssemblyController> _logger;
    private readonly IInstanceRegistry _registry;

    public AssemblyController(ILogger<AssemblyController> logger, IInstanceRegistry registry)
    {
        _logger = logger;
        _registry = registry;
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

            var key = context.Mvid.ToString();
            _registry.Register(key, context);

            return Ok(new
            {
                success = true,
                mvid = key,
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
            var context = _registry.Get(mvid);
            if (context == null)
            {
                return NotFound(new
                {
                    success = false,
                    error_code = mvid != null ? "ASSEMBLY_NOT_FOUND" : "NO_ASSEMBLY_LOADED",
                    message = mvid != null ? $"Assembly with MVID {mvid} not found" : "No assembly loaded"
                });
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
            version = "0.3.0",
            loaded_assemblies = _registry.Count
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
