using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Analysis;
using Microsoft.Extensions.Logging;
using DiffComparator = DotNetMcp.Backend.Core.Modification.DiffComparator;
using ModificationDiffType = DotNetMcp.Backend.Core.Modification.DiffType;

namespace DotNetMcp.Server.Backend;

/// <summary>
/// 本地后端 - 直接调用 Core 服务
/// </summary>
public class LocalBackend : IBackend
{
    private readonly IAssemblyManager _assemblyManager;
    private readonly AnalysisService _analysisService;
    private readonly ModificationService _modificationService;
    private readonly ILogger<LocalBackend> _logger;
    private bool _isHealthy = true;
    private DateTime? _lastHealthCheck;

    public LocalBackend(
        IAssemblyManager assemblyManager,
        AnalysisService analysisService,
        ModificationService modificationService,
        ILogger<LocalBackend> logger)
    {
        _assemblyManager = assemblyManager;
        _analysisService = analysisService;
        _modificationService = modificationService;
        _logger = logger;
    }

    public string Id => "local";
    public string Name => "Local Backend";
    public BackendType Type => BackendType.Local;
    public bool IsHealthy => _isHealthy;
    public DateTime? LastHealthCheck => _lastHealthCheck;

    public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        _isHealthy = true;
        _lastHealthCheck = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    #region 程序集操作

    public async Task<AssemblyLoadResult> LoadAssemblyAsync(string path, IEnumerable<string>? searchPaths = null, CancellationToken cancellationToken = default)
    {
        return await _assemblyManager.LoadAsync(path, searchPaths, cancellationToken);
    }

    public Task<bool> UnloadAssemblyAsync(string mvid, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_assemblyManager.Unload(mvid));
    }

    public Task<IReadOnlyList<AssemblyInfo>> ListAssembliesAsync(CancellationToken cancellationToken = default)
    {
        var assemblies = _assemblyManager.GetAll();
        var defaultMvid = _assemblyManager.DefaultMvid;

        var result = assemblies.Select(ctx => new AssemblyInfo
        {
            Mvid = ctx.Mvid.ToString(),
            Name = ctx.Name ?? "Unknown",
            Path = ctx.AssemblyPath ?? "",
            IsDefault = ctx.Mvid.ToString() == defaultMvid
        }).ToList().AsReadOnly();

        return Task.FromResult<IReadOnlyList<AssemblyInfo>>(result);
    }

    #endregion

    #region 分析操作

    public Task<DecompileResult> DecompileTypeAsync(string mvid, string typeName, string language = "csharp", bool preferOriginalSource = false, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(DecompileResult.Failure($"Assembly '{mvid}' not found"));
        }

        if (preferOriginalSource && language != "il" && context.AssemblyPath != null)
        {
            var resolver = new OriginalSourceResolver();
            var source = resolver.TryResolveType(context.AssemblyPath, typeName);
            if (source != null)
            {
                return Task.FromResult(DecompileResult.Success(source.Code, typeName));
            }
        }

        return Task.FromResult(_analysisService.DecompileType(context, typeName, language));
    }

    public Task<DecompileResult> DecompileMethodAsync(string mvid, string typeName, string methodName, string language = "csharp", bool preferOriginalSource = false, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(DecompileResult.Failure($"Assembly '{mvid}' not found"));
        }

        if (preferOriginalSource && language != "il" && context.AssemblyPath != null)
        {
            var resolver = new OriginalSourceResolver();
            var source = resolver.TryResolveType(context.AssemblyPath, typeName);
            if (source != null)
            {
                return Task.FromResult(DecompileResult.Success(source.Code, $"{typeName}.{methodName}"));
            }
        }

        return Task.FromResult(_analysisService.DecompileMethod(context, typeName, methodName, language));
    }

    public Task<SearchTypesResult> SearchTypesAsync(string mvid, string keyword, string? namespaceFilter = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new SearchTypesResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found", Types = new List<TypeSummary>() });
        }

        return Task.FromResult(_analysisService.SearchTypes(context, keyword, namespaceFilter, limit));
    }

    public Task<SearchStringsResult> SearchStringsAsync(string mvid, string query, string mode = "contains", int limit = 50, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new SearchStringsResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found", Matches = new List<StringMatch>() });
        }

        return Task.FromResult(_analysisService.SearchStrings(context, query, mode, limit));
    }

    public Task<XRefResult> FindReferencesToTypeAsync(string mvid, string typeName, int limit = 50, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new XRefResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found", References = new List<CrossReference>() });
        }

        return Task.FromResult(_analysisService.FindReferencesToType(context, typeName, limit));
    }

    public Task<XRefResult> FindCallsToMethodAsync(string mvid, string typeName, string methodName, int limit = 50, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new XRefResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found", References = new List<CrossReference>() });
        }

        return Task.FromResult(_analysisService.FindCallsToMethod(context, typeName, methodName, limit));
    }

    public Task<CallGraphResult> BuildCallGraphAsync(string mvid, string typeName, string methodName, string direction = "callees", int maxDepth = 3, int maxNodes = 100, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new CallGraphResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found" });
        }

        return Task.FromResult(_analysisService.BuildCallGraph(context, typeName, methodName, direction, maxDepth, maxNodes));
    }

    public Task<CFGResult> BuildControlFlowGraphAsync(string mvid, string typeName, string methodName, bool includeIL = false, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new CFGResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found" });
        }

        return Task.FromResult(_analysisService.BuildControlFlowGraph(context, typeName, methodName, includeIL));
    }

    #endregion

    #region 批量操作

    public Task<BatchDecompileResult> BatchDecompileAsync(string mvid, string[] memberKeys, int maxTotalChars = 200000, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(BatchDecompileResult.Failure($"Assembly '{mvid}' not found"));
        }

        var items = new List<BatchDecompileItem>();
        var totalChars = 0;
        var truncated = false;

        foreach (var key in memberKeys)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            DecompileResult result;
            if (key.Contains("::"))
            {
                var parts = key.Split("::", 2);
                result = _analysisService.DecompileMethod(context, parts[0], parts[1]);
            }
            else
            {
                result = _analysisService.DecompileType(context, key);
            }

            var code = result.IsSuccess ? result.Code ?? "" : $"// Error: {result.ErrorMessage}";
            var codeLength = code.Length;

            if (totalChars + codeLength > maxTotalChars && items.Count > 0)
            {
                truncated = true;
                break;
            }

            totalChars += codeLength;
            items.Add(new BatchDecompileItem
            {
                MemberKey = key,
                Code = code,
                TotalLines = code.Split('\n').Length,
                IsError = !result.IsSuccess
            });
        }

        return Task.FromResult(BatchDecompileResult.Success(items, truncated, totalChars, items.Count, memberKeys.Length));
    }

    public Task<ChunkingPlanResult> PlanChunkingAsync(string mvid, string typeName, string? methodName = null, int targetChunkSize = 6000, int overlap = 2, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
            return Task.FromResult(ChunkingPlanResult.Failure($"Assembly '{mvid}' not found"));

        // Decompile the member
        var decompileResult = methodName != null
            ? _analysisService.DecompileMethod(context, typeName, methodName)
            : _analysisService.DecompileType(context, typeName);

        if (!decompileResult.IsSuccess || string.IsNullOrEmpty(decompileResult.Code))
            return Task.FromResult(ChunkingPlanResult.Failure(decompileResult.ErrorMessage ?? "Decompilation failed"));

        var lines = decompileResult.Code.Split('\n');
        var totalLines = lines.Length;

        if (totalLines == 0)
            return Task.FromResult(ChunkingPlanResult.Success(new List<ChunkInfo>(), 0, 0));

        // Estimate avgCharsPerLine from sample
        var sampleSize = Math.Min(totalLines, 20);
        var sampleChars = lines.Take(sampleSize).Sum(l => l.Length);
        var avgCharsPerLine = Math.Max(1, sampleChars / sampleSize);

        // Calculate lines per chunk
        var linesPerChunk = Math.Max(1, targetChunkSize / avgCharsPerLine);

        if (overlap >= linesPerChunk)
            return Task.FromResult(ChunkingPlanResult.Failure($"Overlap ({overlap}) must be less than lines per chunk ({linesPerChunk}). Increase targetChunkSize or reduce overlap."));

        var chunks = new List<ChunkInfo>();
        var currentStart = 1;

        while (currentStart <= totalLines)
        {
            var currentEnd = Math.Min(currentStart + linesPerChunk - 1, totalLines);
            chunks.Add(new ChunkInfo
            {
                StartLine = currentStart,
                EndLine = currentEnd,
                EstimatedChars = (currentEnd - currentStart + 1) * avgCharsPerLine
            });

            if (currentEnd >= totalLines) break;

            var nextStart = currentEnd + 1 - overlap;
            if (nextStart <= currentStart) nextStart = currentStart + 1;
            currentStart = nextStart;
        }

        return Task.FromResult(ChunkingPlanResult.Success(chunks, totalLines, avgCharsPerLine));
    }

    public Task<TypeOutlineResult> GetTypeOutlineAsync(string mvid, string typeName, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
            return Task.FromResult(TypeOutlineResult.Failure($"Assembly '{mvid}' not found"));

        var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == typeName);
        if (type == null)
            return Task.FromResult(TypeOutlineResult.Failure($"Type '{typeName}' not found"));

        var kind = type.IsInterface ? "Interface" : type.IsEnum ? "Enum" : type.IsValueType ? "Struct" : "Class";
        var accessibility = type.IsPublic || type.IsNestedPublic ? "Public"
            : type.IsNestedFamily ? "Protected"
            : type.IsNestedAssembly ? "Internal"
            : "Private";

        var result = new TypeOutlineResult
        {
            IsSuccess = true,
            TypeName = type.FullName,
            Kind = kind,
            Namespace = type.Namespace,
            Accessibility = accessibility,
            BaseType = type.BaseType?.FullName,
            Interfaces = type.Interfaces.Select(i => i.InterfaceType.FullName).ToList(),
            Members = new List<MemberOutlineItem>()
        };

        foreach (var method in type.Methods)
        {
            var paramStr = string.Join(", ", method.Parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
            result.Members.Add(new MemberOutlineItem
            {
                Kind = method.IsConstructor ? "Constructor" : "Method",
                Name = method.Name,
                Signature = $"{method.ReturnType.Name} {method.Name}({paramStr})",
                Accessibility = method.IsPublic ? "Public" : method.IsFamily ? "Protected" : method.IsAssembly ? "Internal" : "Private",
                IsStatic = method.IsStatic,
                IsVirtual = method.IsVirtual,
                IsAbstract = method.IsAbstract
            });
        }

        foreach (var field in type.Fields)
        {
            result.Members.Add(new MemberOutlineItem
            {
                Kind = "Field",
                Name = field.Name,
                Signature = $"{field.FieldType.Name} {field.Name}",
                Accessibility = field.IsPublic ? "Public" : field.IsFamily ? "Protected" : field.IsAssembly ? "Internal" : "Private",
                IsStatic = field.IsStatic
            });
        }

        foreach (var prop in type.Properties)
        {
            var getter = prop.GetMethod;
            var setter = prop.SetMethod;
            var accessMethod = getter ?? setter;
            result.Members.Add(new MemberOutlineItem
            {
                Kind = "Property",
                Name = prop.Name,
                Signature = $"{prop.PropertyType.Name} {prop.Name} {{ {(getter != null ? "get; " : "")}{(setter != null ? "set; " : "")}}}",
                Accessibility = accessMethod?.IsPublic == true ? "Public" : accessMethod?.IsFamily == true ? "Protected" : "Private",
                IsStatic = accessMethod?.IsStatic ?? false
            });
        }

        foreach (var evt in type.Events)
        {
            result.Members.Add(new MemberOutlineItem
            {
                Kind = "Event",
                Name = evt.Name,
                Signature = $"{evt.EventType.Name} {evt.Name}",
                Accessibility = evt.AddMethod?.IsPublic == true ? "Public" : "Private"
            });
        }

        return Task.FromResult(result);
    }

    public Task<PatchSkeletonResult> GeneratePatchSkeletonAsync(string mvid, string typeName, string methodName, string[] patchKinds, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
            return Task.FromResult(PatchSkeletonResult.Failure($"Assembly '{mvid}' not found"));

        var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == typeName);
        if (type == null)
            return Task.FromResult(PatchSkeletonResult.Failure($"Type '{typeName}' not found"));

        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (method == null)
            return Task.FromResult(PatchSkeletonResult.Failure($"Method '{methodName}' not found in type '{typeName}'"));

        var generator = new PatchSkeletonGenerator();
        return Task.FromResult(generator.Generate(method, patchKinds));
    }

    public Task<CompareAssembliesResult> CompareAssembliesAsync(string leftMvid, string rightMvid, string? namespaceFilter = null, bool includeUnchanged = false, CancellationToken cancellationToken = default)
    {
        var leftContext = GetContext(leftMvid);
        var rightContext = GetContext(rightMvid);

        if (leftContext == null)
            return Task.FromResult(CompareAssembliesResult.Failure($"Left assembly '{leftMvid}' not found"));
        if (rightContext == null)
            return Task.FromResult(CompareAssembliesResult.Failure($"Right assembly '{rightMvid}' not found"));
        if (leftContext.Assembly == null || rightContext.Assembly == null)
            return Task.FromResult(CompareAssembliesResult.Failure("Assembly not loaded"));

        var comparator = new DiffComparator();
        var diff = comparator.CompareAssemblies(leftContext.Assembly, rightContext.Assembly);

        var summary = new CompareAssembliesSummary();
        var items = new List<CompareTypeDiffItem>();

        foreach (var typeDiff in diff.TypeDiffs)
        {
            if (namespaceFilter != null)
            {
                var ns = typeDiff.TypeName.Contains('.')
                    ? typeDiff.TypeName[..typeDiff.TypeName.LastIndexOf('.')]
                    : "";
                if (!ns.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            switch (typeDiff.DiffType)
            {
                case ModificationDiffType.Added: summary.Added++; break;
                case ModificationDiffType.Removed: summary.Removed++; break;
                case ModificationDiffType.Modified: summary.Modified++; break;
                default: summary.Unchanged++; break;
            }

            if (!includeUnchanged && typeDiff.DiffType == ModificationDiffType.Unchanged)
                continue;

            items.Add(new CompareTypeDiffItem
            {
                TypeName = typeDiff.TypeName,
                DiffType = typeDiff.DiffType.ToString(),
                MemberDiffs = typeDiff.MemberDiffs.Select(m => new CompareMemberDiffItem
                {
                    Name = m.MemberName,
                    MemberType = m.MemberType,
                    DiffType = m.DiffType.ToString()
                }).ToList()
            });
        }

        return Task.FromResult(CompareAssembliesResult.Success(summary, items));
    }

    #endregion

    #region 修改操作

    public Task<ModificationResult> InjectAtEntryAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(ModificationResult.Failure("ASSEMBLY_NOT_FOUND", $"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_modificationService.InjectAtEntry(context, methodFullName, request));
    }

    public Task<ModificationResult> ReplaceMethodBodyAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(ModificationResult.Failure("ASSEMBLY_NOT_FOUND", $"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_modificationService.ReplaceMethodBody(context, methodFullName, request));
    }

    public Task<ModificationResult> AddTypeAsync(string mvid, TypeCreationRequest request, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(ModificationResult.Failure("ASSEMBLY_NOT_FOUND", $"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_modificationService.AddType(context, request));
    }

    public Task<ModificationResult> SaveAssemblyAsync(string mvid, string outputPath, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(ModificationResult.Failure("ASSEMBLY_NOT_FOUND", $"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_modificationService.SaveAssembly(context, outputPath));
    }

    #endregion

    #region Helpers

    private AssemblyContext? GetContext(string? mvid)
    {
        return _assemblyManager.Get(mvid);
    }

    #endregion
}
