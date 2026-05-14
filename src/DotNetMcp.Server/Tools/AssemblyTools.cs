using System.ComponentModel;
using ModelContextProtocol.Server;
using DotNetMcp.Server.Backend;
using DotNetMcp.Backend.Core.Utils;

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
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new LoadAssemblyResult { Success = false, Error = err };

        var result = await backend.LoadAssemblyAsync(path, searchPaths);
        if (!result.IsSuccess)
        {
            return new LoadAssemblyResult
            {
                Success = false,
                Error = result.ErrorCode.HasValue
                    ? $"[{result.ErrorCode}] {result.ErrorMessage ?? "Failed to load assembly"}"
                    : result.ErrorMessage ?? "Failed to load assembly"
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
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new ListAssembliesResult { Success = false, Error = err, Assemblies = Array.Empty<AssemblyInfoDto>() };

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
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new UnloadAssemblyResult { Success = false, Error = err };

        var success = await backend.UnloadAssemblyAsync(mvid);
        return new UnloadAssemblyResult
        {
            Success = success,
            Error = success ? null : $"Assembly '{mvid}' not found. Use 'list_assemblies' to see loaded assemblies."
        };
    }

    /// <summary>
    /// 探测 Unity 游戏目录中的 Assembly-CSharp.dll
    /// </summary>
    [McpServerTool(Name = "detect_unity_assembly"), Description("Detect Assembly-CSharp.dll in a Unity game directory. Supports Windows/macOS/Linux Unity layouts. Returns the assembly path, managed directory, and list of all managed dlls. Useful when reverse engineering Unity games where the exact dll path is unknown.")]
    public DetectUnityAssemblyResult DetectUnityAssembly(
        [Description("Path to the Unity game root directory or .app bundle")] string gameRootPath)
    {
        var result = UnityPathDetector.Detect(gameRootPath);
        return new DetectUnityAssemblyResult
        {
            Success = result.IsSuccess,
            AssemblyCSharpPath = result.AssemblyCSharpPath,
            ManagedDirectory = result.ManagedDirectory,
            GameName = result.GameName,
            Platform = result.Platform,
            UnityVersion = result.UnityVersion,
            ManagedAssemblies = result.ManagedAssemblies.ToArray(),
            Error = result.ErrorMessage
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

public record DetectUnityAssemblyResult
{
    public bool Success { get; init; }
    public string? AssemblyCSharpPath { get; init; }
    public string? ManagedDirectory { get; init; }
    public string? GameName { get; init; }
    public string? Platform { get; init; }
    public string? UnityVersion { get; init; }
    public string[] ManagedAssemblies { get; init; } = Array.Empty<string>();
    public string? Error { get; init; }
}
