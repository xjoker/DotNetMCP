using Mono.Cecil;
using Mono.Cecil.Cil;
using DotNetMcp.Backend.Core.Identity;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 设计模式检测器 - 识别常见设计模式
/// </summary>
public class PatternDetector
{
    private readonly ModuleDefinition _module;
    private readonly MemberIdGenerator _idGenerator;

    public PatternDetector(ModuleDefinition module, Guid mvid)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _idGenerator = new MemberIdGenerator(mvid);
    }

    /// <summary>
    /// 检测所有设计模式
    /// </summary>
    public PatternDetectionResult DetectAll()
    {
        var patterns = new List<DetectedPattern>();

        foreach (var type in _module.Types)
        {
            if (type.Name.StartsWith("<")) continue; // 跳过编译器生成的类型

            // 检测单例模式
            var singleton = DetectSingleton(type);
            if (singleton != null) patterns.Add(singleton);

            // 检测工厂模式
            var factories = DetectFactory(type);
            patterns.AddRange(factories);

            // 检测观察者模式
            var observer = DetectObserver(type);
            if (observer != null) patterns.Add(observer);

            // 检测建造者模式
            var builder = DetectBuilder(type);
            if (builder != null) patterns.Add(builder);

            // 检测策略模式
            var strategy = DetectStrategy(type);
            if (strategy != null) patterns.Add(strategy);

            // 检测装饰器模式
            var decorator = DetectDecorator(type);
            if (decorator != null) patterns.Add(decorator);
        }

        return new PatternDetectionResult
        {
            IsSuccess = true,
            Patterns = patterns,
            TotalCount = patterns.Count,
            Summary = patterns
                .GroupBy(p => p.PatternType)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <summary>
    /// 检测单例模式
    /// </summary>
    public DetectedPattern? DetectSingleton(TypeDefinition type)
    {
        // 特征1: 私有构造函数
        var hasPrivateConstructor = type.Methods
            .Where(m => m.IsConstructor && !m.IsStatic)
            .All(m => m.IsPrivate || m.IsFamily);

        if (!hasPrivateConstructor || !type.Methods.Any(m => m.IsConstructor && !m.IsStatic))
            return null;

        // 特征2: 静态字段或属性返回自身类型
        var hasStaticInstance = type.Fields.Any(f =>
            f.IsStatic && f.FieldType.FullName == type.FullName);

        var hasStaticProperty = type.Properties.Any(p =>
            p.GetMethod?.IsStatic == true && p.PropertyType.FullName == type.FullName);

        // 特征3: 静态方法返回自身类型 (GetInstance, Instance, etc.)
        var hasInstanceMethod = type.Methods.Any(m =>
            m.IsStatic && !m.IsConstructor &&
            m.ReturnType.FullName == type.FullName &&
            (m.Name.Contains("Instance", StringComparison.OrdinalIgnoreCase) ||
             m.Name.Contains("GetInstance", StringComparison.OrdinalIgnoreCase) ||
             m.Name == "Get" || m.Name == "Create"));

        if (hasStaticInstance || hasStaticProperty || hasInstanceMethod)
        {
            return new DetectedPattern
            {
                PatternType = "Singleton",
                TypeName = type.FullName,
                TypeId = _idGenerator.GenerateForType(type),
                Confidence = CalculateSingletonConfidence(hasPrivateConstructor, hasStaticInstance, hasStaticProperty, hasInstanceMethod),
                Evidence = new List<string?>
                {
                    hasPrivateConstructor ? "Private constructor" : null,
                    hasStaticInstance ? "Static instance field" : null,
                    hasStaticProperty ? "Static instance property" : null,
                    hasInstanceMethod ? "GetInstance method" : null
                }.Where(e => e != null).Select(e => e!).ToList()
            };
        }

        return null;
    }

    /// <summary>
    /// 检测工厂模式
    /// </summary>
    public List<DetectedPattern> DetectFactory(TypeDefinition type)
    {
        var patterns = new List<DetectedPattern>();

        // 工厂方法特征: Create/Build/Make/Get 开头的静态或实例方法
        var factoryMethods = type.Methods.Where(m =>
            !m.IsConstructor &&
            (m.Name.StartsWith("Create") ||
             m.Name.StartsWith("Build") ||
             m.Name.StartsWith("Make") ||
             m.Name.StartsWith("Get") && m.Name.EndsWith("Instance")) &&
            m.ReturnType.FullName != "System.Void" &&
            m.ReturnType.FullName != type.FullName // 排除单例
        ).ToList();

        if (factoryMethods.Count > 0)
        {
            var returnTypes = factoryMethods
                .Select(m => m.ReturnType.Name)
                .Distinct()
                .ToList();

            patterns.Add(new DetectedPattern
            {
                PatternType = "Factory",
                TypeName = type.FullName,
                TypeId = _idGenerator.GenerateForType(type),
                Confidence = factoryMethods.Count >= 2 ? "High" : "Medium",
                Evidence = factoryMethods.Select(m => $"{m.Name}() -> {m.ReturnType.Name}").ToList(),
                RelatedTypes = returnTypes
            });
        }

        // 抽象工厂特征: 接口或抽象类包含多个 Create 方法
        if ((type.IsInterface || type.IsAbstract) && factoryMethods.Count >= 2)
        {
            patterns.Add(new DetectedPattern
            {
                PatternType = "AbstractFactory",
                TypeName = type.FullName,
                TypeId = _idGenerator.GenerateForType(type),
                Confidence = "High",
                Evidence = new List<string> { $"Abstract type with {factoryMethods.Count} factory methods" }
            });
        }

        return patterns;
    }

    /// <summary>
    /// 检测观察者模式
    /// </summary>
    public DetectedPattern? DetectObserver(TypeDefinition type)
    {
        // 特征1: 包含事件定义
        var events = type.Events.ToList();
        if (events.Count == 0) return null;

        // 特征2: 事件使用 EventHandler 或类似委托
        var eventHandlerEvents = events.Where(e =>
            e.EventType.Name.Contains("EventHandler") ||
            e.EventType.Name.Contains("Action") ||
            e.EventType.Name.EndsWith("Handler")).ToList();

        if (eventHandlerEvents.Count > 0)
        {
            return new DetectedPattern
            {
                PatternType = "Observer",
                TypeName = type.FullName,
                TypeId = _idGenerator.GenerateForType(type),
                Confidence = eventHandlerEvents.Count >= 2 ? "High" : "Medium",
                Evidence = eventHandlerEvents.Select(e => $"event {e.EventType.Name} {e.Name}").ToList()
            };
        }

        // 特征3: Subscribe/Unsubscribe 或 Add/Remove 方法对
        var hasSubscribe = type.Methods.Any(m =>
            m.Name.Contains("Subscribe") || m.Name.Contains("Register") || m.Name.Contains("AddListener"));
        var hasUnsubscribe = type.Methods.Any(m =>
            m.Name.Contains("Unsubscribe") || m.Name.Contains("Unregister") || m.Name.Contains("RemoveListener"));

        if (hasSubscribe && hasUnsubscribe)
        {
            return new DetectedPattern
            {
                PatternType = "Observer",
                TypeName = type.FullName,
                TypeId = _idGenerator.GenerateForType(type),
                Confidence = "Medium",
                Evidence = new List<string> { "Subscribe/Unsubscribe method pair" }
            };
        }

        return null;
    }

    /// <summary>
    /// 检测建造者模式
    /// </summary>
    public DetectedPattern? DetectBuilder(TypeDefinition type)
    {
        // 特征1: 类名包含 Builder
        if (type.Name.EndsWith("Builder"))
        {
            // 特征2: Build 方法
            var buildMethod = type.Methods.FirstOrDefault(m =>
                m.Name == "Build" || m.Name == "Create" || m.Name == "GetResult");

            // 特征3: 流式接口 (返回自身类型的方法)
            var fluentMethods = type.Methods.Where(m =>
                !m.IsConstructor &&
                m.ReturnType.FullName == type.FullName &&
                m.Name.StartsWith("With") || m.Name.StartsWith("Set") || m.Name.StartsWith("Add")).ToList();

            if (buildMethod != null || fluentMethods.Count >= 2)
            {
                return new DetectedPattern
                {
                    PatternType = "Builder",
                    TypeName = type.FullName,
                    TypeId = _idGenerator.GenerateForType(type),
                    Confidence = buildMethod != null && fluentMethods.Count >= 2 ? "High" : "Medium",
                    Evidence = new List<string?>
                    {
                        "Class name ends with 'Builder'",
                        buildMethod != null ? $"Build method: {buildMethod.Name}()" : null,
                        fluentMethods.Count > 0 ? $"{fluentMethods.Count} fluent methods" : null
                    }.Where(e => e != null).Select(e => e!).ToList(),
                    RelatedTypes = buildMethod != null ? new List<string> { buildMethod.ReturnType.Name } : null
                };
            }
        }

        return null;
    }

    /// <summary>
    /// 检测策略模式
    /// </summary>
    public DetectedPattern? DetectStrategy(TypeDefinition type)
    {
        // 特征1: 接口只有一个主要方法
        if (type.IsInterface && type.Methods.Count(m => !m.IsSpecialName) == 1)
        {
            var method = type.Methods.First(m => !m.IsSpecialName);

            // 检查是否有多个实现类
            var implementations = _module.Types
                .Where(t => t.Interfaces.Any(i => i.InterfaceType.FullName == type.FullName))
                .ToList();

            if (implementations.Count >= 2)
            {
                return new DetectedPattern
                {
                    PatternType = "Strategy",
                    TypeName = type.FullName,
                    TypeId = _idGenerator.GenerateForType(type),
                    Confidence = "High",
                    Evidence = new List<string>
                    {
                        $"Single method interface: {method.Name}()",
                        $"{implementations.Count} implementations found"
                    },
                    RelatedTypes = implementations.Select(t => t.FullName).Take(5).ToList()
                };
            }
        }

        return null;
    }

    /// <summary>
    /// 检测装饰器模式
    /// </summary>
    public DetectedPattern? DetectDecorator(TypeDefinition type)
    {
        if (type.IsInterface || type.IsAbstract) return null;

        // 特征1: 实现某个接口
        var interfaces = type.Interfaces.ToList();
        if (interfaces.Count == 0) return null;

        // 特征2: 包含该接口类型的字段 (被装饰对象)
        foreach (var iface in interfaces)
        {
            var wrappedField = type.Fields.FirstOrDefault(f =>
                f.FieldType.FullName == iface.InterfaceType.FullName);

            if (wrappedField != null)
            {
                // 特征3: 构造函数接受该接口类型参数
                var ctorWithInterface = type.Methods.FirstOrDefault(m =>
                    m.IsConstructor &&
                    m.Parameters.Any(p => p.ParameterType.FullName == iface.InterfaceType.FullName));

                if (ctorWithInterface != null)
                {
                    return new DetectedPattern
                    {
                        PatternType = "Decorator",
                        TypeName = type.FullName,
                        TypeId = _idGenerator.GenerateForType(type),
                        Confidence = "High",
                        Evidence = new List<string>
                        {
                            $"Implements {iface.InterfaceType.Name}",
                            $"Contains field of type {iface.InterfaceType.Name}",
                            "Constructor accepts wrapped instance"
                        },
                        RelatedTypes = new List<string> { iface.InterfaceType.FullName }
                    };
                }
            }
        }

        return null;
    }

    private static string CalculateSingletonConfidence(bool privateConstructor, bool staticField, bool staticProperty, bool instanceMethod)
    {
        var score = 0;
        if (privateConstructor) score++;
        if (staticField) score++;
        if (staticProperty) score++;
        if (instanceMethod) score++;

        return score >= 3 ? "High" : score >= 2 ? "Medium" : "Low";
    }
}

#region 数据结构

/// <summary>
/// 模式检测结果
/// </summary>
public record PatternDetectionResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<DetectedPattern>? Patterns { get; init; }
    public int TotalCount { get; init; }
    public Dictionary<string, int>? Summary { get; init; }
}

/// <summary>
/// 检测到的模式
/// </summary>
public record DetectedPattern
{
    public required string PatternType { get; init; }
    public required string TypeName { get; init; }
    public required string TypeId { get; init; }
    public required string Confidence { get; init; }
    public required List<string> Evidence { get; init; }
    public List<string>? RelatedTypes { get; init; }
}

#endregion
