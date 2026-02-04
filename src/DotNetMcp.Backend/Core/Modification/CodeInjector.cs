using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DotNetMcp.Backend.Core.Modification;

/// <summary>
/// 代码注入器 - 在方法中注入代码
/// </summary>
public class CodeInjector
{
    private readonly ModuleDefinition _module;

    public CodeInjector(ModuleDefinition module)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
    }

    /// <summary>
    /// 在方法开头注入代码
    /// </summary>
    public InjectionResult InjectAtMethodEntry(MethodDefinition method, IReadOnlyList<Instruction> instructions)
    {
        if (method == null) return InjectionResult.Failure("Method cannot be null");
        if (!method.HasBody) return InjectionResult.Failure("Method has no body");
        if (instructions == null || instructions.Count == 0)
            return InjectionResult.Failure("Instructions cannot be empty");

        try
        {
            var processor = method.Body.GetILProcessor();
            var firstInstruction = method.Body.Instructions.FirstOrDefault();

            if (firstInstruction == null)
            {
                // 空方法，直接添加
                foreach (var instruction in instructions)
                {
                    processor.Append(instruction);
                }
            }
            else
            {
                // 在第一条指令之前插入
                for (int i = instructions.Count - 1; i >= 0; i--)
                {
                    processor.InsertBefore(firstInstruction, instructions[i]);
                }
            }

            return InjectionResult.Success(instructions.Count);
        }
        catch (Exception ex)
        {
            return InjectionResult.Failure($"Injection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 在方法返回前注入代码
    /// </summary>
    public InjectionResult InjectBeforeReturn(MethodDefinition method, IReadOnlyList<Instruction> instructions)
    {
        if (method == null) return InjectionResult.Failure("Method cannot be null");
        if (!method.HasBody) return InjectionResult.Failure("Method has no body");
        if (instructions == null || instructions.Count == 0)
            return InjectionResult.Failure("Instructions cannot be empty");

        try
        {
            var processor = method.Body.GetILProcessor();
            var retInstructions = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList();

            if (retInstructions.Count == 0)
                return InjectionResult.Failure("No return instruction found");

            int totalInjected = 0;
            foreach (var ret in retInstructions)
            {
                for (int i = instructions.Count - 1; i >= 0; i--)
                {
                    var clone = CloneInstruction(instructions[i], processor);
                    processor.InsertBefore(ret, clone);
                    totalInjected++;
                }
            }

            return InjectionResult.Success(totalInjected);
        }
        catch (Exception ex)
        {
            return InjectionResult.Failure($"Injection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 在指定偏移量处注入代码
    /// </summary>
    public InjectionResult InjectAtOffset(MethodDefinition method, int ilOffset, IReadOnlyList<Instruction> instructions)
    {
        if (method == null) return InjectionResult.Failure("Method cannot be null");
        if (!method.HasBody) return InjectionResult.Failure("Method has no body");

        try
        {
            var processor = method.Body.GetILProcessor();
            var target = method.Body.Instructions.FirstOrDefault(i => i.Offset == ilOffset);

            if (target == null)
                return InjectionResult.Failure($"No instruction at offset {ilOffset}");

            for (int i = instructions.Count - 1; i >= 0; i--)
            {
                processor.InsertBefore(target, instructions[i]);
            }

            return InjectionResult.Success(instructions.Count);
        }
        catch (Exception ex)
        {
            return InjectionResult.Failure($"Injection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 替换方法体
    /// </summary>
    public InjectionResult ReplaceMethodBody(MethodDefinition method, IReadOnlyList<Instruction> instructions)
    {
        if (method == null) return InjectionResult.Failure("Method cannot be null");
        if (!method.HasBody) return InjectionResult.Failure("Method has no body");

        try
        {
            method.Body.Instructions.Clear();
            method.Body.Variables.Clear();
            method.Body.ExceptionHandlers.Clear();

            var processor = method.Body.GetILProcessor();
            foreach (var instruction in instructions)
            {
                processor.Append(instruction);
            }

            return InjectionResult.Success(instructions.Count);
        }
        catch (Exception ex)
        {
            return InjectionResult.Failure($"Replace failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 包装方法（在原方法前后添加代码）
    /// </summary>
    public InjectionResult WrapMethod(
        MethodDefinition method,
        IReadOnlyList<Instruction>? before,
        IReadOnlyList<Instruction>? after)
    {
        if (method == null) return InjectionResult.Failure("Method cannot be null");
        if (!method.HasBody) return InjectionResult.Failure("Method has no body");

        int totalInjected = 0;

        if (before != null && before.Count > 0)
        {
            var result = InjectAtMethodEntry(method, before);
            if (!result.IsSuccess) return result;
            totalInjected += result.InjectedCount;
        }

        if (after != null && after.Count > 0)
        {
            var result = InjectBeforeReturn(method, after);
            if (!result.IsSuccess) return result;
            totalInjected += result.InjectedCount;
        }

        return InjectionResult.Success(totalInjected);
    }

    private Instruction CloneInstruction(Instruction source, ILProcessor processor)
    {
        // 简单克隆，对于复杂操作数需要更详细处理
        if (source.Operand == null)
            return processor.Create(source.OpCode);

        return source.Operand switch
        {
            int i => processor.Create(source.OpCode, i),
            long l => processor.Create(source.OpCode, l),
            float f => processor.Create(source.OpCode, f),
            double d => processor.Create(source.OpCode, d),
            string s => processor.Create(source.OpCode, s),
            MethodReference mr => processor.Create(source.OpCode, mr),
            FieldReference fr => processor.Create(source.OpCode, fr),
            TypeReference tr => processor.Create(source.OpCode, tr),
            _ => processor.Create(source.OpCode)
        };
    }
}

/// <summary>
/// 注入结果
/// </summary>
public record InjectionResult
{
    public bool IsSuccess { get; init; }
    public int InjectedCount { get; init; }
    public string? ErrorMessage { get; init; }

    public static InjectionResult Success(int count) => new() { IsSuccess = true, InjectedCount = count };
    public static InjectionResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
