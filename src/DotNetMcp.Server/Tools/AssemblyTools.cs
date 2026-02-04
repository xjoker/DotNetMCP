using System.ComponentModel;
using ModelContextProtocol.Server;
using DotNetMcp.Server.Backend;

namespace DotNetMcp.Server.Tools;

/// <summary>
/// 程序集管理工具
/// </summary>
[McpServerToolType]
public sealed class AssemblyTools
{
    private readonly IBackendRegistry _registry;

    public AssemblyTools(IBackendRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// 加载 .NET 程序集进行分析
    /// </summary>
    [McpServerTool(Name = "load_assembly"), Description("Load a .NET assembly (DLL/EXE) for analysis. Returns MVID (Module Version ID) as unique identifier for subsequent operations. Use searchPaths for assemblies with external dependencies.")]
    public async Task<LoadAssemblyResult> LoadAssembly(
        [Description("Path to the .NET assembly file (.dll or .exe)")] string path,
        [Description("Additional directories to search for dependencies (e.g., NuGet packages folder)")] string[]? searchPaths = null,
        [Description("Backend ID to use. Omit to use the default backend.")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new LoadAssemblyResult { Success = false, Error = "No backend available" };
        }

        var result = await backend.LoadAssemblyAsync(path, searchPaths);
        if (!result.IsSuccess)
        {
            return new LoadAssemblyResult
            {
                Success = false,
                Error = result.ErrorMessage ?? "Failed to load assembly"
            };
        }

        return new LoadAssemblyResult
        {
            Success = true,
            Mvid = result.Context?.Mvid.ToString(),
            Name = result.Context?.Name,
            Backend = backend.Id
        };
    }

    /// <summary>
    /// 列出所有已加载的程序集
    /// </summary>
    [McpServerTool(Name = "list_assemblies"), Description("List all loaded assemblies with their MVID, name, and path. Shows which assembly is the default (used when mvid is omitted in other tools). Use to verify loaded state or find MVID for specific operations.")]
    public async Task<ListAssembliesResult> ListAssemblies(
        [Description("Backend ID to query. Omit to use the default backend.")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new ListAssembliesResult { Success = false, Error = "No backend available", Assemblies = Array.Empty<AssemblyInfoDto>() };
        }

        var assemblies = await backend.ListAssembliesAsync();
        return new ListAssembliesResult
        {
            Success = true,
            Assemblies = assemblies.Select(a => new AssemblyInfoDto
            {
                Mvid = a.Mvid,
                Name = a.Name,
                Path = a.Path,
                IsDefault = a.IsDefault
            }).ToArray(),
            Backend = backend.Id
        };
    }

    /// <summary>
    /// 卸载程序集
    /// </summary>
    [McpServerTool(Name = "unload_assembly"), Description("Remove an assembly from memory. Use to free resources or before reloading a modified version. The assembly's MVID becomes invalid after unloading.")]
    public async Task<UnloadAssemblyResult> UnloadAssembly(
        [Description("The MVID (Module Version ID) of the assembly to unload")] string mvid,
        [Description("Backend ID. Omit to use the default backend.")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new UnloadAssemblyResult { Success = false, Error = "No backend available" };
        }

        var success = await backend.UnloadAssemblyAsync(mvid);
        return new UnloadAssemblyResult
        {
            Success = success,
            Error = success ? null : "Failed to unload assembly"
        };
    }
}

public record LoadAssemblyResult
{
    public bool Success { get; init; }
    public string? Mvid { get; init; }
    public string? Name { get; init; }
    public string? Backend { get; init; }
    public string? Error { get; init; }
}

public record ListAssembliesResult
{
    public bool Success { get; init; }
    public required AssemblyInfoDto[] Assemblies { get; init; }
    public string? Backend { get; init; }
    public string? Error { get; init; }
}

public record AssemblyInfoDto
{
    public required string Mvid { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public bool IsDefault { get; init; }
}

public record UnloadAssemblyResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}
