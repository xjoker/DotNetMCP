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
    private readonly IInstanceRegistry _registry;

    public ModificationController(
        ILogger<ModificationController> logger,
        ModificationService modificationService,
        IInstanceRegistry registry)
    {
        _logger = logger;
        _modificationService = modificationService;
        _registry = registry;
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

    private AssemblyContext? GetContext(string? mvid) => _registry.Get(mvid);
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
