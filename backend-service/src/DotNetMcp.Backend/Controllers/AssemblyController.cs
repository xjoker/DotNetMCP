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
    private readonly IAssemblyManager _assemblyManager;

    public AssemblyController(ILogger<AssemblyController> logger, IAssemblyManager assemblyManager)
    {
        _logger = logger;
        _assemblyManager = assemblyManager;
    }

    /// <summary>
    /// 加载程序集
    /// </summary>
    [HttpPost("load")]
    public async Task<IActionResult> LoadAssembly([FromBody] LoadAssemblyRequest request)
    {
        try
        {
            var result = await _assemblyManager.LoadAsync(request.Path, request.SearchPaths);

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

            var context = result.Context!;
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
            var context = _assemblyManager.Get(mvid);

            if (context == null)
            {
                if (mvid != null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error_code = "ASSEMBLY_NOT_FOUND",
                        message = $"Assembly with MVID {mvid} not found"
                    });
                }
                return NotFound(new
                {
                    success = false,
                    error_code = "NO_ASSEMBLY_LOADED",
                    message = "No assembly loaded"
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
            version = "0.1.0",
            loaded_assemblies = _assemblyManager.Count
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
