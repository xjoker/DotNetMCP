using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 分析 API 控制器 - 提供反编译、搜索、交叉引用、调用图等功能
/// </summary>
[ApiController]
[Route("analysis")]
public class AnalysisController : ControllerBase
{
    private readonly AnalysisService _analysisService;
    private readonly IInstanceRegistry _registry;

    public AnalysisController(AnalysisService analysisService, IInstanceRegistry registry)
    {
        _analysisService = analysisService;
        _registry = registry;
    }

    private AssemblyContext? GetContext(string? mvid = null) => _registry.Get(mvid);

    #region 反编译

    /// <summary>
    /// 获取类型源码
    /// </summary>
    [HttpGet("type/{typeName}/source")]
    public IActionResult GetTypeSource(string typeName, [FromQuery] string language = "csharp", [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
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
                language = language,
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
        var context = GetContext(mvid);
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
        var context = GetContext(mvid);
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
        // 验证 limit 参数
        if (limit < 0)
        {
            return BadRequest(new { success = false, error_code = "INVALID_LIMIT", message = "Limit must be >= 0" });
        }
        limit = Math.Min(limit, 500); // 最大限制

        var context = GetContext(mvid);
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
        var context = GetContext(mvid);
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
        var context = GetContext(mvid);
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
        var context = GetContext(mvid);
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
        // 验证 direction 参数
        var validDirections = new[] { "callees", "callers", "both" };
        if (!validDirections.Contains(direction.ToLowerInvariant()))
        {
            return BadRequest(new { success = false, error_code = "INVALID_DIRECTION", message = $"Direction must be one of: {string.Join(", ", validDirections)}" });
        }

        // 验证数值参数
        if (max_depth < 0 || max_nodes < 0)
        {
            return BadRequest(new { success = false, error_code = "INVALID_PARAMETER", message = "max_depth and max_nodes must be >= 0" });
        }

        var context = GetContext(mvid);
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

    #region 依赖图

    /// <summary>
    /// 获取程序集依赖图
    /// </summary>
    [HttpGet("dependencies/assembly")]
    public IActionResult GetAssemblyDependencies([FromQuery] bool includeSystem = false, [FromQuery] string format = "json", [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var builder = new DotNetMcp.Backend.Core.Analysis.DependencyGraphBuilder();
        var graph = builder.BuildAssemblyDependencies(context);

        if (format.ToLowerInvariant() == "mermaid")
        {
            var mermaid = builder.GenerateAssemblyMermaid(graph, includeSystem);
            return Ok(new
            {
                success = true,
                data = new
                {
                    format = "mermaid",
                    diagram = mermaid,
                    root_assembly = graph.RootAssembly,
                    dependency_count = graph.Dependencies.Count
                }
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                root_assembly = graph.RootAssembly,
                version = graph.Version,
                dependencies = graph.Dependencies.Select(d => new
                {
                    name = d.Name,
                    version = d.Version,
                    is_system = d.IsSystem
                }),
                native_modules = graph.NativeModules,
                total_count = graph.Dependencies.Count
            }
        });
    }

    /// <summary>
    /// 获取类型依赖图
    /// </summary>
    [HttpGet("dependencies/type/{typeName}")]
    public IActionResult GetTypeDependencies(string typeName, [FromQuery] int maxDepth = 3, [FromQuery] string format = "json", [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        maxDepth = Math.Clamp(maxDepth, 1, 5);

        var builder = new DotNetMcp.Backend.Core.Analysis.DependencyGraphBuilder();
        var graph = builder.BuildTypeDependencies(context, Uri.UnescapeDataString(typeName), maxDepth);

        if (!string.IsNullOrEmpty(graph.Error))
        {
            return BadRequest(new { success = false, error_code = "TYPE_NOT_FOUND", message = graph.Error });
        }

        if (format.ToLowerInvariant() == "mermaid")
        {
            var mermaid = builder.GenerateTypeMermaid(graph);
            return Ok(new
            {
                success = true,
                data = new
                {
                    format = "mermaid",
                    diagram = mermaid,
                    root_type = graph.RootType,
                    dependency_count = graph.Dependencies.Count
                }
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                root_type = graph.RootType,
                @namespace = graph.Namespace,
                dependencies = graph.Dependencies.Select(d => new
                {
                    target_type = d.TargetType,
                    kind = d.Kind.ToString(),
                    is_external = d.IsExternal
                }),
                total_count = graph.Dependencies.Count
            }
        });
    }

    #endregion

    #region 控制流图

    /// <summary>
    /// 构建方法的控制流图
    /// </summary>
    [HttpGet("cfg/{typeName}/{methodName}")]
    public IActionResult BuildControlFlowGraph(string typeName, string methodName, [FromQuery] string format = "json", [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var builder = new DotNetMcp.Backend.Core.Analysis.ControlFlowGraphBuilder();
        var cfg = builder.BuildCFG(context, Uri.UnescapeDataString(typeName), Uri.UnescapeDataString(methodName));

        if (!string.IsNullOrEmpty(cfg.Error))
        {
            return BadRequest(new { success = false, error_code = "CFG_BUILD_FAILED", message = cfg.Error });
        }

        if (format.ToLowerInvariant() == "mermaid")
        {
            var mermaid = builder.GenerateMermaid(cfg);
            return Ok(new
            {
                success = true,
                data = new
                {
                    format = "mermaid",
                    diagram = mermaid,
                    method_name = cfg.MethodName,
                    basic_block_count = cfg.BasicBlocks.Count,
                    edge_count = cfg.Edges.Count
                }
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                method_name = cfg.MethodName,
                return_type = cfg.ReturnType,
                basic_blocks = cfg.BasicBlocks.Select(b => new
                {
                    id = b.Id,
                    start_offset = b.StartOffset,
                    end_offset = b.EndOffset,
                    terminator_type = b.TerminatorType.ToString(),
                    instruction_count = b.Instructions.Count,
                    instructions = b.Instructions.Select(i => new
                    {
                        offset = i.Offset,
                        opcode = i.OpCode,
                        operand = i.Operand
                    })
                }),
                edges = cfg.Edges.Select(e => new
                {
                    from_block = e.FromBlock,
                    to_block = e.ToBlock,
                    edge_type = e.EdgeType.ToString(),
                    label = e.Label
                }),
                statistics = new
                {
                    basic_block_count = cfg.BasicBlocks.Count,
                    edge_count = cfg.Edges.Count,
                    total_instructions = cfg.BasicBlocks.Sum(b => b.Instructions.Count)
                }
            }
        });
    }

    #endregion

    #region 设计模式检测

    /// <summary>
    /// 检测程序集中的设计模式
    /// </summary>
    [HttpGet("patterns")]
    public IActionResult DetectPatterns([FromQuery] double minConfidence = 0.5, [FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        minConfidence = Math.Clamp(minConfidence, 0.0, 1.0);

        var detector = new DotNetMcp.Backend.Core.Analysis.PatternDetector();
        var result = detector.DetectPatterns(context);

        // 按置信度过滤
        var filteredPatterns = result.Patterns
            .Where(p => p.Confidence >= minConfidence)
            .OrderByDescending(p => p.Confidence)
            .ToList();

        // 按模式类型分组统计
        var patternStats = filteredPatterns
            .GroupBy(p => p.PatternType)
            .Select(g => new
            {
                pattern = g.Key.ToString(),
                count = g.Count(),
                avg_confidence = g.Average(p => p.Confidence)
            })
            .OrderByDescending(s => s.count)
            .ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                patterns = filteredPatterns.Select(p => new
                {
                    pattern_type = p.PatternType.ToString(),
                    type_name = p.TypeName,
                    confidence = Math.Round(p.Confidence, 2),
                    evidence = p.Evidence
                }),
                statistics = new
                {
                    total_detected = filteredPatterns.Count,
                    by_pattern = patternStats,
                    min_confidence = minConfidence
                }
            }
        });
    }

    #endregion

    #region 混淆检测

    /// <summary>
    /// 检测程序集中的代码混淆
    /// </summary>
    [HttpGet("obfuscation")]
    public IActionResult DetectObfuscation([FromQuery] string? mvid = null)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        var detector = new DotNetMcp.Backend.Core.Analysis.ObfuscationDetector();
        var result = detector.DetectObfuscation(context);

        return Ok(new
        {
            success = true,
            data = new
            {
                is_obfuscated = result.IsObfuscated,
                obfuscation_score = Math.Round(result.ObfuscationScore, 2),
                obfuscated_types = result.ObfuscatedTypes.Select(i => new
                {
                    name = i.Name,
                    severity = i.Severity.ToString(),
                    evidence = i.Evidence
                }),
                obfuscated_methods = result.ObfuscatedMethods.Select(i => new
                {
                    name = i.Name,
                    severity = i.Severity.ToString(),
                    evidence = i.Evidence
                }),
                obfuscated_fields = result.ObfuscatedFields.Select(i => new
                {
                    name = i.Name,
                    severity = i.Severity.ToString(),
                    evidence = i.Evidence
                }),
                control_flow_obfuscations = result.ControlFlowObfuscations.Select(i => new
                {
                    method = i.Name,
                    severity = i.Severity.ToString(),
                    evidence = i.Evidence
                }),
                string_obfuscations = result.StringObfuscations.Select(i => new
                {
                    method = i.Name,
                    severity = i.Severity.ToString(),
                    evidence = i.Evidence
                }),
                junk_code_patterns = result.JunkCodePatterns.Select(i => new
                {
                    method = i.Name,
                    severity = i.Severity.ToString(),
                    evidence = i.Evidence
                }),
                metadata_tampering_patterns = result.MetadataTamperingPatterns.Select(i => new
                {
                    name = i.Name,
                    severity = i.Severity.ToString(),
                    evidence = i.Evidence
                }),
                anti_analysis_patterns = result.AntiAnalysisPatterns.Select(i => new
                {
                    method = i.Name,
                    severity = i.Severity.ToString(),
                    evidence = i.Evidence
                }),
                statistics = new
                {
                    total_obfuscated_types = result.ObfuscatedTypes.Count,
                    total_obfuscated_methods = result.ObfuscatedMethods.Count,
                    total_obfuscated_fields = result.ObfuscatedFields.Count,
                    control_flow_count = result.ControlFlowObfuscations.Count,
                    string_encryption_count = result.StringObfuscations.Count,
                    junk_code_count = result.JunkCodePatterns.Count,
                    metadata_tampering_count = result.MetadataTamperingPatterns.Count,
                    anti_analysis_count = result.AntiAnalysisPatterns.Count
                }
            }
        });
    }

    #endregion

    #region 批量下载

    /// <summary>
    /// 批量下载类型源码 (ZIP)
    /// </summary>
    [HttpPost("export/types")]
    public IActionResult ExportTypes([FromBody] ExportTypesRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (request.TypeNames == null || request.TypeNames.Count == 0 || request.TypeNames.Count > 100)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Must provide 1-100 type names" });
        }

        var exporter = new DotNetMcp.Backend.Core.Analysis.BatchExporter(_analysisService);
        var zipBytes = exporter.ExportTypesToZip(context, request.TypeNames, request.Language ?? "csharp");

        return File(zipBytes, "application/zip", $"types_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
    }

    /// <summary>
    /// 批量下载方法源码 (ZIP)
    /// </summary>
    [HttpPost("export/methods")]
    public IActionResult ExportMethods([FromBody] ExportMethodsRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (request.Methods == null || request.Methods.Count == 0 || request.Methods.Count > 100)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Must provide 1-100 methods" });
        }

        var exporter = new DotNetMcp.Backend.Core.Analysis.BatchExporter(_analysisService);
        var methodRequests = request.Methods.Select(m => new DotNetMcp.Backend.Core.Analysis.MethodRequest
        {
            TypeName = m.TypeName,
            MethodName = m.MethodName
        }).ToList();

        var zipBytes = exporter.ExportMethodsToZip(context, methodRequests, request.Language ?? "csharp");

        return File(zipBytes, "application/zip", $"methods_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
    }

    /// <summary>
    /// 下载完整命名空间源码 (ZIP)
    /// </summary>
    [HttpPost("export/namespace")]
    public IActionResult ExportNamespace([FromBody] ExportNamespaceRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (string.IsNullOrWhiteSpace(request.NamespacePrefix))
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Namespace prefix required" });
        }

        var exporter = new DotNetMcp.Backend.Core.Analysis.BatchExporter(_analysisService);
        var zipBytes = exporter.ExportNamespaceToZip(context, request.NamespacePrefix, request.Language ?? "csharp");

        var safeName = request.NamespacePrefix.Replace(".", "_");
        return File(zipBytes, "application/zip", $"namespace_{safeName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
    }

    /// <summary>
    /// 下载类型的完整分析报告 (ZIP)
    /// </summary>
    [HttpPost("export/analysis-report")]
    public IActionResult ExportAnalysisReport([FromBody] ExportAnalysisReportRequest request)
    {
        var context = GetContext(request.Mvid);
        if (context == null)
        {
            return BadRequest(new { success = false, error_code = "NO_ASSEMBLY_LOADED", message = "No assembly loaded" });
        }

        if (string.IsNullOrWhiteSpace(request.TypeName))
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Type name required" });
        }

        var exporter = new DotNetMcp.Backend.Core.Analysis.BatchExporter(_analysisService);
        var zipBytes = exporter.ExportAnalysisReportToZip(
            context,
            request.TypeName,
            request.IncludeDependencies ?? true,
            request.IncludePatterns ?? true,
            request.IncludeObfuscation ?? true
        );

        var safeName = request.TypeName.Replace(".", "_");
        return File(zipBytes, "application/zip", $"analysis_{safeName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
    }

    #endregion

    #region 批量操作

    /// <summary>
    /// 批量获取类型源码
    /// </summary>
    [HttpPost("batch/sources")]
    public IActionResult BatchGetTypeSources([FromBody] BatchSourcesRequest request)
    {
        var context = GetContext(request.Mvid);
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
        var context = GetContext(request.Mvid);
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
        var context = GetContext(request.Mvid);
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

    #region 增强分析 API

    /// <summary>
    /// 增强搜索 - 支持高级语法、正则、字面量搜索
    /// </summary>
    [HttpPost("enhanced-search")]
    public IActionResult EnhancedSearch(
        [FromBody] EnhancedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var context = GetContext(request.InstanceId);
        if (context == null)
        {
            return BadRequest(new { success = false, error = "Instance not found or not loaded" });
        }

        var result = _analysisService.EnhancedSearch(
            context,
            request.Query,
            request.Mode ?? "all",
            request.NamespaceFilter,
            request.Limit ?? 100,
            cancellationToken);

        return Ok(new
        {
            success = result.IsSuccess,
            items = result.Items,
            totalCount = result.TotalCount,
            hasMore = result.HasMore,
            searchDurationMs = result.SearchDuration.TotalMilliseconds,
            query = result.Query,
            mode = result.Mode,
            error = result.ErrorMessage
        });
    }

    /// <summary>
    /// 构建增强调用图 - 包含委托、反射、Lambda 分析
    /// </summary>
    [HttpPost("enhanced-callgraph")]
    public IActionResult BuildEnhancedCallGraph(
        [FromBody] InstanceRequest request,
        CancellationToken cancellationToken)
    {
        var context = GetContext(request.InstanceId);
        if (context == null)
        {
            return BadRequest(new { success = false, error = "Instance not found or not loaded" });
        }

        var result = _analysisService.BuildEnhancedCallGraph(context, cancellationToken);

        return Ok(new
        {
            success = result.IsSuccess,
            statistics = result.Statistics,
            reflectionCalls = result.ReflectionCalls?.Select(r => new
            {
                r.CallerMethod,
                r.ReflectionPattern,
                r.TargetTypeName,
                r.TargetMemberName,
                r.IsResolved
            }),
            nodeCount = result.NodeCount,
            edgeCount = result.EdgeCount,
            error = result.ErrorMessage
        });
    }

    /// <summary>
    /// 检测递归调用
    /// </summary>
    [HttpPost("detect-recursion")]
    public IActionResult DetectRecursion(
        [FromBody] InstanceRequest request,
        CancellationToken cancellationToken)
    {
        var context = GetContext(request.InstanceId);
        if (context == null)
        {
            return BadRequest(new { success = false, error = "Instance not found or not loaded" });
        }

        var result = _analysisService.DetectRecursion(context, cancellationToken);

        return Ok(new
        {
            success = result.IsSuccess,
            recursions = result.Recursions?.Select(r => new
            {
                r.MethodName,
                type = r.RecursionType.ToString(),
                r.Depth,
                r.Cycle
            }),
            totalCount = result.TotalCount,
            error = result.ErrorMessage
        });
    }

    /// <summary>
    /// 获取方法的支配树分析
    /// </summary>
    [HttpPost("dominators")]
    public IActionResult AnalyzeDominators([FromBody] MethodAnalysisRequest request)
    {
        var context = GetContext(request.InstanceId);
        if (context == null)
        {
            return BadRequest(new { success = false, error = "Instance not found or not loaded" });
        }

        var result = _analysisService.AnalyzeDominators(context, request.TypeName, request.MethodName);

        return Ok(new
        {
            success = result.IsSuccess,
            methodName = result.MethodName,
            immediateDominators = result.ImmediateDominators,
            dominanceFrontier = result.DominanceFrontier,
            controlDependence = result.ControlDependence,
            blockCount = result.BlockCount,
            error = result.ErrorMessage
        });
    }

    /// <summary>
    /// 获取方法的数据流分析
    /// </summary>
    [HttpPost("dataflow")]
    public IActionResult AnalyzeDataFlow([FromBody] MethodAnalysisRequest request)
    {
        var context = GetContext(request.InstanceId);
        if (context == null)
        {
            return BadRequest(new { success = false, error = "Instance not found or not loaded" });
        }

        var result = _analysisService.AnalyzeDataFlow(context, request.TypeName, request.MethodName);

        return Ok(new
        {
            success = result.IsSuccess,
            methodName = result.MethodName,
            liveIn = result.LiveIn,
            liveOut = result.LiveOut,
            definitionCount = result.DefinitionCount,
            blockCount = result.BlockCount,
            error = result.ErrorMessage
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

public class ExportTypesRequest
{
    public List<string>? TypeNames { get; set; }
    public string? Language { get; set; }
    public string? Mvid { get; set; }
}

public class ExportMethodsRequest
{
    public List<MethodIdentifier>? Methods { get; set; }
    public string? Language { get; set; }
    public string? Mvid { get; set; }
}

public class ExportNamespaceRequest
{
    public string NamespacePrefix { get; set; } = "";
    public string? Language { get; set; }
    public string? Mvid { get; set; }
}

public class ExportAnalysisReportRequest
{
    public string TypeName { get; set; } = "";
    public bool? IncludeDependencies { get; set; }
    public bool? IncludePatterns { get; set; }
    public bool? IncludeObfuscation { get; set; }
    public string? Mvid { get; set; }
}

// 增强搜索请求
public class EnhancedSearchRequest
{
    public required string Query { get; set; }
    public string? Mode { get; set; }  // all, type, member, method, field, property, event, literal, token
    public string? NamespaceFilter { get; set; }
    public int? Limit { get; set; }
    public string? InstanceId { get; set; }
}

// 实例请求
public class InstanceRequest
{
    public string? InstanceId { get; set; }
}

// 方法分析请求
public class MethodAnalysisRequest
{
    public required string TypeName { get; set; }
    public required string MethodName { get; set; }
    public string? InstanceId { get; set; }
}

#endregion
