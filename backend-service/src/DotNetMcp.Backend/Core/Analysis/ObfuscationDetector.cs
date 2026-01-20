using DotNetMcp.Backend.Core.Context;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text.RegularExpressions;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 混淆检测器 - 检测代码混淆特征
/// </summary>
public class ObfuscationDetector
{
    /// <summary>
    /// 检测程序集中的混淆特征
    /// </summary>
    public ObfuscationAnalysisResult DetectObfuscation(AssemblyContext context)
    {
        var result = new ObfuscationAnalysisResult();

        foreach (var type in context.Assembly!.MainModule.Types)
        {
            // 检测类型名混淆
            if (IsObfuscatedIdentifier(type.Name))
            {
                result.ObfuscatedTypes.Add(new ObfuscatedItem
                {
                    ItemType = ObfuscationType.TypeName,
                    Name = type.FullName,
                    Severity = CalculateObfuscationSeverity(type.Name),
                    Evidence = $"Suspicious type name: {type.Name}"
                });
            }

            // 检测方法
            foreach (var method in type.Methods)
            {
                // 混淆的方法名
                if (IsObfuscatedIdentifier(method.Name))
                {
                    result.ObfuscatedMethods.Add(new ObfuscatedItem
                    {
                        ItemType = ObfuscationType.MethodName,
                        Name = $"{type.FullName}.{method.Name}",
                        Severity = CalculateObfuscationSeverity(method.Name),
                        Evidence = $"Suspicious method name: {method.Name}"
                    });
                }

                // 分析方法体
                if (method.HasBody)
                {
                    AnalyzeMethodBody(method, type.FullName, result);
                }
            }

            // 检测字段名混淆
            foreach (var field in type.Fields)
            {
                if (IsObfuscatedIdentifier(field.Name))
                {
                    result.ObfuscatedFields.Add(new ObfuscatedItem
                    {
                        ItemType = ObfuscationType.FieldName,
                        Name = $"{type.FullName}.{field.Name}",
                        Severity = CalculateObfuscationSeverity(field.Name),
                        Evidence = $"Suspicious field name: {field.Name}"
                    });
                }
            }
        }

        // 计算混淆评分
        result.ObfuscationScore = CalculateObfuscationScore(result);
        result.IsObfuscated = result.ObfuscationScore > 0.3;

        return result;
    }

    private void AnalyzeMethodBody(MethodDefinition method, string typeName, ObfuscationAnalysisResult result)
    {
        if (!method.HasBody) return;

        var body = method.Body;
        var instructions = body.Instructions;

        // 检测控制流平坦化
        if (HasControlFlowFlattening(instructions))
        {
            result.ControlFlowObfuscations.Add(new ObfuscatedItem
            {
                ItemType = ObfuscationType.ControlFlowFlattening,
                Name = $"{typeName}.{method.Name}",
                Severity = ObfuscationSeverity.High,
                Evidence = "Detected control flow flattening pattern (switch-based state machine)"
            });
        }

        // 检测字符串加密
        var encryptedStrings = DetectStringEncryption(instructions);
        if (encryptedStrings > 0)
        {
            result.StringObfuscations.Add(new ObfuscatedItem
            {
                ItemType = ObfuscationType.StringEncryption,
                Name = $"{typeName}.{method.Name}",
                Severity = ObfuscationSeverity.Medium,
                Evidence = $"Detected {encryptedStrings} potential string decryption call(s)"
            });
        }

        // 检测无意义的跳转和 NOP 指令
        if (HasJunkCode(instructions))
        {
            result.JunkCodePatterns.Add(new ObfuscatedItem
            {
                ItemType = ObfuscationType.JunkCode,
                Name = $"{typeName}.{method.Name}",
                Severity = ObfuscationSeverity.Low,
                Evidence = "Excessive NOP instructions or meaningless branches"
            });
        }
    }

    #region Identifier Analysis

    private bool IsObfuscatedIdentifier(string name)
    {
        // 跳过编译器生成的名称
        if (name.StartsWith("<") || name.StartsWith("CS$")) 
            return false;

        // 特殊方法名
        if (name.StartsWith(".c") || name == ".ctor" || name == ".cctor")
            return false;

        // 检测单字符或极短名称 (排除常见的如 i, x, y)
        if (name.Length == 1 && !"ixyzIXYZ".Contains(name))
            return true;

        // 检测随机字符模式
        if (Regex.IsMatch(name, @"^[a-zA-Z0-9]{32,}$")) // 超长无意义字符串
            return true;

        // 检测非 ASCII 字符或特殊 Unicode 字符
        if (Regex.IsMatch(name, @"[\u0000-\u001F\u007F-\u009F]")) // 控制字符
            return true;

        // 检测零宽字符
        if (name.Any(c => c == '\u200B' || c == '\u200C' || c == '\u200D'))
            return true;

        // 检测重复字符模式 (aaaaaa, 111111)
        if (Regex.IsMatch(name, @"^(.)\1{5,}$"))
            return true;

        // 检测混合大小写无规律 (aBcDeF)
        if (name.Length > 3 && HasRandomCasing(name))
            return true;

        return false;
    }

    private bool HasRandomCasing(string name)
    {
        int transitions = 0;
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsLetter(name[i - 1]) && char.IsLetter(name[i]))
            {
                if (char.IsUpper(name[i - 1]) != char.IsUpper(name[i]))
                {
                    transitions++;
                }
            }
        }
        // 如果大小写切换次数 > 长度的一半，认为是随机的
        return transitions > name.Length / 2;
    }

    private ObfuscationSeverity CalculateObfuscationSeverity(string name)
    {
        if (name.Length == 1 || Regex.IsMatch(name, @"[\u0000-\u001F]"))
            return ObfuscationSeverity.High;

        if (Regex.IsMatch(name, @"^[a-zA-Z0-9]{32,}$"))
            return ObfuscationSeverity.High;

        if (HasRandomCasing(name))
            return ObfuscationSeverity.Medium;

        return ObfuscationSeverity.Low;
    }

    #endregion

    #region Control Flow Analysis

    private bool HasControlFlowFlattening(IEnumerable<Instruction> instructions)
    {
        // 检测 switch-based state machine 模式
        var switchCount = instructions.Count(i => i.OpCode == OpCodes.Switch);
        if (switchCount == 0) return false;

        // 检测状态变量赋值模式 (大量的 ldc.i4 + stloc)
        var stateAssignments = 0;
        var instructionList = instructions.ToList();
        for (int i = 0; i < instructionList.Count - 1; i++)
        {
            if (instructionList[i].OpCode.Name.StartsWith("ldc.i4") &&
                instructionList[i + 1].OpCode.Name.StartsWith("stloc"))
            {
                stateAssignments++;
            }
        }

        // 如果有 switch 且状态赋值多，很可能是控制流平坦化
        return switchCount >= 1 && stateAssignments > 10;
    }

    private int DetectStringEncryption(IEnumerable<Instruction> instructions)
    {
        int count = 0;
        foreach (var instr in instructions)
        {
            // 检测可疑的字符串解密方法调用
            if (instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt)
            {
                if (instr.Operand is MethodReference mr)
                {
                    // 常见的字符串解密方法特征
                    var methodName = mr.Name.ToLowerInvariant();
                    if (methodName.Contains("decrypt") ||
                        methodName.Contains("decode") ||
                        methodName.Contains("deobfuscate") ||
                        (mr.ReturnType.FullName == "System.String" && 
                         mr.Parameters.Count == 1 &&
                         mr.Parameters[0].ParameterType.FullName == "System.Int32"))
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }

    private bool HasJunkCode(IEnumerable<Instruction> instructions)
    {
        var instructionList = instructions.ToList();
        if (instructionList.Count == 0) return false;

        // 计算 NOP 指令比例
        var nopCount = instructionList.Count(i => i.OpCode == OpCodes.Nop);
        var nopRatio = (double)nopCount / instructionList.Count;

        // 如果 NOP 超过 20%，认为有垃圾代码
        if (nopRatio > 0.2)
            return true;

        // 检测无意义的跳转 (跳到下一条指令)
        int meaninglessBranches = 0;
        for (int i = 0; i < instructionList.Count - 1; i++)
        {
            var instr = instructionList[i];
            if (instr.OpCode.FlowControl == FlowControl.Branch || 
                instr.OpCode.FlowControl == FlowControl.Cond_Branch)
            {
                if (instr.Operand is Instruction target && target == instructionList[i + 1])
                {
                    meaninglessBranches++;
                }
            }
        }

        return meaninglessBranches > 3;
    }

    #endregion

    #region Scoring

    private double CalculateObfuscationScore(ObfuscationAnalysisResult result)
    {
        double score = 0.0;

        // 类型名混淆
        score += result.ObfuscatedTypes.Count * 0.05;
        score += result.ObfuscatedTypes.Count(i => i.Severity == ObfuscationSeverity.High) * 0.05;

        // 方法名混淆
        score += result.ObfuscatedMethods.Count * 0.03;
        score += result.ObfuscatedMethods.Count(i => i.Severity == ObfuscationSeverity.High) * 0.03;

        // 字段名混淆
        score += result.ObfuscatedFields.Count * 0.02;

        // 控制流混淆 (高权重)
        score += result.ControlFlowObfuscations.Count * 0.15;

        // 字符串加密
        score += result.StringObfuscations.Count * 0.10;

        // 垃圾代码
        score += result.JunkCodePatterns.Count * 0.05;

        return Math.Min(score, 1.0);
    }

    #endregion
}

#region Models

public class ObfuscationAnalysisResult
{
    public bool IsObfuscated { get; set; }
    public double ObfuscationScore { get; set; }
    public List<ObfuscatedItem> ObfuscatedTypes { get; set; } = new();
    public List<ObfuscatedItem> ObfuscatedMethods { get; set; } = new();
    public List<ObfuscatedItem> ObfuscatedFields { get; set; } = new();
    public List<ObfuscatedItem> ControlFlowObfuscations { get; set; } = new();
    public List<ObfuscatedItem> StringObfuscations { get; set; } = new();
    public List<ObfuscatedItem> JunkCodePatterns { get; set; } = new();
}

public class ObfuscatedItem
{
    public ObfuscationType ItemType { get; set; }
    public string Name { get; set; } = "";
    public ObfuscationSeverity Severity { get; set; }
    public string Evidence { get; set; } = "";
}

public enum ObfuscationType
{
    TypeName,
    MethodName,
    FieldName,
    ControlFlowFlattening,
    StringEncryption,
    JunkCode
}

public enum ObfuscationSeverity
{
    Low,
    Medium,
    High
}

#endregion
