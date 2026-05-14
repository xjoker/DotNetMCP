using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using Mono.Cecil;
using DotNetMcp.Backend.Core.Context;
using SrmEntityHandle = System.Reflection.Metadata.EntityHandle;
using SrmMethodDefinitionHandle = System.Reflection.Metadata.MethodDefinitionHandle;
using SrmMetadataTokens = System.Reflection.Metadata.Ecma335.MetadataTokens;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 反编译服务 - 使用 ILSpy 引擎反编译 IL 到 C#
/// </summary>
public class DecompilerService : IDisposable
{
    private readonly AssemblyContext _context;
    private CSharpDecompiler? _decompiler;
    private readonly object _lock = new();
    private bool _disposed;

    public DecompilerService(AssemblyContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// 反编译整个类型
    /// </summary>
    public DecompileResult DecompileType(string typeName)
    {
        try
        {
            var decompiler = GetOrCreateDecompiler();
            var fullTypeName = new FullTypeName(typeName);
            var code = decompiler.DecompileTypeAsString(fullTypeName);

            return DecompileResult.Success(code, typeName);
        }
        catch (Exception ex)
        {
            return DecompileResult.Failure($"Failed to decompile type: {ex.Message}");
        }
    }

    /// <summary>
    /// 反编译单个方法
    /// </summary>
    public DecompileResult DecompileMethod(TypeDefinition type, MethodDefinition method)
    {
        try
        {
            var decompiler = GetOrCreateDecompiler();

            // 从 Mono.Cecil MetadataToken 提取行号（低 24 位），转换为 MethodDefinitionHandle
            int rid = method.MetadataToken.ToInt32() & 0x00FFFFFF;
            SrmMethodDefinitionHandle methodHandle = SrmMetadataTokens.MethodDefinitionHandle(rid);
            SrmEntityHandle entityHandle = methodHandle;

            var code = decompiler.DecompileAsString(new[] { entityHandle });
            return DecompileResult.Success(code, $"{type.FullName}.{method.Name}");
        }
        catch (Exception ex)
        {
            return DecompileResult.Failure($"Failed to decompile method: {ex.Message}");
        }
    }

    /// <summary>
    /// 反编译整个程序集
    /// </summary>
    public DecompileResult DecompileAssembly()
    {
        try
        {
            var decompiler = GetOrCreateDecompiler();
            var code = decompiler.DecompileWholeModuleAsString();
            return DecompileResult.Success(code, _context.Assembly.Name.Name);
        }
        catch (Exception ex)
        {
            return DecompileResult.Failure($"Failed to decompile assembly: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取指定类型的 IL 代码
    /// </summary>
    public string GetIL(TypeDefinition type)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($".class {GetTypeAttributes(type)} {type.FullName}");
        sb.AppendLine("{");

        foreach (var method in type.Methods)
        {
            sb.AppendLine($"  .method {GetMethodAttributes(method)} {method.ReturnType.Name} {method.Name}()");
            sb.AppendLine("  {");
            if (method.HasBody)
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    sb.AppendLine($"    IL_{instruction.Offset:X4}: {instruction.OpCode} {instruction.Operand}");
                }
            }
            sb.AppendLine("  }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private CSharpDecompiler GetOrCreateDecompiler()
    {
        lock (_lock)
        {
            if (_decompiler == null)
            {
                var settings = new DecompilerSettings
                {
                    ThrowOnAssemblyResolveErrors = false,
                    ShowXmlDocumentation = false
                };
                _decompiler = new CSharpDecompiler(_context.AssemblyPath, settings);
            }
            return _decompiler;
        }
    }

    private static string GetTypeAttributes(TypeDefinition type)
    {
        var attrs = new List<string>();
        if (type.IsPublic) attrs.Add("public");
        if (type.IsAbstract && type.IsSealed) attrs.Add("static");
        else if (type.IsAbstract) attrs.Add("abstract");
        else if (type.IsSealed) attrs.Add("sealed");
        return string.Join(" ", attrs);
    }

    private static string GetMethodAttributes(MethodDefinition method)
    {
        var attrs = new List<string>();
        if (method.IsPublic) attrs.Add("public");
        if (method.IsPrivate) attrs.Add("private");
        if (method.IsStatic) attrs.Add("static");
        if (method.IsVirtual) attrs.Add("virtual");
        return string.Join(" ", attrs);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            if (_decompiler != null)
            {
                // CSharpDecompiler 本身未实现 IDisposable，但其内部持有的 MetadataFile（PEFile）需要清理
                var metadataFile = _decompiler.TypeSystem?.MainModule?.MetadataFile;
                if (metadataFile is IDisposable disposableFile)
                    disposableFile.Dispose();

                _decompiler = null;
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 反编译结果
/// </summary>
public record DecompileResult
{
    public bool IsSuccess { get; init; }
    public string? Code { get; init; }
    public string? Target { get; init; }
    public string? ErrorMessage { get; init; }

    public static DecompileResult Success(string code, string target)
        => new() { IsSuccess = true, Code = code, Target = target };

    public static DecompileResult Failure(string message)
        => new() { IsSuccess = false, ErrorMessage = message };
}
