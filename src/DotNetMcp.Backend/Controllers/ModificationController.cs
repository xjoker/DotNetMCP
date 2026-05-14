using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Modification;

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
    private readonly IAssemblyManager _assemblyManager;

    public ModificationController(
        ILogger<ModificationController> logger,
        ModificationService modificationService,
        IAssemblyManager assemblyManager)
    {
        _logger = logger;
        _modificationService = modificationService;
        _assemblyManager = assemblyManager;
    }

    /// <summary>
    /// 注入方法入口代码
    /// </summary>
    [HttpPost("inject/entry")]
    public async Task<IActionResult> InjectAtEntry([FromBody] InjectRequest request)
    {
        var context = _assemblyManager.Get(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND", message = "Assembly not found. Load an assembly first using the assembly/load endpoint." });
        }

        await context.OperationLock.WaitAsync();
        try
        {
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
        finally
        {
            context.OperationLock.Release();
        }
    }

    /// <summary>
    /// 替换方法体
    /// </summary>
    [HttpPost("replace/body")]
    public async Task<IActionResult> ReplaceMethodBody([FromBody] InjectRequest request)
    {
        var context = _assemblyManager.Get(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND", message = "Assembly not found. Load an assembly first using the assembly/load endpoint." });
        }

        await context.OperationLock.WaitAsync();
        try
        {
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
        finally
        {
            context.OperationLock.Release();
        }
    }

    /// <summary>
    /// 添加新类型
    /// </summary>
    [HttpPost("type/add")]
    public async Task<IActionResult> AddType([FromBody] AddTypeRequest request)
    {
        var context = _assemblyManager.Get(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND", message = "Assembly not found. Load an assembly first using the assembly/load endpoint." });
        }

        await context.OperationLock.WaitAsync();
        try
        {
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
        finally
        {
            context.OperationLock.Release();
        }
    }

    /// <summary>
    /// 添加方法
    /// </summary>
    [HttpPost("method/add")]
    public async Task<IActionResult> AddMethod([FromBody] AddMethodRequest request)
    {
        var context = _assemblyManager.Get(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND", message = "Assembly not found. Load an assembly first using the assembly/load endpoint." });
        }

        await context.OperationLock.WaitAsync();
        try
        {
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
        finally
        {
            context.OperationLock.Release();
        }
    }

    /// <summary>
    /// 用 C# 源码替换方法体（Roslyn 编译 + Cecil 注入）
    /// </summary>
    [HttpPost("csharp")]
    public async Task<IActionResult> ReplaceMethodBodyWithCSharp([FromBody] CSharpPatchRequest request)
    {
        var context = _assemblyManager.Get(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND", message = "Assembly not found. Load an assembly first using the assembly/load endpoint." });
        }

        await context.OperationLock.WaitAsync();
        try
        {
            var result = _modificationService.ReplaceMethodBodyWithCSharp(
                context,
                request.MethodFullName,
                request.CsharpBody,
                request.Usings,
                request.AllowUnsafe);

            if (!result.IsSuccess)
            {
                return BadRequest(new
                {
                    success = false,
                    error_code = "ROSLYN_PATCH_FAILED",
                    message = result.ErrorMessage,
                    diagnostics = result.Diagnostics
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    method = request.MethodFullName,
                    instructions_replaced = result.InstructionsReplaced
                }
            });
        }
        finally
        {
            context.OperationLock.Release();
        }
    }

    /// <summary>
    /// 保存程序集
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> SaveAssembly([FromBody] SaveRequest request)
    {
        var context = _assemblyManager.Get(request.Mvid);
        if (context == null)
        {
            return NotFound(new { success = false, error_code = "ASSEMBLY_NOT_FOUND", message = "Assembly not found. Load an assembly first using the assembly/load endpoint." });
        }

        await context.OperationLock.WaitAsync();
        try
        {
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
        finally
        {
            context.OperationLock.Release();
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

public record CSharpPatchRequest
{
    public string? Mvid { get; init; }
    public required string MethodFullName { get; init; }
    public required string CsharpBody { get; init; }
    public string[]? Usings { get; init; }
    public bool AllowUnsafe { get; init; }
}

#endregion
