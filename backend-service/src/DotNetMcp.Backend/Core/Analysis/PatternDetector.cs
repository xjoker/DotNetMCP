using DotNetMcp.Backend.Core.Context;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 设计模式检测器 - 自动识别常见设计模式
/// </summary>
public class PatternDetector
{
    /// <summary>
    /// 检测程序集中的所有设计模式
    /// </summary>
    public PatternDetectionResult DetectPatterns(AssemblyContext context)
    {
        var result = new PatternDetectionResult();

        foreach (var type in context.Assembly!.MainModule.Types)
        {
            // 跳过编译器生成的类型
            if (type.Name.Contains("<") || type.Name.Contains("$"))
                continue;

            // 检测单例模式
            if (IsSingleton(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Singleton,
                    TypeName = type.FullName,
                    Confidence = CalculateSingletonConfidence(type),
                    Evidence = GetSingletonEvidence(type)
                });
            }

            // 检测工厂模式
            if (IsFactory(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Factory,
                    TypeName = type.FullName,
                    Confidence = CalculateFactoryConfidence(type),
                    Evidence = GetFactoryEvidence(type)
                });
            }

            // 检测抽象工厂模式
            if (IsAbstractFactory(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.AbstractFactory,
                    TypeName = type.FullName,
                    Confidence = CalculateAbstractFactoryConfidence(type),
                    Evidence = GetAbstractFactoryEvidence(type)
                });
            }

            // 检测观察者模式
            if (IsObserver(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Observer,
                    TypeName = type.FullName,
                    Confidence = CalculateObserverConfidence(type),
                    Evidence = GetObserverEvidence(type)
                });
            }

            // 检测建造者模式
            if (IsBuilder(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Builder,
                    TypeName = type.FullName,
                    Confidence = CalculateBuilderConfidence(type),
                    Evidence = GetBuilderEvidence(type)
                });
            }

            // 检测适配器模式
            if (IsAdapter(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Adapter,
                    TypeName = type.FullName,
                    Confidence = CalculateAdapterConfidence(type),
                    Evidence = GetAdapterEvidence(type)
                });
            }
        }

        return result;
    }

    #region Singleton Pattern Detection

    private bool IsSingleton(TypeDefinition type)
    {
        if (type.IsAbstract || type.IsInterface) return false;

        // 检查是否有私有静态实例字段
        var instanceField = type.Fields.FirstOrDefault(f =>
            f.IsStatic && f.IsPrivate && f.FieldType.FullName == type.FullName);

        if (instanceField == null) return false;

        // 检查是否有公共静态 Instance 属性或方法
        var hasInstanceAccessor = type.Properties.Any(p =>
            p.Name.Contains("Instance", StringComparison.OrdinalIgnoreCase) &&
            p.GetMethod != null && p.GetMethod.IsStatic && p.GetMethod.IsPublic) ||
            type.Methods.Any(m =>
            m.Name.Contains("Instance", StringComparison.OrdinalIgnoreCase) &&
            m.IsStatic && m.IsPublic && m.ReturnType.FullName == type.FullName);

        // 检查是否有私有构造函数
        var hasPrivateCtor = type.Methods.Any(m =>
            m.IsConstructor && (m.IsPrivate || m.IsAssembly));

        return hasInstanceAccessor && hasPrivateCtor;
    }

    private double CalculateSingletonConfidence(TypeDefinition type)
    {
        double confidence = 0.5;

        // 有私有构造函数 +0.3
        if (type.Methods.Any(m => m.IsConstructor && m.IsPrivate))
            confidence += 0.3;

        // 类是 sealed +0.1
        if (type.IsSealed)
            confidence += 0.1;

        // 有 Instance 字段 +0.1
        if (type.Fields.Any(f => f.Name.Contains("Instance", StringComparison.OrdinalIgnoreCase)))
            confidence += 0.1;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetSingletonEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        var instanceField = type.Fields.FirstOrDefault(f =>
            f.IsStatic && f.FieldType.FullName == type.FullName);
        if (instanceField != null)
            evidence.Add($"Static instance field: {instanceField.Name}");

        var privateCtor = type.Methods.FirstOrDefault(m => m.IsConstructor && m.IsPrivate);
        if (privateCtor != null)
            evidence.Add("Private constructor");

        if (type.IsSealed)
            evidence.Add("Sealed class");

        return evidence;
    }

    #endregion

    #region Factory Pattern Detection

    private bool IsFactory(TypeDefinition type)
    {
        if (type.IsAbstract || type.IsInterface) return false;

        // 检查是否有 Create/Make/Build/New 方法返回其他类型
        var factoryMethods = type.Methods.Where(m =>
            !m.IsConstructor &&
            m.IsPublic &&
            (m.Name.StartsWith("Create") || m.Name.StartsWith("Make") ||
             m.Name.StartsWith("Build") || m.Name.StartsWith("New") ||
             m.Name.Contains("Factory")) &&
            m.ReturnType.FullName != "System.Void" &&
            m.ReturnType.FullName != type.FullName).ToList();

        return factoryMethods.Count >= 1;
    }

    private double CalculateFactoryConfidence(TypeDefinition type)
    {
        double confidence = 0.4;

        var factoryMethods = type.Methods.Where(m =>
            m.Name.StartsWith("Create") || m.Name.Contains("Factory")).ToList();

        // 有多个工厂方法
        if (factoryMethods.Count >= 2)
            confidence += 0.2;
        if (factoryMethods.Count >= 3)
            confidence += 0.1;

        // 类名包含 Factory
        if (type.Name.Contains("Factory"))
            confidence += 0.3;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetFactoryEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        var factoryMethods = type.Methods.Where(m =>
            m.Name.StartsWith("Create") || m.Name.StartsWith("Make")).ToList();

        foreach (var method in factoryMethods.Take(3))
        {
            evidence.Add($"Factory method: {method.Name}() returns {method.ReturnType.Name}");
        }

        if (type.Name.Contains("Factory"))
            evidence.Add("Class name contains 'Factory'");

        return evidence;
    }

    #endregion

    #region Abstract Factory Pattern Detection

    private bool IsAbstractFactory(TypeDefinition type)
    {
        if (!type.IsAbstract && !type.IsInterface) return false;

        // 检查是否有多个抽象 Create 方法
        var createMethods = type.Methods.Where(m =>
            m.IsAbstract &&
            (m.Name.StartsWith("Create") || m.Name.StartsWith("Make"))).ToList();

        return createMethods.Count >= 2;
    }

    private double CalculateAbstractFactoryConfidence(TypeDefinition type)
    {
        double confidence = 0.5;

        var createMethods = type.Methods.Where(m =>
            m.IsAbstract && m.Name.StartsWith("Create")).ToList();

        if (createMethods.Count >= 3)
            confidence += 0.2;

        if (type.Name.Contains("Factory"))
            confidence += 0.3;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetAbstractFactoryEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        var createMethods = type.Methods.Where(m =>
            m.IsAbstract && m.Name.StartsWith("Create")).ToList();

        evidence.Add($"{createMethods.Count} abstract Create methods");

        foreach (var method in createMethods.Take(3))
        {
            evidence.Add($"Abstract method: {method.Name}()");
        }

        return evidence;
    }

    #endregion

    #region Observer Pattern Detection

    private bool IsObserver(TypeDefinition type)
    {
        // 检查是否实现了 IObserver<T> 或有 Update/OnNext 方法
        var implementsIObserver = type.Interfaces.Any(i =>
            i.InterfaceType.FullName.StartsWith("System.IObserver"));

        if (implementsIObserver) return true;

        // 检查是否有事件订阅模式
        var hasEvents = type.Events.Count > 0;
        var hasUpdateMethod = type.Methods.Any(m =>
            m.Name == "Update" || m.Name == "OnNext" || m.Name.StartsWith("On"));

        return hasEvents || hasUpdateMethod;
    }

    private double CalculateObserverConfidence(TypeDefinition type)
    {
        double confidence = 0.3;

        if (type.Interfaces.Any(i => i.InterfaceType.FullName.StartsWith("System.IObserver")))
            confidence += 0.5;

        if (type.Events.Count > 0)
            confidence += 0.2;

        if (type.Methods.Any(m => m.Name == "Update" || m.Name == "OnNext"))
            confidence += 0.2;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetObserverEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        if (type.Interfaces.Any(i => i.InterfaceType.FullName.StartsWith("System.IObserver")))
            evidence.Add("Implements IObserver<T>");

        if (type.Events.Count > 0)
            evidence.Add($"{type.Events.Count} event(s) defined");

        var updateMethods = type.Methods.Where(m =>
            m.Name == "Update" || m.Name == "OnNext").ToList();
        foreach (var method in updateMethods)
        {
            evidence.Add($"Observer method: {method.Name}()");
        }

        return evidence;
    }

    #endregion

    #region Builder Pattern Detection

    private bool IsBuilder(TypeDefinition type)
    {
        if (type.IsAbstract || type.IsInterface) return false;

        // 检查是否有 Build() 方法
        var hasBuildMethod = type.Methods.Any(m =>
            m.Name == "Build" && m.ReturnType.FullName != type.FullName);

        if (!hasBuildMethod) return false;

        // 检查是否有多个 With/Set 方法返回自身（流式 API）
        var fluentMethods = type.Methods.Where(m =>
            !m.IsConstructor &&
            m.IsPublic &&
            (m.Name.StartsWith("With") || m.Name.StartsWith("Set")) &&
            m.ReturnType.FullName == type.FullName).ToList();

        return fluentMethods.Count >= 2;
    }

    private double CalculateBuilderConfidence(TypeDefinition type)
    {
        double confidence = 0.4;

        var fluentMethods = type.Methods.Where(m =>
            m.Name.StartsWith("With") && m.ReturnType.FullName == type.FullName).ToList();

        if (fluentMethods.Count >= 3)
            confidence += 0.2;
        if (fluentMethods.Count >= 5)
            confidence += 0.1;

        if (type.Name.Contains("Builder"))
            confidence += 0.3;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetBuilderEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        var buildMethod = type.Methods.FirstOrDefault(m => m.Name == "Build");
        if (buildMethod != null)
            evidence.Add($"Build method returns {buildMethod.ReturnType.Name}");

        var fluentMethods = type.Methods.Where(m =>
            m.Name.StartsWith("With") && m.ReturnType.FullName == type.FullName).ToList();

        evidence.Add($"{fluentMethods.Count} fluent methods (With*/Set*)");

        if (type.Name.Contains("Builder"))
            evidence.Add("Class name contains 'Builder'");

        return evidence;
    }

    #endregion

    #region Adapter Pattern Detection

    private bool IsAdapter(TypeDefinition type)
    {
        if (type.IsAbstract || type.IsInterface) return false;

        // 检查是否实现了接口且持有另一个类型的实例
        if (type.Interfaces.Count == 0) return false;

        // 检查是否有私有字段引用其他类型
        var adapteeFields = type.Fields.Where(f =>
            f.IsPrivate &&
            !f.FieldType.IsPrimitive &&
            !f.FieldType.FullName.StartsWith("System.")).ToList();

        return adapteeFields.Count >= 1 && type.Interfaces.Count >= 1;
    }

    private double CalculateAdapterConfidence(TypeDefinition type)
    {
        double confidence = 0.3;

        if (type.Interfaces.Count >= 1)
            confidence += 0.3;

        var adapteeFields = type.Fields.Where(f =>
            f.IsPrivate && !f.FieldType.IsPrimitive).ToList();

        if (adapteeFields.Count >= 1)
            confidence += 0.2;

        if (type.Name.Contains("Adapter") || type.Name.Contains("Wrapper"))
            confidence += 0.2;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetAdapterEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        evidence.Add($"Implements {type.Interfaces.Count} interface(s)");

        var adapteeFields = type.Fields.Where(f =>
            f.IsPrivate && !f.FieldType.IsPrimitive).Take(2).ToList();

        foreach (var field in adapteeFields)
        {
            evidence.Add($"Adaptee field: {field.Name} ({field.FieldType.Name})");
        }

        if (type.Name.Contains("Adapter"))
            evidence.Add("Class name contains 'Adapter'");

        return evidence;
    }

    #endregion
}

#region Models

public class PatternDetectionResult
{
    public List<DetectedPattern> Patterns { get; set; } = new();
}

public class DetectedPattern
{
    public DesignPattern PatternType { get; set; }
    public string TypeName { get; set; } = "";
    public double Confidence { get; set; }
    public List<string> Evidence { get; set; } = new();
}

public enum DesignPattern
{
    Singleton,
    Factory,
    AbstractFactory,
    Builder,
    Prototype,
    Adapter,
    Bridge,
    Composite,
    Decorator,
    Facade,
    Flyweight,
    Proxy,
    Observer,
    Strategy,
    Command,
    State,
    TemplateMethod,
    Visitor
}

#endregion
