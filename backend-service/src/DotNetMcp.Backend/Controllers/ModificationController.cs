using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 修改控制器 - 提供程序集修改 REST API
/// </summary>
[ApiController]
[Route("modification")]
public class ModificationController : ControllerBase
{
    private readonly ILogger<ModificationController> _logger;
    private readonly ModificationService _modificationService;

    // 共享程序集上下文（临时方案，后续改为注入）
    private static readonly Dictionary<string, AssemblyContext> _contexts = new();
    private static readonly object _lock = new();

    public ModificationController(
        ILogger<ModificationController> logger,
        ModificationService modificationService)
    {
        _logger = logger;
        _modificationService = modificationService;
    }

    /// <summary>
    /// 注入方法入口代码
    /// </summary>
    [HttpPost("inject/entry")]
    public IActionResult InjectAtEntry([FromBody] InjectRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND" });
        }

        var injection = new InjectionRequest
        {
            Instructions = request.Instructions.Select(i => new InstructionInfo
            {
                OpCode = i.OpCode,
                IntValue = i.IntValue,
                StringValue = i.StringValue
            }).ToList()
        };

        var result = _modificationService.InjectAtEntry(context, request.MethodFullName, injection);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                success = false,
                error_code = result.ErrorCode,
                message = result.ErrorMessage
            });
        }

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// 替换方法体
    /// </summary>
    [HttpPost("replace/body")]
    public IActionResult ReplaceMethodBody([FromBody] InjectRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND" });
        }

        var injection = new InjectionRequest
        {
            Instructions = request.Instructions.Select(i => new InstructionInfo
            {
                OpCode = i.OpCode,
                IntValue = i.IntValue,
                StringValue = i.StringValue
            }).ToList()
        };

        var result = _modificationService.ReplaceMethodBody(context, request.MethodFullName, injection);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                success = false,
                error_code = result.ErrorCode,
                message = result.ErrorMessage
            });
        }

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// 添加新类型
    /// </summary>
    [HttpPost("type/add")]
    public IActionResult AddType([FromBody] AddTypeRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND" });
        }

        var typeRequest = new TypeCreationRequest
        {
            Namespace = request.Namespace,
            Name = request.Name,
            Kind = request.Kind
        };

        var result = _modificationService.AddType(context, typeRequest);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                success = false,
                error_code = result.ErrorCode,
                message = result.ErrorMessage
            });
        }

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// 添加方法
    /// </summary>
    [HttpPost("method/add")]
    public IActionResult AddMethod([FromBody] AddMethodRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND" });
        }

        var methodRequest = new MethodCreationRequest
        {
            TypeFullName = request.TypeFullName,
            Name = request.Name,
            ReturnType = request.ReturnType,
            Parameters = request.Parameters?.Select(p => new ParameterInfo
            {
                Name = p.Name,
                Type = p.Type
            }).ToList()
        };

        var result = _modificationService.AddMethod(context, methodRequest);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                success = false,
                error_code = result.ErrorCode,
                message = result.ErrorMessage
            });
        }

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// 保存程序集
    /// </summary>
    [HttpPost("save")]
    public IActionResult SaveAssembly([FromBody] SaveRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND" });
        }

        var result = _modificationService.SaveAssembly(context, request.OutputPath);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                success = false,
                error_code = result.ErrorCode,
                message = result.ErrorMessage
            });
        }

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// 注册程序集上下文（供 AssemblyController 共享使用）
    /// </summary>
    public static void RegisterContext(string mvid, AssemblyContext context)
    {
        lock (_lock)
        {
            _contexts[mvid] = context;
        }
    }

    private AssemblyContext? GetContext(string? mvid)
    {
        if (string.IsNullOrEmpty(mvid))
        {
            lock (_lock)
            {
                return _contexts.Values.FirstOrDefault();
            }
        }

        lock (_lock)
        {
            return _contexts.TryGetValue(mvid, out var context) ? context : null;
        }
    }
}

#region 请求模型

public record InjectRequest
{
    public string? Mvid { get; init; }
    public required string MethodFullName { get; init; }
    public required List<InstructionRequestInfo> Instructions { get; init; }
}

public record InstructionRequestInfo
{
    public required string OpCode { get; init; }
    public int? IntValue { get; init; }
    public string? StringValue { get; init; }
}

public record AddTypeRequest
{
    public string? Mvid { get; init; }
    public required string Namespace { get; init; }
    public required string Name { get; init; }
    public string Kind { get; init; } = "class";
}

public record AddMethodRequest
{
    public string? Mvid { get; init; }
    public required string TypeFullName { get; init; }
    public required string Name { get; init; }
    public string ReturnType { get; init; } = "void";
    public List<ParameterRequestInfo>? Parameters { get; init; }
}

public record ParameterRequestInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
}

public record SaveRequest
{
    public string? Mvid { get; init; }
    public required string OutputPath { get; init; }
}

#endregion
