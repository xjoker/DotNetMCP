using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 分析 API 控制器 - 提供反编译、搜索、交叉引用、调用图等功能
/// </summary>
[ApiController]
[Route("analysis")]
public class AnalysisController : ControllerBase
{
    private readonly AnalysisService _analysisService;
    private readonly IInstanceRegistry _registry;

    public AnalysisController(AnalysisService analysisService, IInstanceRegistry registry)
    {
        _analysisService = analysisService;
        _registry = registry;
    }

    private AssemblyContext? GetContext(string? mvid = null) => _registry.Get(mvid);

    #region 反编译

    /// <summary>
    /// 获取类型源码
    /// </summary>
    [HttpGet("type/{typeName}/source")]
    public IActionResult GetTypeSource(string typeName, [FromQuery] string language = "csharp", [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.DecompileType(context, Uri.UnescapeDataString(typeName), language);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "DECOMPILE_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                type_name = result.Target,
                language = language,
                code = result.Code
            }
        });
    }

    /// <summary>
    /// 获取方法源码
    /// </summary>
    [HttpGet("type/{typeName}/method/{methodName}")]
    public IActionResult GetMethodSource(string typeName, string methodName, [FromQuery] string language = "csharp", [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.DecompileMethod(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName), language);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "DECOMPILE_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                type_name = Uri.UnescapeDataString(typeName),
                method_name = Uri.UnescapeDataString(methodName),
                language = language,
                code = result.Code
            }
        });
    }

    #endregion

    #region 类型信息

    /// <summary>
    /// 获取类型详细信息
    /// </summary>
    [HttpGet("type/{typeName}/info")]
    public IActionResult GetTypeInfo(string typeName, [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.GetTypeInfo(context, Uri.UnescapeDataString(typeName));
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "TYPE_NOT_FOUND", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                full_name = result.FullName,
                @namespace = result.Namespace,
                name = result.Name,
                base_type = result.BaseType,
                is_public = result.IsPublic,
                is_abstract = result.IsAbstract,
                is_sealed = result.IsSealed,
                is_interface = result.IsInterface,
                is_enum = result.IsEnum,
                is_value_type = result.IsValueType,
                interfaces = result.Interfaces,
                methods = result.Methods,
                fields = result.Fields,
                properties = result.Properties
            }
        });
    }

    #endregion

    #region 搜索

    /// <summary>
    /// 搜索类型
    /// </summary>
    [HttpGet("search/types")]
    public IActionResult SearchTypes([FromQuery] string keyword, [FromQuery] string? @namespace = null, [FromQuery] int limit = 50, [FromQuery] string? mvid = null)
    {
        // 验证 limit 参数
        if (limit < 0)
        {
            return BadRequest(new { success = false, error_code = "INVALID_LIMIT", message = "Limit must be >= 0" });
        }
        limit = Math.Min(limit, 500); // 最大限制

        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.SearchTypes(context, keyword, @namespace, limit);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "SEARCH_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                types = result.Types,
                total_count = result.TotalCount
            }
        });
    }

    /// <summary>
    /// 搜索字符串字面量
    /// </summary>
    [HttpGet("search/strings")]
    public IActionResult SearchStrings([FromQuery] string query, [FromQuery] string mode = "contains", [FromQuery] int limit = 50, [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.SearchStrings(context, query, mode, limit);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "SEARCH_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                matches = result.Matches,
                total_count = result.TotalCount
            }
        });
    }

    #endregion

    #region 交叉引用

    /// <summary>
    /// 查找类型引用
    /// </summary>
    [HttpGet("xrefs/type/{typeName}")]
    public IActionResult GetXRefsToType(string typeName, [FromQuery] int limit = 50, [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.FindReferencesToType(context, Uri.UnescapeDataString(typeName), limit);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "XREF_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                target = Uri.UnescapeDataString(typeName),
                references = result.References.Select(r => new
                {
                    source_type = r.SourceTypeName,
                    source_member = r.SourceMemberName,
                    kind = r.Kind.ToString(),
                    il_offset = r.ILOffset
                }),
                total_count = result.TotalCount
            }
        });
    }

    /// <summary>
    /// 查找方法调用
    /// </summary>
    [HttpGet("xrefs/method/{typeName}/{methodName}")]
    public IActionResult GetXRefsToMethod(string typeName, string methodName, [FromQuery] int limit = 50, [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.FindCallsToMethod(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName), limit);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "XREF_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                target = $"{Uri.UnescapeDataString(typeName)}.{Uri.UnescapeDataString(methodName)}",
                references = result.References.Select(r => new
                {
                    source_type = r.SourceTypeName,
                    source_member = r.SourceMemberName,
                    kind = r.Kind.ToString(),
                    il_offset = r.ILOffset
                }),
                total_count = result.TotalCount
            }
        });
    }

    #endregion

    #region 调用图

    /// <summary>
    /// 构建调用图
    /// </summary>
    [HttpGet("callgraph/{typeName}/{methodName}")]
    public IActionResult BuildCallGraph(string typeName, string methodName, [FromQuery] string direction = "callees", [FromQuery] int max_depth = 3, [FromQuery] int max_nodes = 100, [FromQuery] string? mvid = null)
    {
        // 验证 direction 参数
        var validDirections = new[] { "callees", "callers", "both" };
        if (!validDirections.Contains(direction.ToLowerInvariant()))
        {
            return BadRequest(new { success = false, error_code = "INVALID_DIRECTION", message = $"Direction must be one of: {string.Join(", ", validDirections)}" });
        }

        // 验证数值参数
        if (max_depth < 0 || max_nodes < 0)
        {
            return BadRequest(new { success = false, error_code = "INVALID_PARAMETER", message = "max_depth and max_nodes must be >= 0" });
        }

        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.BuildCallGraph(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName), direction, max_depth, max_nodes);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "CALLGRAPH_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                start_method = result.StartMethod,
                direction = direction,
                max_depth_reached = result.MaxDepthReached,
                levels = result.Levels
            }
        });
    }

    #endregion

    #region 批量操作

    /// <summary>
    /// 批量获取类型源码
    /// </summary>
    [HttpPost("batch/sources")]
    public IActionResult BatchGetTypeSources([FromBody] BatchSourcesRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (request.TypeNames == null || request.TypeNames.Count > 20)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Maximum 20 types per batch request" });
        }

        var results = new Dictionary<string, object>();
        foreach (var typeName in request.TypeNames)
        {
            var result = _analysisService.DecompileType(context, typeName, request.Language ?? "csharp");
            results[typeName] = new
            {
                success = result.IsSuccess,
                code = result.Code,
                error = result.ErrorMessage
            };
        }

        return Ok(new { success = true, data = results });
    }

    /// <summary>
    /// 批量获取方法源码
    /// </summary>
    [HttpPost("batch/methods")]
    public IActionResult BatchGetMethods([FromBody] BatchMethodsRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (request.Methods == null || request.Methods.Count > 20)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Maximum 20 methods per batch request" });
        }

        var results = new Dictionary<string, object>();
        foreach (var method in request.Methods)
        {
            var key = $"{method.TypeName}.{method.MethodName}";
            var result = _analysisService.DecompileMethod(context, method.TypeName, method.MethodName, request.Language ?? "csharp");
            results[key] = new
            {
                success = result.IsSuccess,
                code = result.Code,
                error = result.ErrorMessage
            };
        }

        return Ok(new { success = true, data = results });
    }

    /// <summary>
    /// 批量获取交叉引用
    /// </summary>
    [HttpPost("batch/xrefs")]
    public IActionResult BatchGetXRefs([FromBody] BatchXRefsRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (request.TypeNames == null || request.TypeNames.Count > 10)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Maximum 10 types per batch xref request" });
        }

        var results = new Dictionary<string, object>();
        foreach (var typeName in request.TypeNames)
        {
            var result = _analysisService.FindReferencesToType(context, typeName, request.Limit ?? 20);
            results[typeName] = new
            {
                success = result.IsSuccess,
                references = result.References.Select(r => new { r.SourceTypeName, r.Kind }),
                error = result.ErrorMessage
            };
        }

        return Ok(new { success = true, data = results });
    }

    #endregion
}

#region Request Models

public class BatchSourcesRequest
{
    public List<string>? TypeNames { get; set; }
    public string? Language { get; set; }
    public string? Mvid { get; set; }
}

public class BatchMethodsRequest
{
    public List<MethodIdentifier>? Methods { get; set; }
    public string? Language { get; set; }
    public string? Mvid { get; set; }
}

public class MethodIdentifier
{
    public required string TypeName { get; set; }
    public required string MethodName { get; set; }
}

public class BatchXRefsRequest
{
    public List<string>? TypeNames { get; set; }
    public int? Limit { get; set; }
    public string? Mvid { get; set; }
}

#endregion
