using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

namespace DotNetMcp.Backend.Core.Compilation;

/// <summary>
/// 编译服务 - 使用 Roslyn 编译 C# 代码
/// </summary>
public class CompilationService
{
    private readonly ReferenceAssemblyProvider _referenceProvider;

    public CompilationService(ReferenceAssemblyProvider referenceProvider)
    {
        _referenceProvider = referenceProvider ?? throw new ArgumentNullException(nameof(referenceProvider));
    }

    /// <summary>
    /// 编译 C# 源码到程序集
    /// </summary>
    /// <param name="sourceCode">C# 源代码</param>
    /// <param name="assemblyName">程序集名称</param>
    /// <param name="options">编译选项</param>
    /// <returns>编译结果</returns>
    public CompilationResult Compile(
        string sourceCode,
        string assemblyName,
        CompilationOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return CompilationResult.Failure(
                CompilationErrorCode.EmptySource,
                "Source code cannot be empty"
            );
        }

        try
        {
            // 解析语法树
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            // 获取引用程序集
            var references = _referenceProvider.GetReferences(options?.TargetFramework);

            // 创建编译
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: options?.OptimizationLevel ?? OptimizationLevel.Debug,
                    allowUnsafe: options?.AllowUnsafe ?? false
                )
            );

            // 编译到内存流
            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => new CompilationDiagnostic
                    {
                        Id = d.Id,
                        Message = d.GetMessage(),
                        Location = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                        Severity = d.Severity.ToString()
                    })
                    .ToList();

                return CompilationResult.Failure(
                    CompilationErrorCode.CompilationError,
                    $"Compilation failed with {errors.Count} error(s)",
                    errors
                );
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assemblyBytes = ms.ToArray();

            return CompilationResult.Success(assemblyBytes, assemblyName);
        }
        catch (Exception ex)
        {
            return CompilationResult.Failure(
                CompilationErrorCode.InternalError,
                $"Compilation exception: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 验证源代码语法（不编译）
    /// </summary>
    public SyntaxValidationResult ValidateSyntax(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return SyntaxValidationResult.Invalid("Source code cannot be empty");
        }

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var diagnostics = syntaxTree.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => new CompilationDiagnostic
                {
                    Id = d.Id,
                    Message = d.GetMessage(),
                    Location = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Severity = d.Severity.ToString()
                })
                .ToList();

            return diagnostics.Count > 0
                ? SyntaxValidationResult.Invalid("Syntax errors found", diagnostics)
                : SyntaxValidationResult.Valid();
        }
        catch (Exception ex)
        {
            return SyntaxValidationResult.Invalid($"Validation exception: {ex.Message}");
        }
    }
}

/// <summary>
/// 编译选项
/// </summary>
public record CompilationOptions
{
    public string? TargetFramework { get; init; }
    public OptimizationLevel OptimizationLevel { get; init; } = OptimizationLevel.Debug;
    public bool AllowUnsafe { get; init; }
}

/// <summary>
/// 编译结果
/// </summary>
public record CompilationResult
{
    public bool IsSuccess { get; init; }
    public byte[]? AssemblyBytes { get; init; }
    public string? AssemblyName { get; init; }
    public CompilationErrorCode? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<CompilationDiagnostic>? Diagnostics { get; init; }

    public static CompilationResult Success(byte[] assemblyBytes, string assemblyName)
        => new()
        {
            IsSuccess = true,
            AssemblyBytes = assemblyBytes,
            AssemblyName = assemblyName
        };

    public static CompilationResult Failure(
        CompilationErrorCode errorCode,
        string message,
        IReadOnlyList<CompilationDiagnostic>? diagnostics = null)
        => new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
            Diagnostics = diagnostics
        };
}

/// <summary>
/// 语法验证结果
/// </summary>
public record SyntaxValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<CompilationDiagnostic>? Diagnostics { get; init; }

    public static SyntaxValidationResult Valid()
        => new() { IsValid = true };

    public static SyntaxValidationResult Invalid(
        string message,
        IReadOnlyList<CompilationDiagnostic>? diagnostics = null)
        => new()
        {
            IsValid = false,
            ErrorMessage = message,
            Diagnostics = diagnostics
        };
}

/// <summary>
/// 编译诊断信息
/// </summary>
public record CompilationDiagnostic
{
    public required string Id { get; init; }
    public required string Message { get; init; }
    public required int Location { get; init; }
    public required string Severity { get; init; }
}

/// <summary>
/// 编译错误码
/// </summary>
public enum CompilationErrorCode
{
    EmptySource = 2001,
    CompilationError = 2002,
    InternalError = 2003
}
