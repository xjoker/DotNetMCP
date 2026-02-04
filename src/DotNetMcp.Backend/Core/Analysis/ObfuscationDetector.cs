using Mono.Cecil;
using Mono.Cecil.Cil;
using DotNetMcp.Backend.Core.Identity;
using DotNetMcp.Backend.Core.Utils;
using System.Text.RegularExpressions;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 混淆检测器 - 检测程序集是否被混淆及混淆特征
/// </summary>
public class ObfuscationDetector
{
    private readonly ModuleDefinition _module;
    private readonly MemberIdGenerator _idGenerator;

    // 常见混淆器特征
    private static readonly string[] KnownObfuscatorMarkers = new[]
    {
        "Dotfuscator", "ConfuserEx", "Confuser", "SmartAssembly",
        "Eazfuscator", "Babel", "Crypto", "Agile.NET", "Spices",
        "DeepSea", ".NET Reactor", "MaxtoCode", "Themida",
        "VMProtect", "Enigma", "ArmDot", "Obfuscar"
    };

    // 非法标识符模式
    private static readonly Regex InvalidIdentifierPattern = new(
        @"^[^a-zA-Z_]|[^\w]|[\x00-\x1F\x7F-\xFF]",
        RegexOptions.Compiled);

    // 随机字符串模式
    private static readonly Regex RandomStringPattern = new(
        @"^[a-zA-Z0-9]{20,}$|^[A-Z]{10,}$|^[a-z]{10,}$",
        RegexOptions.Compiled);

    public ObfuscationDetector(ModuleDefinition module, Guid mvid)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _idGenerator = new MemberIdGenerator(mvid);
    }

    /// <summary>
    /// 执行完整的混淆检测
    /// </summary>
    public ObfuscationDetectionResult Detect()
    {
        var indicators = new List<ObfuscationIndicator>();
        var stats = new ObfuscationStats();

        // 1. 检测混淆器标记
        var obfuscatorMarkers = DetectObfuscatorMarkers();
        indicators.AddRange(obfuscatorMarkers);

        // 2. 检测非法标识符
        var invalidNames = DetectInvalidIdentifiers(stats);
        indicators.AddRange(invalidNames);

        // 3. 检测随机/超短类型名
        var randomNames = DetectRandomNames(stats);
        indicators.AddRange(randomNames);

        // 4. 检测控制流平坦化
        var controlFlowFlattening = DetectControlFlowFlattening(stats);
        indicators.AddRange(controlFlowFlattening);

        // 5. 检测字符串加密
        var stringEncryption = DetectStringEncryption(stats);
        indicators.AddRange(stringEncryption);

        // 6. 检测反调试/反反编译
        var antiDebug = DetectAntiDebug();
        indicators.AddRange(antiDebug);

        // 7. 检测代理调用
        var proxyCalls = DetectProxyCalls(stats);
        indicators.AddRange(proxyCalls);

        // 计算混淆得分
        var score = CalculateObfuscationScore(indicators, stats);

        return new ObfuscationDetectionResult
        {
            IsSuccess = true,
            IsObfuscated = score >= 30,
            ObfuscationScore = score,
            Confidence = score >= 70 ? "High" : score >= 40 ? "Medium" : "Low",
            DetectedObfuscators = obfuscatorMarkers
                .Where(i => i.Category == "ObfuscatorMarker")
                .Select(i => i.Description)
                .Distinct()
                .ToList(),
            Indicators = indicators,
            Statistics = stats
        };
    }

    /// <summary>
    /// 检测混淆器标记
    /// </summary>
    private List<ObfuscationIndicator> DetectObfuscatorMarkers()
    {
        var indicators = new List<ObfuscationIndicator>();

        // 检查自定义属性
        foreach (var attr in _module.CustomAttributes)
        {
            var attrName = attr.AttributeType.FullName;
            foreach (var marker in KnownObfuscatorMarkers)
            {
                if (attrName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    indicators.Add(new ObfuscationIndicator
                    {
                        Category = "ObfuscatorMarker",
                        Severity = "High",
                        Description = marker,
                        Location = $"Module attribute: {StringSanitizer.Sanitize(attrName)}",
                        Evidence = new List<string> { $"Attribute: {StringSanitizer.Sanitize(attrName)}" }
                    });
                }
            }
        }

        // 检查程序集属性
        foreach (var attr in _module.Assembly.CustomAttributes)
        {
            var attrName = attr.AttributeType.FullName;
            foreach (var marker in KnownObfuscatorMarkers)
            {
                if (attrName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    indicators.Add(new ObfuscationIndicator
                    {
                        Category = "ObfuscatorMarker",
                        Severity = "High",
                        Description = marker,
                        Location = $"Assembly attribute: {StringSanitizer.Sanitize(attrName)}",
                        Evidence = new List<string> { $"Attribute: {StringSanitizer.Sanitize(attrName)}" }
                    });
                }
            }
        }

        // 检查特殊类型名
        foreach (var type in _module.Types.Take(100))
        {
            foreach (var marker in KnownObfuscatorMarkers)
            {
                if (type.FullName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    indicators.Add(new ObfuscationIndicator
                    {
                        Category = "ObfuscatorMarker",
                        Severity = "Medium",
                        Description = marker,
                        Location = StringSanitizer.SanitizeTypeName(type.FullName),
                        Evidence = new List<string> { $"Type name contains: {marker}" }
                    });
                    break;
                }
            }
        }

        return indicators;
    }

    /// <summary>
    /// 检测非法标识符
    /// </summary>
    private List<ObfuscationIndicator> DetectInvalidIdentifiers(ObfuscationStats stats)
    {
        var indicators = new List<ObfuscationIndicator>();
        var invalidCount = 0;

        foreach (var type in _module.Types)
        {
            stats.TotalTypes++;

            // 检查类型名
            if (IsInvalidIdentifier(type.Name))
            {
                invalidCount++;
                stats.InvalidTypeNames++;
                if (indicators.Count < 10) // 限制数量
                {
                    indicators.Add(new ObfuscationIndicator
                    {
                        Category = "InvalidIdentifier",
                        Severity = "Medium",
                        Description = "Invalid type name",
                        Location = StringSanitizer.SanitizeTypeName(type.FullName),
                        Evidence = new List<string> { $"Name: {StringSanitizer.Sanitize(type.Name)}" }
                    });
                }
            }

            // 检查方法名
            foreach (var method in type.Methods)
            {
                stats.TotalMethods++;
                if (!method.IsConstructor && IsInvalidIdentifier(method.Name))
                {
                    stats.InvalidMethodNames++;
                }
            }

            // 检查字段名
            foreach (var field in type.Fields)
            {
                stats.TotalFields++;
                if (IsInvalidIdentifier(field.Name))
                {
                    stats.InvalidFieldNames++;
                }
            }
        }

        if (invalidCount > 0)
        {
            indicators.Insert(0, new ObfuscationIndicator
            {
                Category = "InvalidIdentifier",
                Severity = invalidCount > 10 ? "High" : "Medium",
                Description = $"Found {invalidCount} types with invalid identifiers",
                Location = "Module",
                Evidence = new List<string> { $"Ratio: {(double)invalidCount / stats.TotalTypes:P1}" }
            });
        }

        return indicators;
    }

    /// <summary>
    /// 检测随机/超短类型名
    /// </summary>
    private List<ObfuscationIndicator> DetectRandomNames(ObfuscationStats stats)
    {
        var indicators = new List<ObfuscationIndicator>();
        var shortNames = 0;
        var randomNames = 0;

        foreach (var type in _module.Types)
        {
            var name = type.Name;
            if (name.StartsWith("<")) continue; // 跳过编译器生成

            // 超短名称 (1-2字符)
            if (name.Length <= 2 && !IsCommonShortName(name))
            {
                shortNames++;
                stats.ShortTypeNames++;
            }

            // 随机字符串模式
            if (RandomStringPattern.IsMatch(name) || LooksRandom(name))
            {
                randomNames++;
                stats.RandomTypeNames++;
            }
        }

        if (shortNames > 5)
        {
            indicators.Add(new ObfuscationIndicator
            {
                Category = "ShortNames",
                Severity = shortNames > 20 ? "High" : "Medium",
                Description = $"Found {shortNames} types with very short names (1-2 chars)",
                Location = "Module",
                Evidence = new List<string> { "Typical of name obfuscation" }
            });
        }

        if (randomNames > 5)
        {
            indicators.Add(new ObfuscationIndicator
            {
                Category = "RandomNames",
                Severity = randomNames > 20 ? "High" : "Medium",
                Description = $"Found {randomNames} types with random-looking names",
                Location = "Module",
                Evidence = new List<string> { "Typical of name obfuscation" }
            });
        }

        return indicators;
    }

    /// <summary>
    /// 检测控制流平坦化
    /// </summary>
    private List<ObfuscationIndicator> DetectControlFlowFlattening(ObfuscationStats stats)
    {
        var indicators = new List<ObfuscationIndicator>();
        var suspiciousMethods = 0;

        foreach (var type in _module.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                var body = method.Body;
                var switchCount = body.Instructions.Count(i => i.OpCode == OpCodes.Switch);
                var branchCount = body.Instructions.Count(i =>
                    i.OpCode.FlowControl == FlowControl.Cond_Branch ||
                    i.OpCode.FlowControl == FlowControl.Branch);

                // 控制流平坦化特征: 大量 switch + 循环结构
                if (switchCount >= 1 && branchCount > 20 && body.Instructions.Count > 100)
                {
                    // 检查是否有状态变量模式
                    var localCount = body.Variables.Count;
                    if (localCount > 5)
                    {
                        suspiciousMethods++;
                        stats.FlattenedMethods++;

                        if (indicators.Count < 5)
                        {
                            indicators.Add(new ObfuscationIndicator
                            {
                                Category = "ControlFlowFlattening",
                                Severity = "High",
                                Description = "Possible control flow flattening",
                                Location = $"{StringSanitizer.SanitizeTypeName(type.FullName)}.{StringSanitizer.SanitizeMethodName(method.Name)}",
                                Evidence = new List<string>
                                {
                                    $"Switch statements: {switchCount}",
                                    $"Branches: {branchCount}",
                                    $"Instructions: {body.Instructions.Count}",
                                    $"Local variables: {localCount}"
                                }
                            });
                        }
                    }
                }
            }
        }

        return indicators;
    }

    /// <summary>
    /// 检测字符串加密
    /// </summary>
    private List<ObfuscationIndicator> DetectStringEncryption(ObfuscationStats stats)
    {
        var indicators = new List<ObfuscationIndicator>();
        var suspiciousCalls = 0;

        // 查找可能的解密方法调用
        foreach (var type in _module.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                    {
                        if (instruction.Operand is MethodReference methodRef)
                        {
                            var name = methodRef.Name.ToLower();
                            // 常见解密方法名模式
                            if (name.Contains("decrypt") || name.Contains("deobfuscate") ||
                                name.Contains("getstring") && methodRef.Parameters.Count == 1 &&
                                methodRef.Parameters[0].ParameterType.FullName == "System.Int32")
                            {
                                suspiciousCalls++;
                            }
                        }
                    }
                }
            }
        }

        // 检查是否存在 byte[] 静态字段 (常用于存储加密字符串)
        var byteArrayFields = _module.Types
            .SelectMany(t => t.Fields)
            .Count(f => f.IsStatic && f.FieldType.FullName == "System.Byte[]");

        stats.EncryptedStringIndicators = suspiciousCalls + (byteArrayFields > 10 ? 1 : 0);

        if (suspiciousCalls > 5 || byteArrayFields > 20)
        {
            indicators.Add(new ObfuscationIndicator
            {
                Category = "StringEncryption",
                Severity = "High",
                Description = "Possible string encryption detected",
                Location = "Module",
                Evidence = new List<string>
                {
                    $"Suspicious decrypt calls: {suspiciousCalls}",
                    $"Static byte[] fields: {byteArrayFields}"
                }
            });
        }

        return indicators;
    }

    /// <summary>
    /// 检测反调试/反反编译技术
    /// </summary>
    private List<ObfuscationIndicator> DetectAntiDebug()
    {
        var indicators = new List<ObfuscationIndicator>();

        foreach (var type in _module.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                    {
                        if (instruction.Operand is MethodReference methodRef)
                        {
                            var fullName = $"{methodRef.DeclaringType.FullName}.{methodRef.Name}";

                            // 反调试 API
                            if (fullName.Contains("Debugger.IsAttached") ||
                                fullName.Contains("Debugger.IsLogging") ||
                                fullName.Contains("CheckRemoteDebuggerPresent") ||
                                fullName.Contains("IsDebuggerPresent") ||
                                fullName.Contains("OutputDebugString"))
                            {
                                indicators.Add(new ObfuscationIndicator
                                {
                                    Category = "AntiDebug",
                                    Severity = "High",
                                    Description = "Anti-debugging technique detected",
                                    Location = $"{StringSanitizer.SanitizeTypeName(type.FullName)}.{StringSanitizer.SanitizeMethodName(method.Name)}",
                                    Evidence = new List<string> { $"Call to: {StringSanitizer.Sanitize(fullName)}" }
                                });
                            }

                            // 环境检测
                            if (fullName.Contains("Environment.GetEnvironmentVariable") ||
                                fullName.Contains("Environment.MachineName") && method.Name.Contains("Check", StringComparison.OrdinalIgnoreCase))
                            {
                                // 可能是 VM/沙箱检测，但不一定是混淆
                            }
                        }
                    }
                }
            }
        }

        return indicators;
    }

    /// <summary>
    /// 检测代理调用
    /// </summary>
    private List<ObfuscationIndicator> DetectProxyCalls(ObfuscationStats stats)
    {
        var indicators = new List<ObfuscationIndicator>();
        var proxyMethods = 0;

        // 代理方法特征: 只包含一个 call/callvirt + ret
        foreach (var type in _module.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody || method.IsConstructor) continue;

                var instructions = method.Body.Instructions;
                if (instructions.Count >= 2 && instructions.Count <= 5)
                {
                    var hasOnlyCall = instructions.Count(i =>
                        i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) == 1;
                    var hasRet = instructions.Any(i => i.OpCode == OpCodes.Ret);
                    var hasLdarg = instructions.All(i =>
                        i.OpCode == OpCodes.Ldarg_0 || i.OpCode == OpCodes.Ldarg_1 ||
                        i.OpCode == OpCodes.Ldarg_2 || i.OpCode == OpCodes.Ldarg_3 ||
                        i.OpCode == OpCodes.Ldarg_S || i.OpCode == OpCodes.Ldarg ||
                        i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt ||
                        i.OpCode == OpCodes.Ret || i.OpCode == OpCodes.Nop);

                    if (hasOnlyCall && hasRet && hasLdarg)
                    {
                        proxyMethods++;
                    }
                }
            }
        }

        stats.ProxyMethods = proxyMethods;

        if (proxyMethods > 20)
        {
            indicators.Add(new ObfuscationIndicator
            {
                Category = "ProxyCalls",
                Severity = proxyMethods > 50 ? "High" : "Medium",
                Description = $"Found {proxyMethods} potential proxy methods",
                Location = "Module",
                Evidence = new List<string> { "Methods that only forward calls to other methods" }
            });
        }

        return indicators;
    }

    private int CalculateObfuscationScore(List<ObfuscationIndicator> indicators, ObfuscationStats stats)
    {
        var score = 0;

        // 混淆器标记
        score += indicators.Count(i => i.Category == "ObfuscatorMarker") * 30;

        // 无效标识符
        if (stats.TotalTypes > 0)
        {
            var invalidRatio = (double)stats.InvalidTypeNames / stats.TotalTypes;
            score += (int)(invalidRatio * 50);
        }

        // 短名称
        score += Math.Min(stats.ShortTypeNames, 20);

        // 控制流平坦化
        score += stats.FlattenedMethods * 10;

        // 字符串加密
        score += stats.EncryptedStringIndicators * 5;

        // 代理方法
        score += Math.Min(stats.ProxyMethods / 5, 10);

        // 反调试
        score += indicators.Count(i => i.Category == "AntiDebug") * 15;

        return Math.Min(score, 100);
    }

    private static bool IsInvalidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        if (name.StartsWith("<")) return false; // 编译器生成的合法名称

        // 检查非法字符
        return InvalidIdentifierPattern.IsMatch(name) ||
               name.Any(c => c > 127 && !char.IsLetter(c)) ||
               name.Contains('\0');
    }

    private static bool IsCommonShortName(string name)
    {
        var common = new[] { "T", "K", "V", "E", "I", "ID", "Id", "OK", "UI" };
        return common.Contains(name);
    }

    private static bool LooksRandom(string name)
    {
        if (name.Length < 8) return false;

        // 检查连续辅音或元音
        var vowels = "aeiouAEIOU";
        var consonantCount = 0;
        var vowelCount = 0;

        foreach (var c in name)
        {
            if (vowels.Contains(c))
            {
                vowelCount++;
                consonantCount = 0;
            }
            else if (char.IsLetter(c))
            {
                consonantCount++;
                vowelCount = 0;
            }

            if (consonantCount > 5 || vowelCount > 4) return true;
        }

        // 检查数字混合
        var digitRatio = (double)name.Count(char.IsDigit) / name.Length;
        if (digitRatio > 0.3 && name.Length > 10) return true;

        return false;
    }

    private static string EscapeString(string s)
    {
        return string.Concat(s.Select(c => c < 32 || c > 126 ? $"\\x{(int)c:X2}" : c.ToString()));
    }
}

#region 数据结构

/// <summary>
/// 混淆检测结果
/// </summary>
public record ObfuscationDetectionResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsObfuscated { get; init; }
    public int ObfuscationScore { get; init; }
    public string? Confidence { get; init; }
    public List<string>? DetectedObfuscators { get; init; }
    public List<ObfuscationIndicator>? Indicators { get; init; }
    public ObfuscationStats? Statistics { get; init; }
}

/// <summary>
/// 混淆指标
/// </summary>
public record ObfuscationIndicator
{
    public required string Category { get; init; }
    public required string Severity { get; init; }
    public required string Description { get; init; }
    public required string Location { get; init; }
    public required List<string> Evidence { get; init; }
}

/// <summary>
/// 混淆统计
/// </summary>
public class ObfuscationStats
{
    public int TotalTypes { get; set; }
    public int TotalMethods { get; set; }
    public int TotalFields { get; set; }
    public int InvalidTypeNames { get; set; }
    public int InvalidMethodNames { get; set; }
    public int InvalidFieldNames { get; set; }
    public int ShortTypeNames { get; set; }
    public int RandomTypeNames { get; set; }
    public int FlattenedMethods { get; set; }
    public int EncryptedStringIndicators { get; set; }
    public int ProxyMethods { get; set; }
}

#endregion
