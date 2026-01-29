using Mono.Cecil;
using Mono.Cecil.Cil;
using DotNetMcp.Backend.Core.Identity;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DotNetMcp.Backend.Core.Analysis;

#region 搜索模式和请求

/// <summary>
/// 搜索模式
/// </summary>
public enum SearchMode
{
    TypeAndMember,      // 类型和成员
    Type,               // 仅类型
    Member,             // 仅成员
    Method,             // 仅方法
    Field,              // 仅字段
    Property,           // 仅属性
    Event,              // 仅事件
    Literal,            // 字面量（字符串/数字）
    Token,              // 元数据 Token
    Resource,           // 资源文件
    Namespace           // 命名空间
}

/// <summary>
/// 搜索请求
/// </summary>
public class SearchRequest
{
    public required string RawQuery { get; init; }
    public SearchMode Mode { get; init; } = SearchMode.TypeAndMember;
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public Regex? RegEx { get; set; }
    public bool UseRegex { get; init; }
    public bool ExactMatch { get; init; }
    public bool CaseSensitive { get; init; }
    public string? NamespaceFilter { get; init; }
    public int Limit { get; init; } = 100;
    
    // 高级语法解析结果
    public List<string> MustContain { get; set; } = new();    // +term
    public List<string> MustExclude { get; set; } = new();    // -term
    public List<string> ExactTerms { get; set; } = new();     // =term
    public List<string> FuzzyTerms { get; set; } = new();     // ~term

    /// <summary>
    /// 解析搜索查询
    /// </summary>
    public static SearchRequest Parse(string query, SearchMode mode = SearchMode.TypeAndMember, int limit = 100)
    {
        var request = new SearchRequest
        {
            RawQuery = query,
            Mode = mode,
            Limit = limit
        };

        if (string.IsNullOrWhiteSpace(query))
            return request;

        // 检测正则表达式模式 (以 / 开头和结尾)
        if (query.StartsWith("/") && query.EndsWith("/") && query.Length > 2)
        {
            try
            {
                request.RegEx = new Regex(query[1..^1], RegexOptions.IgnoreCase | RegexOptions.Compiled);
                return request;
            }
            catch
            {
                // 正则无效，继续普通解析
            }
        }

        // 检测元数据 Token (0x 开头)
        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            request.Keywords = new[] { query };
            return request;
        }

        // 解析高级语法
        var terms = SplitSearchTerms(query);
        var normalTerms = new List<string>();

        foreach (var term in terms)
        {
            if (string.IsNullOrEmpty(term)) continue;

            switch (term[0])
            {
                case '+':
                    if (term.Length > 1)
                        request.MustContain.Add(term[1..]);
                    break;
                case '-':
                    if (term.Length > 1)
                        request.MustExclude.Add(term[1..]);
                    break;
                case '=':
                    if (term.Length > 1)
                        request.ExactTerms.Add(term[1..]);
                    break;
                case '~':
                    if (term.Length > 1)
                        request.FuzzyTerms.Add(term[1..]);
                    break;
                case '"':
                    // 引号包裹的字符串字面量
                    if (term.Length > 2 && term.EndsWith("\""))
                        normalTerms.Add(term[1..^1]);
                    else
                        normalTerms.Add(term);
                    break;
                default:
                    normalTerms.Add(term);
                    break;
            }
        }

        // 合并必须包含项到普通关键词
        normalTerms.AddRange(request.MustContain);
        request.Keywords = normalTerms.ToArray();

        return request;
    }

    private static List<string> SplitSearchTerms(string query)
    {
        var terms = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (char c in query)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    terms.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            terms.Add(current.ToString());

        return terms;
    }
}

#endregion

#region 搜索结果

/// <summary>
/// 搜索结果项
/// </summary>
public record SearchResultItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Type { get; init; }  // type, method, field, property, event, literal, etc.
    public string? DeclaringType { get; init; }
    public string? Namespace { get; init; }
    public string? AssemblyName { get; init; }
    public int? ILOffset { get; init; }
    public string? Value { get; init; }  // For literals
    public double Relevance { get; init; }  // 相关性评分
}

/// <summary>
/// 搜索结果集
/// </summary>
public class EnhancedSearchResult
{
    public required IReadOnlyList<SearchResultItem> Items { get; init; }
    public int TotalCount { get; init; }
    public bool HasMore { get; init; }
    public TimeSpan SearchDuration { get; init; }
    public string? Query { get; init; }
    public SearchMode Mode { get; init; }
}

#endregion

#region 搜索策略接口

/// <summary>
/// 搜索策略接口
/// </summary>
public interface ISearchStrategy
{
    /// <summary>
    /// 执行搜索
    /// </summary>
    void Search(
        ModuleDefinition module,
        SearchRequest request,
        ConcurrentBag<SearchResultItem> results,
        CancellationToken cancellationToken);

    /// <summary>
    /// 策略支持的搜索模式
    /// </summary>
    IEnumerable<SearchMode> SupportedModes { get; }
}

/// <summary>
/// 搜索策略基类
/// </summary>
public abstract class AbstractSearchStrategy : ISearchStrategy
{
    protected readonly MemberIdGenerator IdGenerator;

    protected AbstractSearchStrategy(Guid mvid)
    {
        IdGenerator = new MemberIdGenerator(mvid);
    }

    public abstract void Search(
        ModuleDefinition module,
        SearchRequest request,
        ConcurrentBag<SearchResultItem> results,
        CancellationToken cancellationToken);

    public abstract IEnumerable<SearchMode> SupportedModes { get; }

    /// <summary>
    /// 检查名称是否匹配搜索请求
    /// </summary>
    protected bool IsMatch(string name, SearchRequest request)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // 正则匹配
        if (request.RegEx != null)
            return request.RegEx.IsMatch(name);

        // 排除项检查
        foreach (var exclude in request.MustExclude)
        {
            if (name.Contains(exclude, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 精确匹配项检查
        foreach (var exact in request.ExactTerms)
        {
            var compareName = name;
            var tickIndex = name.IndexOf('`');
            if (tickIndex > 0)
                compareName = name[..tickIndex];

            if (!string.Equals(compareName, exact, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 模糊匹配检查
        foreach (var fuzzy in request.FuzzyTerms)
        {
            if (!IsFuzzyMatch(name.ToLowerInvariant(), fuzzy.ToLowerInvariant()))
                return false;
        }

        // 普通关键字匹配 (所有关键字都必须匹配)
        foreach (var keyword in request.Keywords)
        {
            if (string.IsNullOrEmpty(keyword)) continue;
            if (!name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return request.Keywords.Length > 0 || 
               request.ExactTerms.Count > 0 || 
               request.FuzzyTerms.Count > 0;
    }

    /// <summary>
    /// 模糊匹配（非连续字符匹配）
    /// </summary>
    protected static bool IsFuzzyMatch(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            return false;

        if (pattern.Length > text.Length)
            return false;

        int textIndex = 0;
        for (int patternIndex = 0; patternIndex < pattern.Length; patternIndex++)
        {
            var found = false;
            while (textIndex < text.Length)
            {
                if (text[textIndex] == pattern[patternIndex])
                {
                    found = true;
                    textIndex++;
                    break;
                }
                textIndex++;
            }
            if (!found) return false;
        }
        return true;
    }

    /// <summary>
    /// 计算相关性评分
    /// </summary>
    protected static double CalculateRelevance(string name, SearchRequest request)
    {
        double score = 1.0;

        // 精确匹配得分最高
        foreach (var exact in request.ExactTerms)
        {
            if (string.Equals(name, exact, StringComparison.OrdinalIgnoreCase))
                score *= 2.0;
        }

        // 前缀匹配加分
        foreach (var keyword in request.Keywords)
        {
            if (name.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                score *= 1.5;
        }

        // 名称长度影响（短名称优先）
        score *= Math.Max(0.5, 1.0 - (name.Length / 100.0));

        return score;
    }

    /// <summary>
    /// 命名空间过滤
    /// </summary>
    protected static bool PassesNamespaceFilter(string? typeNamespace, SearchRequest request)
    {
        if (string.IsNullOrEmpty(request.NamespaceFilter))
            return true;

        return typeNamespace?.StartsWith(request.NamespaceFilter, StringComparison.OrdinalIgnoreCase) == true;
    }
}

#endregion

#region 具体策略实现

/// <summary>
/// 类型搜索策略
/// </summary>
public class TypeSearchStrategy : AbstractSearchStrategy
{
    public TypeSearchStrategy(Guid mvid) : base(mvid) { }

    public override IEnumerable<SearchMode> SupportedModes => new[] 
    { 
        SearchMode.TypeAndMember, 
        SearchMode.Type 
    };

    public override void Search(
        ModuleDefinition module,
        SearchRequest request,
        ConcurrentBag<SearchResultItem> results,
        CancellationToken cancellationToken)
    {
        foreach (var type in GetAllTypes(module))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (results.Count >= request.Limit)
                return;

            if (!PassesNamespaceFilter(type.Namespace, request))
                continue;

            if (IsMatch(type.Name, request) || IsMatch(type.FullName, request))
            {
                results.Add(new SearchResultItem
                {
                    Id = IdGenerator.GenerateForType(type),
                    Name = type.Name,
                    FullName = type.FullName,
                    Type = GetTypeKind(type),
                    Namespace = type.Namespace,
                    AssemblyName = module.Assembly?.Name?.Name,
                    Relevance = CalculateRelevance(type.Name, request)
                });
            }
        }
    }

    private static IEnumerable<TypeDefinition> GetAllTypes(ModuleDefinition module)
    {
        foreach (var type in module.Types)
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type))
                yield return nested;
        }
    }

    private static IEnumerable<TypeDefinition> GetNestedTypes(TypeDefinition type)
    {
        foreach (var nested in type.NestedTypes)
        {
            yield return nested;
            foreach (var deepNested in GetNestedTypes(nested))
                yield return deepNested;
        }
    }

    private static string GetTypeKind(TypeDefinition type)
    {
        if (type.IsInterface) return "interface";
        if (type.IsEnum) return "enum";
        if (type.IsValueType && !type.IsEnum) return "struct";
        if (IsDelegateType(type)) return "delegate";
        return "class";
    }

    private static bool IsDelegateType(TypeDefinition type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.FullName == "System.MulticastDelegate" ||
                baseType.FullName == "System.Delegate")
                return true;
            try
            {
                baseType = baseType.Resolve()?.BaseType;
            }
            catch
            {
                break;
            }
        }
        return false;
    }
}

/// <summary>
/// 成员搜索策略
/// </summary>
public class MemberSearchStrategy : AbstractSearchStrategy
{
    public MemberSearchStrategy(Guid mvid) : base(mvid) { }

    public override IEnumerable<SearchMode> SupportedModes => new[]
    {
        SearchMode.TypeAndMember,
        SearchMode.Member,
        SearchMode.Method,
        SearchMode.Field,
        SearchMode.Property,
        SearchMode.Event
    };

    public override void Search(
        ModuleDefinition module,
        SearchRequest request,
        ConcurrentBag<SearchResultItem> results,
        CancellationToken cancellationToken)
    {
        foreach (var type in GetAllTypes(module))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (results.Count >= request.Limit)
                return;

            if (!PassesNamespaceFilter(type.Namespace, request))
                continue;

            // 搜索方法
            if (request.Mode == SearchMode.TypeAndMember || 
                request.Mode == SearchMode.Member || 
                request.Mode == SearchMode.Method)
            {
                foreach (var method in type.Methods)
                {
                    if (IsMatch(method.Name, request))
                    {
                        results.Add(new SearchResultItem
                        {
                            Id = IdGenerator.GenerateForMethod(method),
                            Name = method.Name,
                            FullName = $"{type.FullName}.{method.Name}",
                            Type = "method",
                            DeclaringType = type.FullName,
                            Namespace = type.Namespace,
                            Relevance = CalculateRelevance(method.Name, request)
                        });
                    }
                }
            }

            // 搜索字段
            if (request.Mode == SearchMode.TypeAndMember || 
                request.Mode == SearchMode.Member || 
                request.Mode == SearchMode.Field)
            {
                foreach (var field in type.Fields)
                {
                    if (IsMatch(field.Name, request))
                    {
                        results.Add(new SearchResultItem
                        {
                            Id = IdGenerator.GenerateForField(field),
                            Name = field.Name,
                            FullName = $"{type.FullName}.{field.Name}",
                            Type = "field",
                            DeclaringType = type.FullName,
                            Namespace = type.Namespace,
                            Relevance = CalculateRelevance(field.Name, request)
                        });
                    }
                }
            }

            // 搜索属性
            if (request.Mode == SearchMode.TypeAndMember || 
                request.Mode == SearchMode.Member || 
                request.Mode == SearchMode.Property)
            {
                foreach (var prop in type.Properties)
                {
                    if (IsMatch(prop.Name, request))
                    {
                        results.Add(new SearchResultItem
                        {
                            Id = IdGenerator.GenerateForProperty(prop),
                            Name = prop.Name,
                            FullName = $"{type.FullName}.{prop.Name}",
                            Type = "property",
                            DeclaringType = type.FullName,
                            Namespace = type.Namespace,
                            Relevance = CalculateRelevance(prop.Name, request)
                        });
                    }
                }
            }

            // 搜索事件
            if (request.Mode == SearchMode.TypeAndMember || 
                request.Mode == SearchMode.Member || 
                request.Mode == SearchMode.Event)
            {
                foreach (var evt in type.Events)
                {
                    if (IsMatch(evt.Name, request))
                    {
                        results.Add(new SearchResultItem
                        {
                            Id = IdGenerator.GenerateForEvent(evt),
                            Name = evt.Name,
                            FullName = $"{type.FullName}.{evt.Name}",
                            Type = "event",
                            DeclaringType = type.FullName,
                            Namespace = type.Namespace,
                            Relevance = CalculateRelevance(evt.Name, request)
                        });
                    }
                }
            }
        }
    }

    private static IEnumerable<TypeDefinition> GetAllTypes(ModuleDefinition module)
    {
        foreach (var type in module.Types)
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type))
                yield return nested;
        }
    }

    private static IEnumerable<TypeDefinition> GetNestedTypes(TypeDefinition type)
    {
        foreach (var nested in type.NestedTypes)
        {
            yield return nested;
            foreach (var deepNested in GetNestedTypes(nested))
                yield return deepNested;
        }
    }
}

/// <summary>
/// 字面量搜索策略
/// </summary>
public class LiteralSearchStrategy : AbstractSearchStrategy
{
    public LiteralSearchStrategy(Guid mvid) : base(mvid) { }

    public override IEnumerable<SearchMode> SupportedModes => new[] { SearchMode.Literal };

    public override void Search(
        ModuleDefinition module,
        SearchRequest request,
        ConcurrentBag<SearchResultItem> results,
        CancellationToken cancellationToken)
    {
        // 解析查询为字面量值
        var searchValue = request.Keywords.FirstOrDefault() ?? request.RawQuery;
        var parsedValue = TryParseValue(searchValue);

        foreach (var type in module.Types)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (results.Count >= request.Limit)
                return;

            // 搜索方法体中的字面量
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                foreach (var instruction in method.Body.Instructions)
                {
                    if (results.Count >= request.Limit)
                        return;

                    var literalValue = GetLiteralValue(instruction);
                    if (literalValue == null) continue;

                    bool matches = false;
                    string? valueStr = literalValue.ToString();

                    // 字符串匹配
                    if (literalValue is string strValue)
                    {
                        if (request.RegEx != null)
                            matches = request.RegEx.IsMatch(strValue);
                        else
                            matches = strValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase);
                    }
                    // 数值匹配
                    else if (parsedValue != null && literalValue.GetType() == parsedValue.GetType())
                    {
                        matches = literalValue.Equals(parsedValue);
                    }
                    // 数值字符串匹配
                    else if (valueStr != null)
                    {
                        matches = valueStr.Contains(searchValue, StringComparison.OrdinalIgnoreCase);
                    }

                    if (matches)
                    {
                        results.Add(new SearchResultItem
                        {
                            Id = $"{IdGenerator.GenerateForMethod(method)}:IL_{instruction.Offset:X4}",
                            Name = $"IL_{instruction.Offset:X4}",
                            FullName = $"{type.FullName}.{method.Name}:IL_{instruction.Offset:X4}",
                            Type = "literal",
                            DeclaringType = type.FullName,
                            Namespace = type.Namespace,
                            ILOffset = instruction.Offset,
                            Value = valueStr,
                            Relevance = 1.0
                        });
                    }
                }
            }

            // 搜索常量字段
            foreach (var field in type.Fields)
            {
                if (!field.HasConstant) continue;

                var constant = field.Constant;
                if (constant == null) continue;

                bool matches = false;
                string? valueStr = constant.ToString();

                if (constant is string strValue)
                {
                    matches = strValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase);
                }
                else if (valueStr != null)
                {
                    matches = valueStr.Contains(searchValue, StringComparison.OrdinalIgnoreCase);
                }

                if (matches)
                {
                    results.Add(new SearchResultItem
                    {
                        Id = IdGenerator.GenerateForField(field),
                        Name = field.Name,
                        FullName = $"{type.FullName}.{field.Name}",
                        Type = "constant",
                        DeclaringType = type.FullName,
                        Namespace = type.Namespace,
                        Value = valueStr,
                        Relevance = 1.5  // 常量优先级更高
                    });
                }
            }
        }
    }

    private static object? GetLiteralValue(Instruction instruction)
    {
        return instruction.OpCode.Code switch
        {
            Code.Ldstr => instruction.Operand as string,
            Code.Ldc_I4 or Code.Ldc_I4_S => instruction.Operand,
            Code.Ldc_I4_0 => 0,
            Code.Ldc_I4_1 => 1,
            Code.Ldc_I4_2 => 2,
            Code.Ldc_I4_3 => 3,
            Code.Ldc_I4_4 => 4,
            Code.Ldc_I4_5 => 5,
            Code.Ldc_I4_6 => 6,
            Code.Ldc_I4_7 => 7,
            Code.Ldc_I4_8 => 8,
            Code.Ldc_I4_M1 => -1,
            Code.Ldc_I8 => instruction.Operand,
            Code.Ldc_R4 => instruction.Operand,
            Code.Ldc_R8 => instruction.Operand,
            _ => null
        };
    }

    private static object? TryParseValue(string input)
    {
        if (int.TryParse(input, out var intVal)) return intVal;
        if (long.TryParse(input, out var longVal)) return longVal;
        if (double.TryParse(input, out var doubleVal)) return doubleVal;
        if (float.TryParse(input, out var floatVal)) return floatVal;
        return null;
    }
}

/// <summary>
/// 元数据 Token 搜索策略
/// </summary>
public class TokenSearchStrategy : AbstractSearchStrategy
{
    public TokenSearchStrategy(Guid mvid) : base(mvid) { }

    public override IEnumerable<SearchMode> SupportedModes => new[] { SearchMode.Token };

    public override void Search(
        ModuleDefinition module,
        SearchRequest request,
        ConcurrentBag<SearchResultItem> results,
        CancellationToken cancellationToken)
    {
        var tokenStr = request.Keywords.FirstOrDefault() ?? request.RawQuery;
        if (!tokenStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return;

        if (!uint.TryParse(tokenStr[2..], System.Globalization.NumberStyles.HexNumber, null, out var token))
            return;

        cancellationToken.ThrowIfCancellationRequested();

        // 搜索类型
        foreach (var type in module.Types)
        {
            if (type.MetadataToken.ToUInt32() == token)
            {
                results.Add(new SearchResultItem
                {
                    Id = IdGenerator.GenerateForType(type),
                    Name = type.Name,
                    FullName = type.FullName,
                    Type = "type",
                    Namespace = type.Namespace,
                    Value = $"0x{token:X8}",
                    Relevance = 2.0
                });
                return;
            }

            // 搜索成员
            foreach (var method in type.Methods)
            {
                if (method.MetadataToken.ToUInt32() == token)
                {
                    results.Add(new SearchResultItem
                    {
                        Id = IdGenerator.GenerateForMethod(method),
                        Name = method.Name,
                        FullName = $"{type.FullName}.{method.Name}",
                        Type = "method",
                        DeclaringType = type.FullName,
                        Value = $"0x{token:X8}",
                        Relevance = 2.0
                    });
                    return;
                }
            }

            foreach (var field in type.Fields)
            {
                if (field.MetadataToken.ToUInt32() == token)
                {
                    results.Add(new SearchResultItem
                    {
                        Id = IdGenerator.GenerateForField(field),
                        Name = field.Name,
                        FullName = $"{type.FullName}.{field.Name}",
                        Type = "field",
                        DeclaringType = type.FullName,
                        Value = $"0x{token:X8}",
                        Relevance = 2.0
                    });
                    return;
                }
            }
        }
    }
}

#endregion

#region 增强搜索服务

/// <summary>
/// 增强搜索服务 - 统一搜索入口
/// </summary>
public class EnhancedSearchService
{
    private readonly List<ISearchStrategy> _strategies = new();
    private readonly Guid _mvid;

    public EnhancedSearchService(Guid mvid)
    {
        _mvid = mvid;
        
        // 注册默认策略
        _strategies.Add(new TypeSearchStrategy(mvid));
        _strategies.Add(new MemberSearchStrategy(mvid));
        _strategies.Add(new LiteralSearchStrategy(mvid));
        _strategies.Add(new TokenSearchStrategy(mvid));
    }

    /// <summary>
    /// 执行搜索
    /// </summary>
    public EnhancedSearchResult Search(
        ModuleDefinition module,
        string query,
        SearchMode mode = SearchMode.TypeAndMember,
        string? namespaceFilter = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 自动检测搜索模式
        if (mode == SearchMode.TypeAndMember)
        {
            mode = DetectSearchMode(query);
        }

        var request = SearchRequest.Parse(query, mode, limit);
        request.Keywords = request.Keywords.Length > 0 ? request.Keywords : new[] { query };

        if (!string.IsNullOrEmpty(namespaceFilter))
        {
            // 创建新的 request 对象以包含命名空间过滤
            request = new SearchRequest
            {
                RawQuery = request.RawQuery,
                Mode = request.Mode,
                Keywords = request.Keywords,
                RegEx = request.RegEx,
                UseRegex = request.UseRegex,
                ExactMatch = request.ExactMatch,
                CaseSensitive = request.CaseSensitive,
                NamespaceFilter = namespaceFilter,
                Limit = limit,
                MustContain = request.MustContain,
                MustExclude = request.MustExclude,
                ExactTerms = request.ExactTerms,
                FuzzyTerms = request.FuzzyTerms
            };
        }

        var results = new ConcurrentBag<SearchResultItem>();

        // 选择合适的策略并行执行
        var applicableStrategies = _strategies
            .Where(s => s.SupportedModes.Contains(mode))
            .ToList();

        Parallel.ForEach(applicableStrategies, strategy =>
        {
            try
            {
                strategy.Search(module, request, results, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 忽略取消
            }
        });

        stopwatch.Stop();

        // 按相关性排序并限制结果数量
        var sortedResults = results
            .OrderByDescending(r => r.Relevance)
            .ThenBy(r => r.Name.Length)
            .Take(limit)
            .ToList();

        return new EnhancedSearchResult
        {
            Items = sortedResults,
            TotalCount = results.Count,
            HasMore = results.Count > limit,
            SearchDuration = stopwatch.Elapsed,
            Query = query,
            Mode = mode
        };
    }

    /// <summary>
    /// 快速类型搜索
    /// </summary>
    public EnhancedSearchResult SearchTypes(
        ModuleDefinition module,
        string query,
        string? namespaceFilter = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return Search(module, query, SearchMode.Type, namespaceFilter, limit, cancellationToken);
    }

    /// <summary>
    /// 快速成员搜索
    /// </summary>
    public EnhancedSearchResult SearchMembers(
        ModuleDefinition module,
        string query,
        string? namespaceFilter = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return Search(module, query, SearchMode.Member, namespaceFilter, limit, cancellationToken);
    }

    /// <summary>
    /// 字面量搜索
    /// </summary>
    public EnhancedSearchResult SearchLiterals(
        ModuleDefinition module,
        string query,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return Search(module, query, SearchMode.Literal, null, limit, cancellationToken);
    }

    /// <summary>
    /// 自动检测搜索模式
    /// </summary>
    private static SearchMode DetectSearchMode(string query)
    {
        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return SearchMode.Token;

        if (query.StartsWith("\"") && query.EndsWith("\""))
            return SearchMode.Literal;

        // 如果查询看起来像数字
        if (double.TryParse(query, out _))
            return SearchMode.Literal;

        return SearchMode.TypeAndMember;
    }

    /// <summary>
    /// 注册自定义策略
    /// </summary>
    public void RegisterStrategy(ISearchStrategy strategy)
    {
        _strategies.Add(strategy);
    }
}

#endregion
