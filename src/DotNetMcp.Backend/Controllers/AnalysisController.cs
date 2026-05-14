using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Analysis;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 分析 API 控制器 - 提供反编译、搜索、交叉引用、调用图等功能
/// </summary>
[ApiController]
[Route("analysis")]
public class AnalysisController : ControllerBase
{
    private readonly AnalysisService _analysisService;
    private readonly IAssemblyManager _assemblyManager;

    public AnalysisController(AnalysisService analysisService, IAssemblyManager assemblyManager)
    {
        _analysisService = analysisService;
        _assemblyManager = assemblyManager;
    }

    #region 反编译

    /// <summary>
    /// 获取类型源码
    /// </summary>
    [HttpGet("type/{typeName}/source")]
    public IActionResult GetTypeSource(string typeName, [FromQuery] string language = "csharp", [FromQuery] string? mvid = null, [FromQuery] bool preferOriginalSource = false)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded. Use assembly/load to load one first." });
        }

        // 尝试 PDB 原始源码（仅嵌入和本地，远程 SourceLink 默认禁用）
        if (preferOriginalSource && language != "il" && context.AssemblyPath != null)
        {
            using var resolver = new OriginalSourceResolver();
            var source = resolver.TryResolveType(context.AssemblyPath, Uri.UnescapeDataString(typeName));
            if (source != null)
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        type_name = Uri.UnescapeDataString(typeName),
                        language,
                        code = source.Code,
                        source_kind = source.SourceKind
                    }
                });
            }
        }

        var result = _analysisService.DecompileType(context, Uri.UnescapeDataString(typeName), language);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "DECOMPILE_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                type_name = result.Target,
                language,
                code = result.Code
            }
        });
    }

    /// <summary>
    /// 获取方法源码
    /// </summary>
    [HttpGet("type/{typeName}/method/{methodName}")]
    public IActionResult GetMethodSource(string typeName, string methodName, [FromQuery] string language = "csharp", [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.DecompileMethod(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName), language);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "DECOMPILE_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                type_name = Uri.UnescapeDataString(typeName),
                method_name = Uri.UnescapeDataString(methodName),
                language = language,
                code = result.Code
            }
        });
    }

    #endregion

    #region 类型信息

    /// <summary>
    /// 获取类型详细信息
    /// </summary>
    [HttpGet("type/{typeName}/info")]
    public IActionResult GetTypeInfo(string typeName, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.GetTypeInfo(context, Uri.UnescapeDataString(typeName));
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "TYPE_NOT_FOUND", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                full_name = result.FullName,
                @namespace = result.Namespace,
                name = result.Name,
                base_type = result.BaseType,
                is_public = result.IsPublic,
                is_abstract = result.IsAbstract,
                is_sealed = result.IsSealed,
                is_interface = result.IsInterface,
                is_enum = result.IsEnum,
                is_value_type = result.IsValueType,
                interfaces = result.Interfaces,
                methods = result.Methods,
                fields = result.Fields,
                properties = result.Properties
            }
        });
    }

    #endregion

    #region 搜索

    /// <summary>
    /// 搜索类型
    /// </summary>
    [HttpGet("search/types")]
    public IActionResult SearchTypes([FromQuery] string keyword, [FromQuery] string? @namespace = null, [FromQuery] int limit = 50, [FromQuery] string? mvid = null)
    {
        if (limit < 0)
        {
            return BadRequest(new { success = false, error_code = "INVALID_LIMIT", message = "Limit must be >= 0" });
        }
        limit = Math.Min(limit, 500);

        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.SearchTypes(context, keyword, @namespace, limit);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "SEARCH_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                types = result.Types,
                total_count = result.TotalCount
            }
        });
    }

    /// <summary>
    /// 搜索字符串字面量
    /// </summary>
    [HttpGet("search/strings")]
    public IActionResult SearchStrings([FromQuery] string query, [FromQuery] string mode = "contains", [FromQuery] int limit = 50, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.SearchStrings(context, query, mode, limit);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "SEARCH_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                matches = result.Matches,
                total_count = result.TotalCount
            }
        });
    }

    #endregion

    #region 交叉引用

    /// <summary>
    /// 查找类型引用
    /// </summary>
    [HttpGet("xrefs/type/{typeName}")]
    public IActionResult GetXRefsToType(string typeName, [FromQuery] int limit = 50, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.FindReferencesToType(context, Uri.UnescapeDataString(typeName), limit);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "XREF_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                target = Uri.UnescapeDataString(typeName),
                references = result.References.Select(r => new
                {
                    source_type = r.SourceTypeName,
                    source_member = r.SourceMemberName,
                    kind = r.Kind.ToString(),
                    il_offset = r.ILOffset
                }),
                total_count = result.TotalCount
            }
        });
    }

    /// <summary>
    /// 查找方法调用
    /// </summary>
    [HttpGet("xrefs/method/{typeName}/{methodName}")]
    public IActionResult GetXRefsToMethod(string typeName, string methodName, [FromQuery] int limit = 50, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.FindCallsToMethod(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName), limit);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "XREF_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                target = $"{Uri.UnescapeDataString(typeName)}.{Uri.UnescapeDataString(methodName)}",
                references = result.References.Select(r => new
                {
                    source_type = r.SourceTypeName,
                    source_member = r.SourceMemberName,
                    kind = r.Kind.ToString(),
                    il_offset = r.ILOffset
                }),
                total_count = result.TotalCount
            }
        });
    }

    #endregion

    #region 调用图

    /// <summary>
    /// 构建调用图
    /// </summary>
    [HttpGet("callgraph/{typeName}/{methodName}")]
    public IActionResult BuildCallGraph(string typeName, string methodName, [FromQuery] string direction = "callees", [FromQuery] int max_depth = 3, [FromQuery] int max_nodes = 100, [FromQuery] string? mvid = null)
    {
        var validDirections = new[] { "callees", "callers", "both" };
        if (!validDirections.Contains(direction.ToLowerInvariant()))
        {
            return BadRequest(new { success = false, error_code = "INVALID_DIRECTION", message = $"Direction must be one of: {string.Join(", ", validDirections)}" });
        }

        if (max_depth < 0 || max_nodes < 0)
        {
            return BadRequest(new { success = false, error_code = "INVALID_PARAMETER", message = "max_depth and max_nodes must be >= 0" });
        }

        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.BuildCallGraph(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName), direction, max_depth, max_nodes);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "CALLGRAPH_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                start_method = result.StartMethod,
                direction = direction,
                max_depth_reached = result.MaxDepthReached,
                levels = result.Levels
            }
        });
    }

    #endregion

    #region 控制流图

    /// <summary>
    /// 构建控制流图
    /// </summary>
    [HttpGet("cfg/{typeName}/{methodName}")]
    public IActionResult BuildControlFlowGraph(string typeName, string methodName, [FromQuery] bool include_il = false, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.BuildControlFlowGraph(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName), include_il);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "CFG_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                method_name = result.MethodName,
                block_count = result.BlockCount,
                edge_count = result.EdgeCount,
                entry_block = result.EntryBlockId,
                exit_blocks = result.ExitBlockIds,
                blocks = result.Blocks,
                edges = result.Edges,
                mermaid = result.Mermaid
            }
        });
    }

    #endregion

    #region 依赖图

    /// <summary>
    /// 构建依赖图
    /// </summary>
    [HttpGet("dependencies")]
    public IActionResult BuildDependencyGraph([FromQuery] string level = "assembly", [FromQuery] string? root_type = null, [FromQuery] int max_depth = 3, [FromQuery] string? mvid = null)
    {
        var validLevels = new[] { "assembly", "namespace", "type" };
        if (!validLevels.Contains(level.ToLowerInvariant()))
        {
            return BadRequest(new { success = false, error_code = "INVALID_LEVEL", message = $"Level must be one of: {string.Join(", ", validLevels)}" });
        }

        if (max_depth < 0 || max_depth > 10)
        {
            return BadRequest(new { success = false, error_code = "INVALID_PARAMETER", message = "max_depth must be between 0 and 10" });
        }

        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.BuildDependencyGraph(context, level, root_type, max_depth);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "DEPENDENCY_GRAPH_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                level = result.Level,
                root_id = result.RootId,
                total_nodes = result.TotalNodes,
                internal_nodes = result.InternalNodes,
                external_nodes = result.ExternalNodes,
                total_edges = result.TotalEdges,
                nodes = result.Nodes,
                edges = result.Edges,
                mermaid = result.Mermaid
            }
        });
    }

    #endregion

    #region 设计模式检测

    /// <summary>
    /// 检测设计模式
    /// </summary>
    [HttpGet("patterns")]
    public IActionResult DetectPatterns([FromQuery] string? type_name = null, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.DetectPatterns(context, type_name != null ? Uri.UnescapeDataString(type_name) : null);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "PATTERN_DETECTION_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                total_count = result.TotalCount,
                summary = result.Summary,
                patterns = result.Patterns
            }
        });
    }

    #endregion

    #region 混淆检测

    /// <summary>
    /// 检测混淆
    /// </summary>
    [HttpGet("obfuscation")]
    public IActionResult DetectObfuscation([FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var result = _analysisService.DetectObfuscation(context);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error_code = "OBFUSCATION_DETECTION_FAILED", message = result.ErrorMessage });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                is_obfuscated = result.IsObfuscated,
                obfuscation_score = result.ObfuscationScore,
                confidence = result.Confidence,
                detected_obfuscators = result.DetectedObfuscators,
                indicators = result.Indicators,
                statistics = result.Statistics
            }
        });
    }

    #endregion

    #region 批量操作

    /// <summary>
    /// 批量获取类型源码
    /// </summary>
    [HttpPost("batch/sources")]
    public IActionResult BatchGetTypeSources([FromBody] BatchSourcesRequest request)
    {
        var context = _assemblyManager.Get(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (request.TypeNames == null || request.TypeNames.Count > 20)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Maximum 20 types per batch request" });
        }

        var results = new Dictionary<string, object>();
        foreach (var typeName in request.TypeNames)
        {
            var result = _analysisService.DecompileType(context, typeName, request.Language ?? "csharp");
            results[typeName] = new
            {
                success = result.IsSuccess,
                code = result.Code,
                error = result.ErrorMessage
            };
        }

        return Ok(new { success = true, data = results });
    }

    /// <summary>
    /// 批量获取方法源码
    /// </summary>
    [HttpPost("batch/methods")]
    public IActionResult BatchGetMethods([FromBody] BatchMethodsRequest request)
    {
        var context = _assemblyManager.Get(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (request.Methods == null || request.Methods.Count > 20)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Maximum 20 methods per batch request" });
        }

        var results = new Dictionary<string, object>();
        foreach (var method in request.Methods)
        {
            var key = $"{method.TypeName}.{method.MethodName}";
            var result = _analysisService.DecompileMethod(context, method.TypeName, method.MethodName, request.Language ?? "csharp");
            results[key] = new
            {
                success = result.IsSuccess,
                code = result.Code,
                error = result.ErrorMessage
            };
        }

        return Ok(new { success = true, data = results });
    }

    /// <summary>
    /// 批量获取交叉引用
    /// </summary>
    [HttpPost("batch/xrefs")]
    public IActionResult BatchGetXRefs([FromBody] BatchXRefsRequest request)
    {
        var context = _assemblyManager.Get(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (request.TypeNames == null || request.TypeNames.Count > 10)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Maximum 10 types per batch xref request" });
        }

        var results = new Dictionary<string, object>();
        foreach (var typeName in request.TypeNames)
        {
            var result = _analysisService.FindReferencesToType(context, typeName, request.Limit ?? 20);
            results[typeName] = new
            {
                success = result.IsSuccess,
                references = result.References.Select(r => new { r.SourceTypeName, r.Kind }),
                error = result.ErrorMessage
            };
        }

        return Ok(new { success = true, data = results });
    }

    #endregion

    #region 类型大纲

    /// <summary>
    /// 获取类型元数据大纲（无需反编译）
    /// </summary>
    [HttpGet("outline/{typeName}")]
    public IActionResult GetTypeOutline(string typeName, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });

        var decodedName = Uri.UnescapeDataString(typeName);
        var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == decodedName);
        if (type == null)
            return BadRequest(new { success = false, error_code = "TYPE_NOT_FOUND", message = $"Type '{decodedName}' not found" });

        var kind = type.IsInterface ? "Interface" : type.IsEnum ? "Enum" : type.IsValueType ? "Struct" : "Class";
        var accessibility = type.IsPublic || type.IsNestedPublic ? "Public"
            : type.IsNestedFamily ? "Protected"
            : type.IsNestedAssembly ? "Internal"
            : "Private";

        var members = new List<object>();

        foreach (var method in type.Methods)
        {
            var paramStr = string.Join(", ", method.Parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
            members.Add(new
            {
                kind = method.IsConstructor ? "Constructor" : "Method",
                name = method.Name,
                signature = $"{method.ReturnType.Name} {method.Name}({paramStr})",
                accessibility = method.IsPublic ? "Public" : method.IsFamily ? "Protected" : method.IsAssembly ? "Internal" : "Private",
                isStatic = method.IsStatic,
                isVirtual = method.IsVirtual,
                isAbstract = method.IsAbstract
            });
        }

        foreach (var field in type.Fields)
        {
            members.Add(new
            {
                kind = "Field",
                name = field.Name,
                signature = $"{field.FieldType.Name} {field.Name}",
                accessibility = field.IsPublic ? "Public" : field.IsFamily ? "Protected" : field.IsAssembly ? "Internal" : "Private",
                isStatic = field.IsStatic,
                isVirtual = false,
                isAbstract = false
            });
        }

        foreach (var prop in type.Properties)
        {
            var getter = prop.GetMethod;
            var setter = prop.SetMethod;
            var accessMethod = getter ?? setter;
            members.Add(new
            {
                kind = "Property",
                name = prop.Name,
                signature = $"{prop.PropertyType.Name} {prop.Name} {{ {(getter != null ? "get; " : "")}{(setter != null ? "set; " : "")}}}",
                accessibility = accessMethod?.IsPublic == true ? "Public" : accessMethod?.IsFamily == true ? "Protected" : "Private",
                isStatic = accessMethod?.IsStatic ?? false,
                isVirtual = false,
                isAbstract = false
            });
        }

        foreach (var evt in type.Events)
        {
            members.Add(new
            {
                kind = "Event",
                name = evt.Name,
                signature = $"{evt.EventType.Name} {evt.Name}",
                accessibility = evt.AddMethod?.IsPublic == true ? "Public" : "Private",
                isStatic = false,
                isVirtual = false,
                isAbstract = false
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                typeName = type.FullName,
                kind,
                @namespace = type.Namespace,
                accessibility,
                baseType = type.BaseType?.FullName,
                interfaces = type.Interfaces.Select(i => i.InterfaceType.FullName).ToList(),
                members
            }
        });
    }

    #endregion

    #region 分块规划

    /// <summary>
    /// 规划 LLM 友好的源码分块方案
    /// </summary>
    [HttpGet("plan-chunking")]
    public IActionResult PlanChunking([FromQuery] string typeName, [FromQuery] string? methodName = null,
        [FromQuery] int targetChunkSize = 6000, [FromQuery] int overlap = 2, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });

        var decompileResult = methodName != null
            ? _analysisService.DecompileMethod(context, typeName, methodName)
            : _analysisService.DecompileType(context, typeName);

        if (!decompileResult.IsSuccess || string.IsNullOrEmpty(decompileResult.Code))
            return BadRequest(new { success = false, error_code = "DECOMPILE_FAILED", message = decompileResult.ErrorMessage ?? "Decompilation failed" });

        var lines = decompileResult.Code.Split('\n');
        var totalLines = lines.Length;

        if (totalLines == 0)
            return Ok(new { success = true, data = new { chunks = Array.Empty<object>(), totalLines = 0, avgCharsPerLine = 0 } });

        var sampleSize = Math.Min(totalLines, 20);
        var sampleChars = lines.Take(sampleSize).Sum(l => l.Length);
        var avgCharsPerLine = Math.Max(1, sampleChars / sampleSize);
        var linesPerChunk = Math.Max(1, targetChunkSize / avgCharsPerLine);

        if (overlap >= linesPerChunk)
            return BadRequest(new { success = false, error_code = "INVALID_PARAMS", message = $"Overlap ({overlap}) must be less than lines per chunk ({linesPerChunk})" });

        var chunks = new List<object>();
        var currentStart = 1;

        while (currentStart <= totalLines)
        {
            var currentEnd = Math.Min(currentStart + linesPerChunk - 1, totalLines);
            chunks.Add(new { startLine = currentStart, endLine = currentEnd, estimatedChars = (currentEnd - currentStart + 1) * avgCharsPerLine });

            if (currentEnd >= totalLines) break;
            var nextStart = currentEnd + 1 - overlap;
            if (nextStart <= currentStart) nextStart = currentStart + 1;
            currentStart = nextStart;
        }

        return Ok(new { success = true, data = new { chunks, totalLines, avgCharsPerLine } });
    }

    #endregion

    #region 程序集对比

    /// <summary>
    /// 对比两个程序集的结构差异
    /// </summary>
    [HttpGet("compare")]
    public IActionResult CompareAssemblies([FromQuery] string leftMvid, [FromQuery] string rightMvid,
        [FromQuery] string? namespaceFilter = null, [FromQuery] bool includeUnchanged = false)
    {
        var leftContext = _assemblyManager.Get(leftMvid);
        var rightContext = _assemblyManager.Get(rightMvid);

        if (leftContext == null)
            return BadRequest(new { success = false, error_code = "ASSEMBLY_NOT_FOUND", message = $"Left assembly '{leftMvid}' not found" });
        if (rightContext == null)
            return BadRequest(new { success = false, error_code = "ASSEMBLY_NOT_FOUND", message = $"Right assembly '{rightMvid}' not found" });
        if (leftContext.Assembly == null || rightContext.Assembly == null)
            return BadRequest(new { success = false, error_code = "ASSEMBLY_NOT_LOADED", message = "Assembly not loaded" });

        var comparator = new DotNetMcp.Backend.Core.Modification.DiffComparator();
        var diff = comparator.CompareAssemblies(leftContext.Assembly, rightContext.Assembly);

        var summary = new { added = 0, removed = 0, modified = 0, unchanged = 0 };
        int added = 0, removed = 0, modified = 0, unchanged = 0;
        var items = new List<object>();

        foreach (var typeDiff in diff.TypeDiffs)
        {
            if (namespaceFilter != null)
            {
                var ns = typeDiff.TypeName.Contains('.') ? typeDiff.TypeName[..typeDiff.TypeName.LastIndexOf('.')] : "";
                if (!ns.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            switch (typeDiff.DiffType)
            {
                case DotNetMcp.Backend.Core.Modification.DiffType.Added: added++; break;
                case DotNetMcp.Backend.Core.Modification.DiffType.Removed: removed++; break;
                case DotNetMcp.Backend.Core.Modification.DiffType.Modified: modified++; break;
                default: unchanged++; break;
            }

            if (!includeUnchanged && typeDiff.DiffType == DotNetMcp.Backend.Core.Modification.DiffType.Unchanged)
                continue;

            items.Add(new
            {
                typeName = typeDiff.TypeName,
                diffType = typeDiff.DiffType.ToString(),
                memberDiffs = typeDiff.MemberDiffs.Select(m => new
                {
                    name = m.MemberName,
                    memberType = m.MemberType,
                    diffType = m.DiffType.ToString()
                }).ToList()
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                summary = new { added, removed, modified, unchanged },
                typeDiffs = items
            }
        });
    }

    #endregion

    #region Patch 骨架生成

    /// <summary>
    /// 生成 Harmony Patch 骨架代码
    /// </summary>
    [HttpPost("patch-skeleton")]
    public IActionResult GeneratePatchSkeleton([FromBody] PatchSkeletonRequest request, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid ?? request.Mvid);
        if (context == null)
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });

        var type = context.Assembly?.MainModule.Types.FirstOrDefault(t => t.FullName == request.TypeName);
        if (type == null)
            return BadRequest(new { success = false, error_code = "TYPE_NOT_FOUND", message = $"Type '{request.TypeName}' not found" });

        Mono.Cecil.MethodDefinition? method;
        var methodName = request.MethodName;

        if (methodName.Contains('('))
        {
            var nameOnly = methodName[..methodName.IndexOf('(')];
            var paramsPart = methodName[(methodName.IndexOf('(') + 1)..].TrimEnd(')');
            var paramTypes = paramsPart.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
            method = type.Methods.FirstOrDefault(m => m.Name == nameOnly && m.Parameters.Count == paramTypes.Length
                && m.Parameters.Select((p, i) => p.ParameterType.Name == paramTypes[i] || p.ParameterType.FullName == paramTypes[i]).All(x => x));
        }
        else
        {
            var candidates = type.Methods.Where(m => m.Name == methodName).ToList();
            if (candidates.Count > 1)
            {
                var overloads = string.Join(", ", candidates.Select(m =>
                    $"{m.Name}({string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name))})"));
                return BadRequest(new { success = false, error_code = "AMBIGUOUS_METHOD", message = $"Method '{methodName}' is overloaded. Specify parameters: {overloads}" });
            }
            method = candidates.FirstOrDefault();
        }

        if (method == null)
            return BadRequest(new { success = false, error_code = "METHOD_NOT_FOUND", message = $"Method '{methodName}' not found in type '{request.TypeName}'" });

        var generator = new PatchSkeletonGenerator();
        var patchKinds = request.PatchKinds ?? new[] { "Prefix", "Postfix" };
        var result = generator.Generate(method, patchKinds);

        if (!result.IsSuccess)
            return BadRequest(new { success = false, error_code = "GENERATION_FAILED", message = result.ErrorMessage });

        return Ok(new { success = true, data = new { code = result.Code, notes = result.Notes } });
    }

    #endregion

    #region 增强搜索

    /// <summary>
    /// 增强搜索 - 支持正则、高级语法（+/-/=/~）、Token、字面量等全能搜索
    /// </summary>
    [HttpGet("enhanced-search")]
    public IActionResult EnhancedSearch(
        [FromQuery] string query,
        [FromQuery] string mode = "auto",
        [FromQuery] string? @namespace = null,
        [FromQuery] int limit = 100,
        [FromQuery] string? mvid = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { success = false, error_code = "INVALID_QUERY", message = "query cannot be empty" });
        }

        if (limit < 1 || limit > 1000)
        {
            limit = Math.Clamp(limit, 1, 1000);
        }

        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded. Use assembly/load to load one first." });
        }

        var module = context.Assembly?.MainModule;
        if (module == null)
        {
            return BadRequest(new { success = false, error_code = "MODULE_NOT_LOADED", message = "Assembly module is not available." });
        }

        var parsedMode = (string.IsNullOrEmpty(mode) || string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase))
            ? SearchMode.TypeAndMember
            : Enum.TryParse<SearchMode>(mode, true, out var m) ? m : SearchMode.TypeAndMember;

        var service = new EnhancedSearchService(context.Mvid);
        var result = service.Search(module, query, parsedMode, @namespace, limit);

        return Ok(result);
    }

    #endregion

    #region 继承分析

    /// <summary>
    /// 查找类型的所有基类链（含接口）
    /// </summary>
    [HttpGet("inheritance/base-types/{typeName}")]
    public IActionResult FindBaseTypes(string typeName, [FromQuery] bool includeInterfaces = true, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });

        var result = _analysisService.FindBaseTypes(context, Uri.UnescapeDataString(typeName), includeInterfaces);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error_code = "INHERITANCE_FAILED", message = result.ErrorMessage });

        return Ok(new { success = true, data = new { types = result.Types, total_count = result.TotalCount } });
    }

    /// <summary>
    /// 查找继承自指定类型的所有派生类型
    /// </summary>
    [HttpGet("inheritance/derived-types/{typeName}")]
    public IActionResult FindDerivedTypes(string typeName, [FromQuery] bool directOnly = false, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });

        var result = _analysisService.FindDerivedTypes(context, Uri.UnescapeDataString(typeName), directOnly);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error_code = "INHERITANCE_FAILED", message = result.ErrorMessage });

        return Ok(new { success = true, data = new { types = result.Types, total_count = result.TotalCount } });
    }

    /// <summary>
    /// 查找接口的所有实现
    /// </summary>
    [HttpGet("inheritance/implementations/{interfaceTypeName}")]
    public IActionResult GetImplementations(string interfaceTypeName, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });

        var result = _analysisService.GetImplementations(context, Uri.UnescapeDataString(interfaceTypeName));
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error_code = "INHERITANCE_FAILED", message = result.ErrorMessage });

        return Ok(new { success = true, data = new { types = result.Types, total_count = result.TotalCount } });
    }

    /// <summary>
    /// 查找方法的所有覆盖
    /// </summary>
    [HttpGet("inheritance/overrides/{typeName}/{methodName}")]
    public IActionResult GetOverrides(string typeName, string methodName, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });

        var result = _analysisService.GetOverrides(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName));
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error_code = "INHERITANCE_FAILED", message = result.ErrorMessage });

        return Ok(new { success = true, data = new { methods = result.Methods, total_count = result.TotalCount } });
    }

    /// <summary>
    /// 查找方法的所有重载
    /// </summary>
    [HttpGet("inheritance/overloads/{typeName}/{methodName}")]
    public IActionResult GetOverloads(string typeName, string methodName, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });

        var result = _analysisService.GetOverloads(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName));
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error_code = "INHERITANCE_FAILED", message = result.ErrorMessage });

        return Ok(new { success = true, data = new { methods = result.Methods, total_count = result.TotalCount } });
    }

    #endregion

    #region 批量反编译

    /// <summary>
    /// 批量反编译 - 一次请求反编译多个成员，带字符预算控制
    /// </summary>
    [HttpPost("batch-decompile")]
    public IActionResult BatchDecompile([FromBody] BatchDecompileRequest request, [FromQuery] string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid ?? request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (request.MemberKeys == null || request.MemberKeys.Count == 0)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "memberKeys cannot be empty" });
        }

        var items = new List<object>();
        var totalChars = 0;
        var truncated = false;
        var maxTotalChars = request.MaxTotalChars ?? 200000;

        foreach (var key in request.MemberKeys)
        {
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
            items.Add(new
            {
                memberKey = key,
                code,
                totalLines = code.Split('\n').Length,
                isError = !result.IsSuccess
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                items,
                truncated,
                totalCharsReturned = totalChars,
                processed = items.Count,
                requested = request.MemberKeys.Count
            }
        });
    }

    #endregion

    #region 索引预热

    /// <summary>
    /// 预热类型和成员索引
    /// </summary>
    [HttpPost("warm-index")]
    public IActionResult WarmIndex([FromQuery] string? mvid = null, [FromQuery] bool typeIndex = true, [FromQuery] bool memberIndex = true, [FromQuery] int? maxSeconds = null)
    {
        var context = _assemblyManager.Get(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded. Use assembly/load to load one first." });
        }

        var result = _analysisService.WarmIndex(context, typeIndex, memberIndex, maxSeconds);
        return Ok(new
        {
            success = result.IsSuccess,
            data = result.IsSuccess ? (object)new
            {
                type_index_built = result.TypeIndexBuilt,
                member_index_built = result.MemberIndexBuilt,
                type_count = result.TypeCount,
                member_count = result.MemberCount,
                elapsed_ms = result.ElapsedMs,
                max_seconds_exceeded = result.MaxSecondsExceeded
            } : null,
            message = result.IsSuccess ? null : result.ErrorMessage
        });
    }

    #endregion
}

#region Request Models

public class BatchSourcesRequest
{
    public List<string>? TypeNames { get; set; }
    public string? Language { get; set; }
    public string? Mvid { get; set; }
}

public class BatchMethodsRequest
{
    public List<MethodIdentifier>? Methods { get; set; }
    public string? Language { get; set; }
    public string? Mvid { get; set; }
}

public class MethodIdentifier
{
    public required string TypeName { get; set; }
    public required string MethodName { get; set; }
}

public class BatchXRefsRequest
{
    public List<string>? TypeNames { get; set; }
    public int? Limit { get; set; }
    public string? Mvid { get; set; }
}

public class BatchDecompileRequest
{
    public List<string>? MemberKeys { get; set; }
    public int? MaxTotalChars { get; set; }
    public string? Mvid { get; set; }
}

public class PatchSkeletonRequest
{
    public required string TypeName { get; set; }
    public required string MethodName { get; set; }
    public string[]? PatchKinds { get; set; }
    public string? Mvid { get; set; }
}

#endregion
