using System.ComponentModel;
using ModelContextProtocol.Server;
using DotNetMcp.Server.Backend;

namespace DotNetMcp.Server.Tools;

/// <summary>
/// 搜索工具
/// </summary>
[McpServerToolType]
public sealed class SearchTools
{
    private readonly IBackendRegistry _registry;

    public SearchTools(IBackendRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// 搜索类型
    /// </summary>
    [McpServerTool(Name = "search_types"), Description("Search for types by name. Performs case-insensitive substring matching (e.g., 'Service' matches 'UserService', 'ServiceBase'). Use empty keyword to list all types. Filter by namespace for focused results.")]
    public async Task<SearchTypesToolResult> SearchTypes(
        [Description("Keyword to search for in type names. Case-insensitive substring match. Use empty string to list all types.")] string keyword,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Filter results to types within this namespace (e.g., 'MyApp.Services')")] string? namespaceFilter = null,
        [Description("Maximum number of results to return (default: 50)")] int limit = 50,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new SearchTypesToolResult { Success = false, Error = "No backend available. Use 'list_backends' to check registered backends, or ensure the local backend is enabled.", Types = Array.Empty<TypeDto>() };
        }

        var result = await backend.SearchTypesAsync(mvid ?? "", keyword, namespaceFilter, limit);
        return new SearchTypesToolResult
        {
            Success = result.IsSuccess,
            Types = result.Types?.Select(t => new TypeDto
            {
                FullName = t.FullName,
                Namespace = t.Namespace,
                Name = t.Name,
                Kind = t.Kind,
                MethodCount = t.MethodCount,
                FieldCount = t.FieldCount
            }).ToArray() ?? Array.Empty<TypeDto>(),
            TotalCount = result.TotalCount,
            Error = result.IsSuccess ? null : $"SearchTypes failed (keyword='{keyword}'): {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 搜索字符串字面量
    /// </summary>
    [McpServerTool(Name = "search_strings"), Description("Find hardcoded string literals in IL code. Useful for finding config keys, error messages, URLs, credentials, or API endpoints embedded in the assembly. Returns the string value and its location (type, method, IL offset).")]
    public async Task<SearchStringsToolResult> SearchStrings(
        [Description("String pattern to search for (e.g., 'password', 'http://', 'api_key')")] string query,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Search mode: 'contains' (substring match, default), 'exact' (exact match), or 'startswith' (prefix match)")] string mode = "contains",
        [Description("Maximum number of results to return (default: 50)")] int limit = 50,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new SearchStringsToolResult { Success = false, Error = "No backend available. Use 'list_backends' to check registered backends, or ensure the local backend is enabled.", Matches = Array.Empty<StringMatchDto>() };
        }

        var result = await backend.SearchStringsAsync(mvid ?? "", query, mode, limit);
        return new SearchStringsToolResult
        {
            Success = result.IsSuccess,
            Matches = result.Matches?.Select(m => new StringMatchDto
            {
                Value = m.Value,
                TypeName = m.TypeName,
                MethodName = m.MethodName,
                ILOffset = m.ILOffset
            }).ToArray() ?? Array.Empty<StringMatchDto>(),
            TotalCount = result.TotalCount,
            Error = result.IsSuccess ? null : $"SearchStrings failed (query='{query}'): {result.ErrorMessage}"
        };
    }
}

public record SearchTypesToolResult
{
    public bool Success { get; init; }
    public required TypeDto[] Types { get; init; }
    public int TotalCount { get; init; }
    public string? Error { get; init; }
}

public record TypeDto
{
    public required string FullName { get; init; }
    public string? Namespace { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public int MethodCount { get; init; }
    public int FieldCount { get; init; }
}

public record SearchStringsToolResult
{
    public bool Success { get; init; }
    public required StringMatchDto[] Matches { get; init; }
    public int TotalCount { get; init; }
    public string? Error { get; init; }
}

public record StringMatchDto
{
    public required string Value { get; init; }
    public required string TypeName { get; init; }
    public required string MethodName { get; init; }
    public int ILOffset { get; init; }
}
