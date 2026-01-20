using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;
using System.IO.Compression;
using System.Text;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 批量导出器 - 批量下载类型和方法源码
/// </summary>
public class BatchExporter
{
    private readonly AnalysisService _analysisService;

    public BatchExporter(AnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    /// <summary>
    /// 批量导出类型源码到 ZIP
    /// </summary>
    public byte[] ExportTypesToZip(AssemblyContext context, IEnumerable<string> typeNames, string language = "csharp")
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var typeName in typeNames)
            {
                var result = _analysisService.DecompileType(context, typeName, language);
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Code))
                {
                    var fileName = SanitizeFileName(typeName) + GetFileExtension(language);
                    var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                    
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                    writer.Write(result.Code);
                }
            }
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// 批量导出方法源码到 ZIP
    /// </summary>
    public byte[] ExportMethodsToZip(AssemblyContext context, IEnumerable<MethodRequest> methods, string language = "csharp")
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var method in methods)
            {
                var result = _analysisService.DecompileMethod(context, method.TypeName, method.MethodName, language);
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Code))
                {
                    var fileName = SanitizeFileName($"{method.TypeName}.{method.MethodName}") + GetFileExtension(language);
                    var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                    
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                    writer.Write(result.Code);
                }
            }
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// 导出完整命名空间到 ZIP
    /// </summary>
    public byte[] ExportNamespaceToZip(AssemblyContext context, string namespacePrefix, string language = "csharp")
    {
        var types = context.Assembly!.MainModule.Types
            .Where(t => t.FullName.StartsWith(namespacePrefix))
            .Select(t => t.FullName)
            .ToList();

        return ExportTypesToZip(context, types, language);
    }

    /// <summary>
    /// 导出分析报告到 ZIP (包含依赖图、CFG、模式检测等)
    /// </summary>
    public byte[] ExportAnalysisReportToZip(
        AssemblyContext context, 
        string typeName, 
        bool includeDependencies = true,
        bool includePatterns = true,
        bool includeObfuscation = true)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // 1. 源码
            var sourceResult = _analysisService.DecompileType(context, typeName, "csharp");
            if (sourceResult.IsSuccess)
            {
                AddTextEntry(archive, $"{SanitizeFileName(typeName)}.cs", sourceResult.Code);
            }

            // 2. 依赖图
            if (includeDependencies)
            {
                var depBuilder = new DependencyGraphBuilder();
                var depGraph = depBuilder.BuildTypeDependencies(context, typeName);
                var mermaid = depBuilder.GenerateTypeMermaid(depGraph);
                AddTextEntry(archive, $"{SanitizeFileName(typeName)}_dependencies.mmd", mermaid);
            }

            // 3. 设计模式
            if (includePatterns)
            {
                var patternDetector = new PatternDetector();
                var patterns = patternDetector.DetectPatterns(context);
                var typePatterns = patterns.Patterns.Where(p => p.TypeName == typeName).ToList();
                if (typePatterns.Any())
                {
                    var report = GeneratePatternsReport(typePatterns);
                    AddTextEntry(archive, $"{SanitizeFileName(typeName)}_patterns.md", report);
                }
            }

            // 4. 混淆检测
            if (includeObfuscation)
            {
                var obfDetector = new ObfuscationDetector();
                var obfResult = obfDetector.DetectObfuscation(context);
                var typeObf = new
                {
                    Types = obfResult.ObfuscatedTypes.Where(i => i.Name == typeName),
                    Methods = obfResult.ObfuscatedMethods.Where(i => i.Name.StartsWith(typeName + ".")),
                    Fields = obfResult.ObfuscatedFields.Where(i => i.Name.StartsWith(typeName + "."))
                };
                
                if (typeObf.Types.Any() || typeObf.Methods.Any() || typeObf.Fields.Any())
                {
                    var report = GenerateObfuscationReport(typeName, typeObf);
                    AddTextEntry(archive, $"{SanitizeFileName(typeName)}_obfuscation.md", report);
                }
            }
        }

        return memoryStream.ToArray();
    }

    private void AddTextEntry(ZipArchive archive, string fileName, string? content)
    {
        if (string.IsNullOrEmpty(content)) return;

        var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        writer.Write(content);
    }

    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Replace("<", "_").Replace(">", "_");
    }

    private string GetFileExtension(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "csharp" => ".cs",
            "il" => ".il",
            "vb" => ".vb",
            _ => ".txt"
        };
    }

    private string GeneratePatternsReport(List<DetectedPattern> patterns)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Design Patterns Detected");
        sb.AppendLine();
        
        foreach (var pattern in patterns)
        {
            sb.AppendLine($"## {pattern.PatternType}");
            sb.AppendLine($"**Confidence**: {pattern.Confidence:P0}");
            sb.AppendLine();
            sb.AppendLine("**Evidence**:");
            foreach (var evidence in pattern.Evidence)
            {
                sb.AppendLine($"- {evidence}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GenerateObfuscationReport(string typeName, dynamic obfData)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Obfuscation Analysis: {typeName}");
        sb.AppendLine();

        if (obfData.Types.Any())
        {
            sb.AppendLine("## Type Name Obfuscation");
            foreach (var item in obfData.Types)
            {
                sb.AppendLine($"- **{item.Severity}**: {item.Evidence}");
            }
            sb.AppendLine();
        }

        if (obfData.Methods.Any())
        {
            sb.AppendLine("## Method Name Obfuscation");
            foreach (var item in obfData.Methods)
            {
                sb.AppendLine($"- **{item.Severity}**: {item.Evidence}");
            }
            sb.AppendLine();
        }

        if (obfData.Fields.Any())
        {
            sb.AppendLine("## Field Name Obfuscation");
            foreach (var item in obfData.Fields)
            {
                sb.AppendLine($"- **{item.Severity}**: {item.Evidence}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public record MethodRequest
{
    public string TypeName { get; set; } = "";
    public string MethodName { get; set; } = "";
}
