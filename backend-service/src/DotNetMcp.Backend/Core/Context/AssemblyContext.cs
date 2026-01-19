using Mono.Cecil;

namespace DotNetMcp.Backend.Core.Context;

/// <summary>
/// 程序集上下文 - 管理程序集的加载、解析和生命周期
/// </summary>
public class AssemblyContext : IDisposable
{
    private readonly string _assemblyPath;
    private readonly CustomAssemblyResolver _resolver;
    private AssemblyDefinition? _assembly;
    private bool _disposed;

    /// <summary>
    /// 程序集定义
    /// </summary>
    public AssemblyDefinition Assembly
    {
        get
        {
            if (_assembly == null)
                throw new InvalidOperationException("Assembly not loaded. Call LoadAsync first.");
            return _assembly;
        }
    }

    /// <summary>
    /// 模块版本 ID (MVID)
    /// </summary>
    public Guid Mvid => Assembly.MainModule.Mvid;

    /// <summary>
    /// 程序集名称
    /// </summary>
    public string Name => Assembly.Name.Name;

    /// <summary>
    /// 程序集版本
    /// </summary>
    public Version Version => Assembly.Name.Version;

    /// <summary>
    /// 是否已加载
    /// </summary>
    public bool IsLoaded => _assembly != null;

    public AssemblyContext(string assemblyPath, IEnumerable<string>? searchPaths = null)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path cannot be null or empty", nameof(assemblyPath));

        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");

        _assemblyPath = Path.GetFullPath(assemblyPath);
        _resolver = new CustomAssemblyResolver(searchPaths);
    }

    /// <summary>
    /// 异步加载程序集
    /// </summary>
    public async Task<AssemblyLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_assembly != null)
            return AssemblyLoadResult.Success(this);

        try
        {
            // Cecil 的读取是同步的，使用 Task.Run 避免阻塞
            _assembly = await Task.Run(() =>
            {
                var readerParameters = new ReaderParameters
                {
                    AssemblyResolver = _resolver,
                    ReadingMode = ReadingMode.Deferred, // 延迟加载优化内存
                    InMemory = true // 避免文件锁定
                };

                return AssemblyDefinition.ReadAssembly(_assemblyPath, readerParameters);
            }, cancellationToken);

            return AssemblyLoadResult.Success(this);
        }
        catch (BadImageFormatException ex)
        {
            return AssemblyLoadResult.Failure(
                AssemblyLoadErrorCode.InvalidFormat,
                $"Invalid assembly format: {ex.Message}",
                ex
            );
        }
        catch (FileNotFoundException ex)
        {
            return AssemblyLoadResult.Failure(
                AssemblyLoadErrorCode.DependencyNotFound,
                $"Dependency not found: {ex.Message}",
                ex
            );
        }
        catch (Exception ex)
        {
            return AssemblyLoadResult.Failure(
                AssemblyLoadErrorCode.Unknown,
                $"Failed to load assembly: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// 获取程序集信息摘要
    /// </summary>
    public AssemblyInfo GetInfo()
    {
        if (!IsLoaded)
            throw new InvalidOperationException("Assembly not loaded");

        var module = Assembly.MainModule;
        
        return new AssemblyInfo
        {
            Name = Assembly.Name.Name,
            FullName = Assembly.FullName,
            Version = Assembly.Name.Version.ToString(),
            Mvid = module.Mvid.ToString(),
            TargetFramework = GetTargetFramework(module),
            TypesCount = module.Types.Count,
            Dependencies = Assembly.MainModule.AssemblyReferences
                .Select(r => r.FullName)
                .ToList()
        };
    }

    /// <summary>
    /// 获取目标框架
    /// </summary>
    private static string GetTargetFramework(ModuleDefinition module)
    {
        // 查找 TargetFrameworkAttribute
        var targetFrameworkAttr = module.Assembly.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute");

        if (targetFrameworkAttr?.ConstructorArguments.Count > 0)
        {
            return targetFrameworkAttr.ConstructorArguments[0].Value?.ToString() ?? "Unknown";
        }

        // 回退：根据 Runtime 版本推测
        var runtime = module.Runtime;
        return runtime switch
        {
            TargetRuntime.Net_1_0 => ".NETFramework,Version=v1.0",
            TargetRuntime.Net_1_1 => ".NETFramework,Version=v1.1",
            TargetRuntime.Net_2_0 => ".NETFramework,Version=v2.0",
            TargetRuntime.Net_4_0 => ".NETFramework,Version=v4.0",
            _ => $"Runtime: {runtime}"
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _assembly?.Dispose();
        _resolver?.Dispose();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 程序集加载结果
/// </summary>
public record AssemblyLoadResult
{
    public bool IsSuccess { get; init; }
    public AssemblyContext? Context { get; init; }
    public AssemblyLoadErrorCode? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }

    public static AssemblyLoadResult Success(AssemblyContext context)
        => new() { IsSuccess = true, Context = context };

    public static AssemblyLoadResult Failure(
        AssemblyLoadErrorCode errorCode,
        string message,
        Exception? exception = null)
        => new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
            Exception = exception
        };
}

/// <summary>
/// 程序集加载错误码
/// </summary>
public enum AssemblyLoadErrorCode
{
    Unknown = 1000,
    FileNotFound = 4001,
    InvalidFormat = 4002,
    DependencyNotFound = 4003,
    AccessDenied = 4004
}

/// <summary>
/// 程序集信息
/// </summary>
public record AssemblyInfo
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Version { get; init; }
    public required string Mvid { get; init; }
    public required string TargetFramework { get; init; }
    public required int TypesCount { get; init; }
    public required List<string> Dependencies { get; init; }
}
