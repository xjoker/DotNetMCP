using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Modification;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DotNetMcp.Backend.Services;

/// <summary>
/// 修改服务 - 协调所有程序集修改操作
/// </summary>
public class ModificationService
{
    private readonly ILogger<ModificationService> _logger;

    public ModificationService(ILogger<ModificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注入方法入口代码
    /// </summary>
    public ModificationResult InjectAtEntry(AssemblyContext context, string methodFullName, InjectionRequest request)
    {
        try
        {
            var method = FindMethod(context, methodFullName);
            if (method == null)
            {
                return ModificationResult.Failure("METHOD_NOT_FOUND", $"Method '{methodFullName}' not found");
            }

            var injector = new CodeInjector(context.Assembly!.MainModule);
            var builder = new ILBuilder(method.Body.GetILProcessor());

            // 构建注入代码
            foreach (var instruction in request.Instructions)
            {
                AppendInstruction(builder, instruction, context.Assembly!.MainModule);
            }

            var instructions = builder.Build();
            var result = injector.InjectAtMethodEntry(method, instructions);

            if (!result.IsSuccess)
            {
                return ModificationResult.Failure("INJECTION_FAILED", result.ErrorMessage ?? "Injection failed");
            }

            _logger.LogInformation("Injected {Count} instructions at entry of {Method}", 
                instructions.Count, methodFullName);

            return ModificationResult.Success(new
            {
                method = methodFullName,
                injected_count = instructions.Count,
                location = "entry"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject at method entry");
            return ModificationResult.Failure("INJECTION_ERROR", ex.Message);
        }
    }

    /// <summary>
    /// 替换方法体
    /// </summary>
    public ModificationResult ReplaceMethodBody(AssemblyContext context, string methodFullName, InjectionRequest request)
    {
        try
        {
            var method = FindMethod(context, methodFullName);
            if (method == null)
            {
                return ModificationResult.Failure("METHOD_NOT_FOUND", $"Method '{methodFullName}' not found");
            }

            var injector = new CodeInjector(context.Assembly!.MainModule);
            var builder = new ILBuilder(method.Body.GetILProcessor());

            foreach (var instruction in request.Instructions)
            {
                AppendInstruction(builder, instruction, context.Assembly!.MainModule);
            }

            var instructions = builder.Build();
            var result = injector.ReplaceMethodBody(method, instructions);

            if (!result.IsSuccess)
            {
                return ModificationResult.Failure("REPLACE_FAILED", result.ErrorMessage ?? "Replace failed");
            }

            _logger.LogInformation("Replaced method body of {Method} with {Count} instructions", 
                methodFullName, instructions.Count);

            return ModificationResult.Success(new
            {
                method = methodFullName,
                instruction_count = instructions.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replace method body");
            return ModificationResult.Failure("REPLACE_ERROR", ex.Message);
        }
    }

    /// <summary>
    /// 添加新类型
    /// </summary>
    public ModificationResult AddType(AssemblyContext context, TypeCreationRequest request)
    {
        try
        {
            var rewriter = new AssemblyRewriter(context.Assembly!);
            var factory = new TypeFactory(context.Assembly!.MainModule);

            TypeDefinition type = request.Kind switch
            {
                "class" => factory.CreateClass(request.Namespace, request.Name),
                "interface" => factory.CreateInterface(request.Namespace, request.Name),
                "struct" => factory.CreateStruct(request.Namespace, request.Name),
                _ => throw new ArgumentException($"Unknown type kind: {request.Kind}")
            };

            var result = rewriter.AddType(type);
            if (!result.IsSuccess)
            {
                return ModificationResult.Failure("ADD_TYPE_FAILED", result.ErrorMessage ?? "Failed to add type");
            }

            _logger.LogInformation("Added {Kind} {Namespace}.{Name}", 
                request.Kind, request.Namespace, request.Name);

            return ModificationResult.Success(new
            {
                full_name = $"{request.Namespace}.{request.Name}",
                kind = request.Kind
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add type");
            return ModificationResult.Failure("ADD_TYPE_ERROR", ex.Message);
        }
    }

    /// <summary>
    /// 添加方法到类型
    /// </summary>
    public ModificationResult AddMethod(AssemblyContext context, MethodCreationRequest request)
    {
        try
        {
            // 验证方法名
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return ModificationResult.Failure("INVALID_METHOD_NAME", "Method name cannot be empty");
            }

            var type = FindType(context, request.TypeFullName);
            if (type == null)
            {
                return ModificationResult.Failure("TYPE_NOT_FOUND", $"Type '{request.TypeFullName}' not found");
            }

            var rewriter = new AssemblyRewriter(context.Assembly!);
            var factory = new TypeFactory(context.Assembly!.MainModule);

            var returnType = ResolveType(context.Assembly!.MainModule, request.ReturnType);
            var parameters = request.Parameters?
                .Select(p => (p.Name, ResolveType(context.Assembly!.MainModule, p.Type)))
                .ToArray() ?? Array.Empty<(string, TypeReference)>();

            var method = factory.CreateMethod(request.Name, returnType, parameters: parameters);
            
            // 添加默认方法体
            method.Body = new MethodBody(method);
            var il = method.Body.GetILProcessor();
            if (returnType.FullName == "System.Void")
            {
                il.Append(il.Create(OpCodes.Ret));
            }
            else
            {
                il.Append(il.Create(OpCodes.Ldnull));
                il.Append(il.Create(OpCodes.Ret));
            }

            var result = rewriter.AddMethod(type, method);
            if (!result.IsSuccess)
            {
                return ModificationResult.Failure("ADD_METHOD_FAILED", result.ErrorMessage ?? "Failed to add method");
            }

            _logger.LogInformation("Added method {Name} to {Type}", request.Name, request.TypeFullName);

            return ModificationResult.Success(new
            {
                method_name = request.Name,
                type = request.TypeFullName,
                return_type = request.ReturnType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add method");
            return ModificationResult.Failure("ADD_METHOD_ERROR", ex.Message);
        }
    }

    /// <summary>
    /// 保存程序集到文件
    /// </summary>
    public ModificationResult SaveAssembly(AssemblyContext context, string outputPath)
    {
        try
        {
            var rewriter = new AssemblyRewriter(context.Assembly!);
            var result = rewriter.Save(outputPath);

            if (!result.IsSuccess)
            {
                return ModificationResult.Failure("SAVE_FAILED", result.ErrorMessage ?? "Failed to save assembly");
            }

            _logger.LogInformation("Saved assembly to {Path}", outputPath);

            return ModificationResult.Success(new
            {
                path = outputPath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save assembly");
            return ModificationResult.Failure("SAVE_ERROR", ex.Message);
        }
    }

    /// <summary>
    /// 对比两个程序集
    /// </summary>
    public ModificationResult CompareAssemblies(AssemblyContext original, AssemblyContext modified)
    {
        try
        {
            var comparator = new DiffComparator();
            var diff = comparator.CompareAssemblies(original.Assembly!, modified.Assembly!);

            return ModificationResult.Success(new
            {
                original_name = diff.OriginalName,
                modified_name = diff.ModifiedName,
                total_changes = diff.TotalChanges,
                type_diffs = diff.TypeDiffs.Select(t => new
                {
                    type_name = t.TypeName,
                    diff_type = t.DiffType.ToString(),
                    member_diffs = t.MemberDiffs.Select(m => new
                    {
                        member_name = m.MemberName,
                        member_type = m.MemberType,
                        diff_type = m.DiffType.ToString()
                    })
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare assemblies");
            return ModificationResult.Failure("COMPARE_ERROR", ex.Message);
        }
    }

    private MethodDefinition? FindMethod(AssemblyContext context, string fullName)
    {
        // 规范化方法名格式
        // 支持格式：
        // 1. Namespace.Type.MethodName (推荐)
        // 2. Namespace.Type::MethodName (Cecil 风格)
        // 3. Cecil FullName (System.Void Namespace.Type::Method(params))
        
        var normalizedName = fullName;
        
        // 转换 :: 为 . 以统一处理
        if (fullName.Contains("::") && !fullName.Contains(" "))
        {
            normalizedName = fullName.Replace("::", ".");
        }
        
        foreach (var type in context.Assembly!.MainModule.Types)
        {
            // 匹配方法
            var method = FindMethodInType(type, normalizedName, fullName);
            if (method != null) return method;

            // 搜索嵌套类型
            foreach (var nested in type.NestedTypes)
            {
                method = FindMethodInType(nested, normalizedName, fullName);
                if (method != null) return method;
                
                // 递归搜索深层嵌套
                foreach (var deepNested in nested.NestedTypes)
                {
                    method = FindMethodInType(deepNested, normalizedName, fullName);
                    if (method != null) return method;
                }
            }
        }
        
        _logger.LogWarning("Method not found: {FullName}. Available formats: Namespace.Type.Method or Type::Method", fullName);
        return null;
    }

    private MethodDefinition? FindMethodInType(TypeDefinition type, string normalizedName, string originalName)
    {
        foreach (var method in type.Methods)
        {
            // 格式 1: Namespace.Type.MethodName
            var dotFormat = $"{type.FullName}.{method.Name}";
            if (dotFormat == normalizedName) return method;
            
            // 格式 2: Cecil FullName 匹配
            if (method.FullName == originalName) return method;
            
            // 格式 3: 仅方法名匹配（如果类型名匹配）
            if (normalizedName.EndsWith($".{method.Name}") && 
                normalizedName.StartsWith(type.FullName))
            {
                return method;
            }
        }
        return null;
    }


    private TypeDefinition? FindType(AssemblyContext context, string fullName)
    {
        foreach (var type in context.Assembly!.MainModule.Types)
        {
            if (type.FullName == fullName) return type;
            foreach (var nested in type.NestedTypes)
            {
                if (nested.FullName == fullName) return nested;
            }
        }
        return null;
    }

    private TypeReference ResolveType(ModuleDefinition module, string typeName)
    {
        return typeName switch
        {
            "void" => module.ImportReference(typeof(void)),
            "int" => module.ImportReference(typeof(int)),
            "long" => module.ImportReference(typeof(long)),
            "string" => module.ImportReference(typeof(string)),
            "bool" => module.ImportReference(typeof(bool)),
            "object" => module.ImportReference(typeof(object)),
            "float" => module.ImportReference(typeof(float)),
            "double" => module.ImportReference(typeof(double)),
            _ => module.ImportReference(typeof(object)) // 默认返回 object
        };
    }

    private void AppendInstruction(ILBuilder builder, InstructionInfo info, ModuleDefinition module)
    {
        switch (info.OpCode.ToLowerInvariant())
        {
            case "nop":
                builder.Nop();
                break;
            case "ldarg.0":
                builder.LoadArg(0);
                break;
            case "ldarg.1":
                builder.LoadArg(1);
                break;
            case "ldc.i4":
            case "ldc_i4":
                builder.LoadInt(info.IntValue ?? 0);
                break;
            case "ldstr":
                builder.LoadString(info.StringValue ?? "");
                break;
            case "add":
                builder.Add();
                break;
            case "sub":
                builder.Sub();
                break;
            case "mul":
                builder.Mul();
                break;
            case "div":
                builder.Div();
                break;
            case "ret":
                builder.Ret();
                break;
            case "pop":
                builder.Pop();
                break;
            case "dup":
                builder.Dup();
                break;
            case "call":
                if (string.IsNullOrEmpty(info.StringValue))
                {
                    throw new ArgumentException("call opcode requires StringValue with method signature");
                }
                var methodRef = ResolveMethodReference(module, info.StringValue);
                builder.Call(methodRef);
                break;
            case "callvirt":
                if (string.IsNullOrEmpty(info.StringValue))
                {
                    throw new ArgumentException("callvirt opcode requires StringValue with method signature");
                }
                var methodRefVirt = ResolveMethodReference(module, info.StringValue);
                builder.CallVirt(methodRefVirt);
                break;
            default:
                throw new ArgumentException($"Unknown opcode: {info.OpCode}");
        }
    }

    /// <summary>
    /// 解析方法引用签名
    /// 支持格式: "System.Void System.Console::WriteLine(System.String)"
    /// 或简化格式: "System.Console::WriteLine"
    /// </summary>
    private MethodReference ResolveMethodReference(ModuleDefinition module, string signature)
    {
        // 尝试解析完整签名: "ReturnType DeclaringType::MethodName(ParamTypes)"
        // 简化处理: 只解析 "DeclaringType::MethodName" 格式

        var parts = signature.Split(new[] { "::" }, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid method signature format: {signature}. Expected 'Type::Method' or 'ReturnType Type::Method(Params)'");
        }

        // 提取类型名和方法名
        var typePart = parts[0].Trim();
        var methodPart = parts[1].Trim();

        // 如果有返回类型前缀，提取实际类型名
        var typeNameParts = typePart.Split(' ');
        var typeName = typeNameParts.Length > 1 ? typeNameParts[^1] : typePart;

        // 提取方法名（去掉参数部分）
        var methodName = methodPart.Contains('(')
            ? methodPart.Substring(0, methodPart.IndexOf('('))
            : methodPart;

        // 查找类型
        var type = Type.GetType(typeName) ?? Type.GetType($"{typeName}, mscorlib") ?? Type.GetType($"{typeName}, System.Runtime");
        if (type == null)
        {
            throw new ArgumentException($"Cannot resolve type: {typeName}");
        }

        // 查找方法（简化：只按名称匹配第一个）
        var method = type.GetMethods().FirstOrDefault(m => m.Name == methodName);
        if (method == null)
        {
            throw new ArgumentException($"Cannot resolve method: {methodName} in type {typeName}");
        }

        return module.ImportReference(method);
    }

    /// <summary>
    /// 验证 opcode 是否有效
    /// </summary>
    private static readonly HashSet<string> ValidOpCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "nop", "ldarg.0", "ldarg.1", "ldc.i4", "ldc_i4", "ldstr",
        "add", "sub", "mul", "div", "ret", "pop", "dup", "call", "callvirt"
    };

    public static bool IsValidOpCode(string opcode) => ValidOpCodes.Contains(opcode);
}

/// <summary>
/// 修改结果
/// </summary>
public class ModificationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public object? Data { get; init; }

    public static ModificationResult Success(object? data = null) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ModificationResult Failure(string errorCode, string message) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = message
    };
}

/// <summary>
/// 注入请求
/// </summary>
public record InjectionRequest
{
    public required List<InstructionInfo> Instructions { get; init; }
}

/// <summary>
/// 指令信息
/// </summary>
public record InstructionInfo
{
    public required string OpCode { get; init; }
    public int? IntValue { get; init; }
    public string? StringValue { get; init; }
}

/// <summary>
/// 类型创建请求
/// </summary>
public record TypeCreationRequest
{
    public required string Namespace { get; init; }
    public required string Name { get; init; }
    public string Kind { get; init; } = "class";
}

/// <summary>
/// 方法创建请求
/// </summary>
public record MethodCreationRequest
{
    public required string TypeFullName { get; init; }
    public required string Name { get; init; }
    public string ReturnType { get; init; } = "void";
    public List<ParameterInfo>? Parameters { get; init; }
}

/// <summary>
/// 参数信息
/// </summary>
public record ParameterInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
}
