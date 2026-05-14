using DotNetMcp.Backend.Core.Compilation;
using DotNetMcp.Backend.Core.Context;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DotNetMcp.Backend.Core.Modification;

/// <summary>
/// Roslyn 补丁服务 - 把 C# 代码片段编译后用 Cecil 替换目标方法的 IL
/// </summary>
public class RoslynPatchService
{
    private readonly CompilationService _compilation;

    private static readonly string[] DefaultUsings =
    {
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text"
    };

    public RoslynPatchService(CompilationService compilation)
    {
        _compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
    }

    /// <summary>
    /// 用 C# 源代码替换目标方法体
    /// </summary>
    public RoslynPatchResult ReplaceMethodBody(
        AssemblyContext context,
        string typeName,
        string methodName,
        string csharpBody,
        string[]? usings = null,
        string[]? referenceAssemblyPaths = null,
        bool allowUnsafe = false)
    {
        if (context.Assembly == null)
            return RoslynPatchResult.Failure("Assembly not loaded in context");

        if (string.IsNullOrWhiteSpace(typeName))
            return RoslynPatchResult.Failure("typeName cannot be empty");

        if (string.IsNullOrWhiteSpace(methodName))
            return RoslynPatchResult.Failure("methodName cannot be empty");

        if (string.IsNullOrWhiteSpace(csharpBody))
            return RoslynPatchResult.Failure("csharpBody cannot be empty");

        // 1. 找到目标方法
        var targetMethod = FindMethod(context.Assembly.MainModule, typeName, methodName);
        if (targetMethod == null)
            return RoslynPatchResult.Failure($"Method '{typeName}::{methodName}' not found in assembly");

        // 2. 生成包装类源码
        var effectiveUsings = (usings?.Length > 0 ? usings : DefaultUsings);
        var wrappedSource = BuildWrapperSource(targetMethod, csharpBody, effectiveUsings, referenceAssemblyPaths);

        // 3. 编译
        var compileOptions = new CompilationOptions
        {
            AllowUnsafe = allowUnsafe,
            OptimizationLevel = Microsoft.CodeAnalysis.OptimizationLevel.Debug
        };

        var compileResult = _compilation.Compile(wrappedSource, "PatchTemp", compileOptions);
        if (!compileResult.IsSuccess)
        {
            return new RoslynPatchResult
            {
                IsSuccess = false,
                ErrorMessage = compileResult.ErrorMessage,
                Diagnostics = compileResult.Diagnostics?.ToList(),
                TypeName = typeName,
                MethodName = methodName
            };
        }

        // 4. 从编译结果加载 patch 方法
        MethodDefinition? patchMethod;
        try
        {
            patchMethod = LoadPatchMethod(compileResult.AssemblyBytes!);
        }
        catch (Exception ex)
        {
            return RoslynPatchResult.Failure($"Failed to load compiled patch assembly: {ex.Message}");
        }

        if (patchMethod == null)
            return RoslynPatchResult.Failure("Compiled assembly does not contain __Patch__.__Method__");

        // 5. 把 patch 方法的 IL 复制到目标方法
        try
        {
            var count = MergeMethodBody(targetMethod, patchMethod, context.Assembly.MainModule);
            return new RoslynPatchResult
            {
                IsSuccess = true,
                TypeName = typeName,
                MethodName = methodName,
                InstructionsReplaced = count
            };
        }
        catch (Exception ex)
        {
            return RoslynPatchResult.Failure($"Failed to merge IL into target method: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // 私有辅助方法
    // -------------------------------------------------------------------------

    private static MethodDefinition? FindMethod(ModuleDefinition module, string typeName, string methodName)
    {
        foreach (var type in module.Types)
        {
            var m = FindMethodInType(type, typeName, methodName);
            if (m != null) return m;

            foreach (var nested in type.NestedTypes)
            {
                m = FindMethodInType(nested, typeName, methodName);
                if (m != null) return m;
            }
        }
        return null;
    }

    private static MethodDefinition? FindMethodInType(TypeDefinition type, string typeName, string methodName)
    {
        // 支持 "Namespace.Type" 或 Cecil FullName
        if (!type.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase) &&
            !type.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            return null;

        return type.Methods.FirstOrDefault(m =>
            m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 构建完整的可编译 C# 源代码（包裹在 __Patch__ 类中）
    /// </summary>
    private static string BuildWrapperSource(
        MethodDefinition targetMethod,
        string csharpBody,
        string[] usings,
        string[]? extraReferencePaths)
    {
        var usingLines = string.Join("\n", usings.Select(u => $"using {u};"));
        var returnTypeName = GetCSharpTypeName(targetMethod.ReturnType);
        var paramList = BuildParamList(targetMethod);

        return $@"{usingLines}

public static class __Patch__
{{
    public static {returnTypeName} __Method__({paramList})
    {{
        {csharpBody}
    }}
}}
";
    }

    private static string GetCSharpTypeName(TypeReference typeRef)
    {
        // 尝试把常见 CIL 类型名映射到 C# 关键字，方便编译
        return typeRef.FullName switch
        {
            "System.Void" => "void",
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Int16" => "short",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            "System.UInt16" => "ushort",
            "System.Boolean" => "bool",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Decimal" => "decimal",
            "System.Char" => "char",
            "System.String" => "string",
            "System.Object" => "object",
            _ => typeRef.FullName
        };
    }

    private static string BuildParamList(MethodDefinition method)
    {
        // 实例方法把 this 也作为第一个参数（静态包装类里用 object __this）
        var parts = new List<string>();

        if (!method.IsStatic)
        {
            var declaringTypeName = GetCSharpTypeName(method.DeclaringType);
            // 目标类型可能在另一个程序集，用 object 替代以保证可编译
            parts.Add("object __this");
        }

        foreach (var p in method.Parameters)
        {
            var typeName = GetCSharpTypeName(p.ParameterType);
            // 对于外部类型，降级为 object 避免编译失败
            var safeTypeName = IsBuiltinType(p.ParameterType) ? typeName : "object";
            parts.Add($"{safeTypeName} {SanitizeParamName(p.Name)}");
        }

        return string.Join(", ", parts);
    }

    private static bool IsBuiltinType(TypeReference typeRef)
    {
        return typeRef.FullName switch
        {
            "System.Void" or "System.Int32" or "System.Int64" or "System.Int16"
            or "System.Byte" or "System.SByte" or "System.UInt32" or "System.UInt64"
            or "System.UInt16" or "System.Boolean" or "System.Single" or "System.Double"
            or "System.Decimal" or "System.Char" or "System.String" or "System.Object" => true,
            _ => false
        };
    }

    private static string SanitizeParamName(string name)
    {
        // 避免 C# 保留字冲突
        return name switch
        {
            "this" => "__this",
            "params" => "__params",
            "ref" => "__ref",
            "out" => "__out",
            "in" => "__in",
            _ => string.IsNullOrEmpty(name) ? "__p" : name
        };
    }

    /// <summary>
    /// 从 bytes 加载 Cecil AssemblyDefinition 并找到 __Patch__.__Method__
    /// 使用 ReadingMode.Immediate 确保在 MemoryStream 关闭前完整读取所有元数据。
    /// </summary>
    private static MethodDefinition? LoadPatchMethod(byte[] assemblyBytes)
    {
        var ms = new MemoryStream(assemblyBytes);
        var patchAssembly = AssemblyDefinition.ReadAssembly(ms, new ReaderParameters
        {
            ReadingMode = ReadingMode.Immediate,
            InMemory = true
        });
        var patchType = patchAssembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "__Patch__");
        return patchType?.Methods.FirstOrDefault(m => m.Name == "__Method__");
    }

    /// <summary>
    /// 把 patchMethod 的方法体克隆到 targetMethod，跨 module 导入所有引用
    /// </summary>
    private static int MergeMethodBody(
        MethodDefinition targetMethod,
        MethodDefinition patchMethod,
        ModuleDefinition targetModule)
    {
        var targetBody = targetMethod.Body;
        var patchBody = patchMethod.Body;

        // 清空目标方法体
        targetBody.Instructions.Clear();
        targetBody.Variables.Clear();
        targetBody.ExceptionHandlers.Clear();
        targetBody.InitLocals = patchBody.InitLocals;

        // 复制局部变量，跨 module 导入类型
        var varMapping = new Dictionary<int, VariableDefinition>();
        for (int i = 0; i < patchBody.Variables.Count; i++)
        {
            var srcVar = patchBody.Variables[i];
            var importedType = targetModule.ImportReference(srcVar.VariableType);
            var newVar = new VariableDefinition(importedType);
            targetBody.Variables.Add(newVar);
            varMapping[i] = newVar;
        }

        var ilProcessor = targetBody.GetILProcessor();

        // 第一遍：创建指令（不处理跳转目标 Operand）
        var instructionMapping = new Dictionary<Instruction, Instruction>(ReferenceEqualityComparer.Instance);
        var newInstructions = new List<Instruction>();

        foreach (var srcInstr in patchBody.Instructions)
        {
            var newInstr = CloneInstruction(srcInstr, targetModule, varMapping, ilProcessor);
            instructionMapping[srcInstr] = newInstr;
            newInstructions.Add(newInstr);
        }

        // 第二遍：修复跳转 Operand（Instruction / Instruction[]）
        for (int i = 0; i < patchBody.Instructions.Count; i++)
        {
            var srcInstr = patchBody.Instructions[i];
            var newInstr = newInstructions[i];

            if (srcInstr.Operand is Instruction targetInstr && instructionMapping.TryGetValue(targetInstr, out var mappedTarget))
            {
                newInstr.Operand = mappedTarget;
            }
            else if (srcInstr.Operand is Instruction[] targetInstrs)
            {
                newInstr.Operand = targetInstrs
                    .Select(t => instructionMapping.TryGetValue(t, out var mt) ? mt : t)
                    .ToArray();
            }
        }

        // 追加到目标方法体
        foreach (var instr in newInstructions)
        {
            ilProcessor.Append(instr);
        }

        // 复制异常处理器
        foreach (var handler in patchBody.ExceptionHandlers)
        {
            var newHandler = new ExceptionHandler(handler.HandlerType);

            if (handler.TryStart != null && instructionMapping.TryGetValue(handler.TryStart, out var ts))
                newHandler.TryStart = ts;
            if (handler.TryEnd != null && instructionMapping.TryGetValue(handler.TryEnd, out var te))
                newHandler.TryEnd = te;
            if (handler.HandlerStart != null && instructionMapping.TryGetValue(handler.HandlerStart, out var hs))
                newHandler.HandlerStart = hs;
            if (handler.HandlerEnd != null && instructionMapping.TryGetValue(handler.HandlerEnd, out var he))
                newHandler.HandlerEnd = he;
            if (handler.FilterStart != null && instructionMapping.TryGetValue(handler.FilterStart, out var fs))
                newHandler.FilterStart = fs;

            if (handler.CatchType != null)
                newHandler.CatchType = targetModule.ImportReference(handler.CatchType);

            targetBody.ExceptionHandlers.Add(newHandler);
        }

        return newInstructions.Count;
    }

    /// <summary>
    /// 克隆单条指令，把 Operand 中的跨 module 引用导入到 targetModule。
    /// 跳转目标（Instruction/Instruction[]）留给调用方在第二遍修复。
    /// </summary>
    private static Instruction CloneInstruction(
        Instruction src,
        ModuleDefinition targetModule,
        Dictionary<int, VariableDefinition> varMapping,
        ILProcessor ilProcessor)
    {
        if (src.Operand == null)
            return ilProcessor.Create(src.OpCode);

        return src.Operand switch
        {
            // MethodReference — 跨 module 导入
            MethodReference mref =>
                ilProcessor.Create(src.OpCode, targetModule.ImportReference(mref)),

            // TypeReference — 跨 module 导入
            TypeReference tref =>
                ilProcessor.Create(src.OpCode, targetModule.ImportReference(tref)),

            // FieldReference — 跨 module 导入
            FieldReference fref =>
                ilProcessor.Create(src.OpCode, targetModule.ImportReference(fref)),

            // 局部变量 — 按索引重映射
            VariableDefinition vd =>
                ilProcessor.Create(src.OpCode,
                    varMapping.TryGetValue(vd.Index, out var newVar) ? newVar : vd),

            // 参数定义 — patch 包装类里按顺序对应目标方法参数
            ParameterDefinition pd =>
                ilProcessor.Create(src.OpCode, pd),

            // 字符串字面量
            string s =>
                ilProcessor.Create(src.OpCode, s),

            // 整数
            int iv =>
                ilProcessor.Create(src.OpCode, iv),
            sbyte sbv =>
                ilProcessor.Create(src.OpCode, sbv),
            long lv =>
                ilProcessor.Create(src.OpCode, lv),
            float fv =>
                ilProcessor.Create(src.OpCode, fv),
            double dv =>
                ilProcessor.Create(src.OpCode, dv),
            byte bv =>
                ilProcessor.Create(src.OpCode, bv),

            // 跳转目标 — 先用自身占位，第二遍修复
            Instruction _ =>
                ilProcessor.Create(src.OpCode, src.Operand as Instruction),

            Instruction[] _ =>
                ilProcessor.Create(src.OpCode, src.Operand as Instruction[]),

            // 其他（CallSite 等）— 不处理，记录原始 Operand
            _ => CreateWithRawOperand(ilProcessor, src)
        };
    }

    private static Instruction CreateWithRawOperand(ILProcessor ilProcessor, Instruction src)
    {
        // 降级处理：创建一个 nop 以免崩溃
        var nop = ilProcessor.Create(OpCodes.Nop);
        return nop;
    }
}

/// <summary>
/// Roslyn 补丁结果
/// </summary>
public record RoslynPatchResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<CompilationDiagnostic>? Diagnostics { get; init; }
    public string? TypeName { get; init; }
    public string? MethodName { get; init; }
    public int? InstructionsReplaced { get; init; }

    public static RoslynPatchResult Failure(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}
