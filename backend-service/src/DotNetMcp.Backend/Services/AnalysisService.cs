using Microsoft.Extensions.Logging;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Identity;
using Mono.Cecil;

namespace DotNetMcp.Backend.Services;

/// <summary>
/// 分析服务 - 协调所有分析操作
/// </summary>
public class AnalysisService
{
    private readonly ILogger<AnalysisService> _logger;

    public AnalysisService(ILogger<AnalysisService> logger)
    {
        _logger = logger;
    }

    #region 反编译

    /// <summary>
    /// 反编译类型为 C# 源码
    /// </summary>
    public DecompileResult DecompileType(AssemblyContext context, string typeName, string language = "csharp")
    {
        try
        {
            _logger.LogInformation("Decompiling type: {TypeName}", typeName);
            
            var decompiler = new DecompilerService(context);

            if (language.ToLower() == "il")
            {
                var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == typeName);
                if (type == null)
                {
                    return DecompileResult.Failure($"Type '{typeName}' not found");
                }
                var ilCode = decompiler.GetIL(type);
                return DecompileResult.Success(ilCode, typeName);
            }

            return decompiler.DecompileType(typeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decompile type: {TypeName}", typeName);
            return DecompileResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// 反编译方法
    /// </summary>
    public DecompileResult DecompileMethod(AssemblyContext context, string typeName, string methodName, string language = "csharp")
    {
        try
        {
            _logger.LogInformation("Decompiling method: {TypeName}.{MethodName}", typeName, methodName);

            var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == typeName);
            if (type == null)
            {
                return DecompileResult.Failure($"Type '{typeName}' not found");
            }

            var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method == null)
            {
                return DecompileResult.Failure($"Method '{methodName}' not found in type '{typeName}'");
            }

            var decompiler = new DecompilerService(context);
            return decompiler.DecompileMethod(type, method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decompile method: {TypeName}.{MethodName}", typeName, methodName);
            return DecompileResult.Failure(ex.Message);
        }
    }

    #endregion

    #region 类型信息

    /// <summary>
    /// 获取类型详细信息
    /// </summary>
    public TypeInfoResult GetTypeInfo(AssemblyContext context, string typeName)
    {
        try
        {
            var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == typeName);
            if (type == null)
            {
                return TypeInfoResult.Failure($"Type '{typeName}' not found");
            }

            return new TypeInfoResult
            {
                IsSuccess = true,
                FullName = type.FullName,
                Namespace = type.Namespace,
                Name = type.Name,
                BaseType = type.BaseType?.FullName,
                IsPublic = type.IsPublic,
                IsAbstract = type.IsAbstract,
                IsSealed = type.IsSealed,
                IsInterface = type.IsInterface,
                IsEnum = type.IsEnum,
                IsValueType = type.IsValueType,
                Interfaces = type.Interfaces.Select(i => i.InterfaceType.FullName).ToList(),
                Methods = type.Methods.Select(m => new MethodSummary
                {
                    Name = m.Name,
                    ReturnType = m.ReturnType.Name,
                    ParameterCount = m.Parameters.Count,
                    IsPublic = m.IsPublic,
                    IsStatic = m.IsStatic,
                    IsVirtual = m.IsVirtual
                }).ToList(),
                Fields = type.Fields.Select(f => new FieldSummary
                {
                    Name = f.Name,
                    FieldType = f.FieldType.Name,
                    IsPublic = f.IsPublic,
                    IsStatic = f.IsStatic
                }).ToList(),
                Properties = type.Properties.Select(p => new PropertySummary
                {
                    Name = p.Name,
                    PropertyType = p.PropertyType.Name,
                    HasGetter = p.GetMethod != null,
                    HasSetter = p.SetMethod != null
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get type info: {TypeName}", typeName);
            return TypeInfoResult.Failure(ex.Message);
        }
    }

    #endregion

    #region 搜索

    /// <summary>
    /// 搜索类型
    /// </summary>
    public SearchTypesResult SearchTypes(AssemblyContext context, string keyword, string? namespaceFilter = null, int limit = 50)
    {
        try
        {
            var types = context.Assembly?.MainModule.Types
                .Where(t => t.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Where(t => namespaceFilter == null || t.Namespace?.StartsWith(namespaceFilter) == true)
                .Take(limit)
                .Select(t => new TypeSummary
                {
                    FullName = t.FullName,
                    Namespace = t.Namespace,
                    Name = t.Name,
                    Kind = GetTypeKind(t),
                    MethodCount = t.Methods.Count,
                    FieldCount = t.Fields.Count
                })
                .ToList() ?? new List<TypeSummary>();

            return new SearchTypesResult
            {
                IsSuccess = true,
                Types = types,
                TotalCount = types.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search types: {Keyword}", keyword);
            return new SearchTypesResult { IsSuccess = false, ErrorMessage = ex.Message, Types = new List<TypeSummary>() };
        }
    }

    /// <summary>
    /// 搜索字符串字面量
    /// </summary>
    public SearchStringsResult SearchStrings(AssemblyContext context, string query, string mode = "contains", int limit = 50)
    {
        try
        {
            var results = new List<StringMatch>();

            foreach (var type in context.Assembly?.MainModule.Types ?? Enumerable.Empty<TypeDefinition>())
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;

                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.Operand is string str)
                        {
                            bool matches = mode switch
                            {
                                "exact" => str == query,
                                "startswith" => str.StartsWith(query, StringComparison.OrdinalIgnoreCase),
                                _ => str.Contains(query, StringComparison.OrdinalIgnoreCase)
                            };

                            if (matches)
                            {
                                results.Add(new StringMatch
                                {
                                    Value = str,
                                    TypeName = type.FullName,
                                    MethodName = method.Name,
                                    ILOffset = instruction.Offset
                                });

                                if (results.Count >= limit) break;
                            }
                        }
                    }
                    if (results.Count >= limit) break;
                }
                if (results.Count >= limit) break;
            }

            return new SearchStringsResult
            {
                IsSuccess = true,
                Matches = results,
                TotalCount = results.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search strings: {Query}", query);
            return new SearchStringsResult { IsSuccess = false, ErrorMessage = ex.Message, Matches = new List<StringMatch>() };
        }
    }

    #endregion

    #region 交叉引用

    /// <summary>
    /// 查找类型引用
    /// </summary>
    public XRefResult FindReferencesToType(AssemblyContext context, string typeName, int limit = 50)
    {
        try
        {
            var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == typeName);
            if (type == null)
            {
                return new XRefResult { IsSuccess = false, ErrorMessage = $"Type '{typeName}' not found", References = new List<CrossReference>() };
            }

            var analyzer = new CrossReferenceAnalyzer(context.Assembly!.MainModule, context.Mvid);
            var refs = analyzer.FindReferencesToType(type).Take(limit).ToList();

            return new XRefResult
            {
                IsSuccess = true,
                References = refs,
                TotalCount = refs.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find references to type: {TypeName}", typeName);
            return new XRefResult { IsSuccess = false, ErrorMessage = ex.Message, References = new List<CrossReference>() };
        }
    }

    /// <summary>
    /// 查找方法调用
    /// </summary>
    public XRefResult FindCallsToMethod(AssemblyContext context, string typeName, string methodName, int limit = 50)
    {
        try
        {
            var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == typeName);
            if (type == null)
            {
                return new XRefResult { IsSuccess = false, ErrorMessage = $"Type '{typeName}' not found", References = new List<CrossReference>() };
            }

            var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method == null)
            {
                return new XRefResult { IsSuccess = false, ErrorMessage = $"Method '{methodName}' not found", References = new List<CrossReference>() };
            }

            var analyzer = new CrossReferenceAnalyzer(context.Assembly!.MainModule, context.Mvid);
            var refs = analyzer.FindCallsToMethod(method).Take(limit).ToList();

            return new XRefResult
            {
                IsSuccess = true,
                References = refs,
                TotalCount = refs.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find calls to method: {TypeName}.{MethodName}", typeName, methodName);
            return new XRefResult { IsSuccess = false, ErrorMessage = ex.Message, References = new List<CrossReference>() };
        }
    }

    #endregion

    #region 调用图

    /// <summary>
    /// 构建调用图
    /// </summary>
    public CallGraphResult BuildCallGraph(AssemblyContext context, string typeName, string methodName, string direction = "callees", int maxDepth = 3, int maxNodes = 100)
    {
        try
        {
            var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == typeName);
            if (type == null)
            {
                return new CallGraphResult { IsSuccess = false, ErrorMessage = $"Type '{typeName}' not found" };
            }

            var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method == null)
            {
                return new CallGraphResult { IsSuccess = false, ErrorMessage = $"Method '{methodName}' not found" };
            }

            var analyzer = new CallGraphAnalyzer(context.Assembly!.MainModule, context.Mvid);
            var chainResult = analyzer.GetCallChain(method, maxDepth);

            return new CallGraphResult
            {
                IsSuccess = true,
                StartMethod = chainResult.StartMethodName,
                Levels = chainResult.Levels.Select(l => new CallGraphLevel
                {
                    Depth = l.Depth,
                    Methods = l.Methods
                }).ToList(),
                MaxDepthReached = chainResult.MaxDepthReached
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build call graph: {TypeName}.{MethodName}", typeName, methodName);
            return new CallGraphResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    #endregion

    #region Helpers

    private static string GetTypeKind(TypeDefinition type)
    {
        if (type.IsInterface) return "interface";
        if (type.IsEnum) return "enum";
        if (type.IsValueType) return "struct";
        return "class";
    }

    #endregion
}

#region Result Types

public record TypeInfoResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? FullName { get; init; }
    public string? Namespace { get; init; }
    public string? Name { get; init; }
    public string? BaseType { get; init; }
    public bool IsPublic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsSealed { get; init; }
    public bool IsInterface { get; init; }
    public bool IsEnum { get; init; }
    public bool IsValueType { get; init; }
    public List<string>? Interfaces { get; init; }
    public List<MethodSummary>? Methods { get; init; }
    public List<FieldSummary>? Fields { get; init; }
    public List<PropertySummary>? Properties { get; init; }

    public static TypeInfoResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}

public record MethodSummary
{
    public required string Name { get; init; }
    public required string ReturnType { get; init; }
    public int ParameterCount { get; init; }
    public bool IsPublic { get; init; }
    public bool IsStatic { get; init; }
    public bool IsVirtual { get; init; }
}

public record FieldSummary
{
    public required string Name { get; init; }
    public required string FieldType { get; init; }
    public bool IsPublic { get; init; }
    public bool IsStatic { get; init; }
}

public record PropertySummary
{
    public required string Name { get; init; }
    public required string PropertyType { get; init; }
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
}

public record TypeSummary
{
    public required string FullName { get; init; }
    public string? Namespace { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public int MethodCount { get; init; }
    public int FieldCount { get; init; }
}

public record SearchTypesResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public required List<TypeSummary> Types { get; init; }
    public int TotalCount { get; init; }
}

public record StringMatch
{
    public required string Value { get; init; }
    public required string TypeName { get; init; }
    public required string MethodName { get; init; }
    public int ILOffset { get; init; }
}

public record SearchStringsResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public required List<StringMatch> Matches { get; init; }
    public int TotalCount { get; init; }
}

public record XRefResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public required List<CrossReference> References { get; init; }
    public int TotalCount { get; init; }
}

public record CallGraphResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StartMethod { get; init; }
    public List<CallGraphLevel>? Levels { get; init; }
    public bool MaxDepthReached { get; init; }
}

public record CallGraphLevel
{
    public int Depth { get; init; }
    public required List<string> Methods { get; init; }
}

#endregion
