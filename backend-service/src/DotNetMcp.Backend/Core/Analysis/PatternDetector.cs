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

            // 检测原型模式
            if (IsPrototype(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Prototype,
                    TypeName = type.FullName,
                    Confidence = CalculatePrototypeConfidence(type),
                    Evidence = GetPrototypeEvidence(type)
                });
            }

            // 检测桥接模式
            if (IsBridge(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Bridge,
                    TypeName = type.FullName,
                    Confidence = CalculateBridgeConfidence(type),
                    Evidence = GetBridgeEvidence(type)
                });
            }

            // 检测组合模式
            if (IsComposite(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Composite,
                    TypeName = type.FullName,
                    Confidence = CalculateCompositeConfidence(type),
                    Evidence = GetCompositeEvidence(type)
                });
            }

            // 检测装饰器模式
            if (IsDecorator(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Decorator,
                    TypeName = type.FullName,
                    Confidence = CalculateDecoratorConfidence(type),
                    Evidence = GetDecoratorEvidence(type)
                });
            }

            // 检测外观模式
            if (IsFacade(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Facade,
                    TypeName = type.FullName,
                    Confidence = CalculateFacadeConfidence(type),
                    Evidence = GetFacadeEvidence(type)
                });
            }

            // 检测享元模式
            if (IsFlyweight(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Flyweight,
                    TypeName = type.FullName,
                    Confidence = CalculateFlyweightConfidence(type),
                    Evidence = GetFlyweightEvidence(type)
                });
            }

            // 检测代理模式
            if (IsProxy(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Proxy,
                    TypeName = type.FullName,
                    Confidence = CalculateProxyConfidence(type),
                    Evidence = GetProxyEvidence(type)
                });
            }

            // 检测策略模式
            if (IsStrategy(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Strategy,
                    TypeName = type.FullName,
                    Confidence = CalculateStrategyConfidence(type),
                    Evidence = GetStrategyEvidence(type)
                });
            }

            // 检测命令模式
            if (IsCommand(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Command,
                    TypeName = type.FullName,
                    Confidence = CalculateCommandConfidence(type),
                    Evidence = GetCommandEvidence(type)
                });
            }

            // 检测状态模式
            if (IsState(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.State,
                    TypeName = type.FullName,
                    Confidence = CalculateStateConfidence(type),
                    Evidence = GetStateEvidence(type)
                });
            }

            // 检测模板方法模式
            if (IsTemplateMethod(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.TemplateMethod,
                    TypeName = type.FullName,
                    Confidence = CalculateTemplateMethodConfidence(type),
                    Evidence = GetTemplateMethodEvidence(type)
                });
            }

            // 检测访问者模式
            if (IsVisitor(type))
            {
                result.Patterns.Add(new DetectedPattern
                {
                    PatternType = DesignPattern.Visitor,
                    TypeName = type.FullName,
                    Confidence = CalculateVisitorConfidence(type),
                    Evidence = GetVisitorEvidence(type)
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

    #region Prototype Pattern Detection

    private bool IsPrototype(TypeDefinition type)
    {
        if (type.IsAbstract || type.IsInterface) return false;

        // 检查是否实现了 ICloneable 或有 Clone 方法
        var implementsICloneable = type.Interfaces.Any(i =>
            i.InterfaceType.FullName == "System.ICloneable");

        var hasCloneMethod = type.Methods.Any(m =>
            m.Name == "Clone" && m.Parameters.Count == 0 &&
            (m.ReturnType.FullName == "System.Object" || m.ReturnType.FullName == type.FullName));

        return implementsICloneable || hasCloneMethod;
    }

    private double CalculatePrototypeConfidence(TypeDefinition type)
    {
        double confidence = 0.4;

        if (type.Interfaces.Any(i => i.InterfaceType.FullName == "System.ICloneable"))
            confidence += 0.4;

        if (type.Methods.Any(m => m.Name == "Clone"))
            confidence += 0.2;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetPrototypeEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        if (type.Interfaces.Any(i => i.InterfaceType.FullName == "System.ICloneable"))
            evidence.Add("Implements ICloneable");

        var cloneMethod = type.Methods.FirstOrDefault(m => m.Name == "Clone");
        if (cloneMethod != null)
            evidence.Add($"Clone method returns {cloneMethod.ReturnType.Name}");

        return evidence;
    }

    #endregion

    #region Bridge Pattern Detection

    private bool IsBridge(TypeDefinition type)
    {
        if (type.IsInterface) return false;

        // 桥接模式：抽象类持有接口引用
        if (!type.IsAbstract) return false;

        var interfaceFields = type.Fields.Where(f =>
            f.FieldType.Resolve()?.IsInterface == true).ToList();

        return interfaceFields.Count >= 1;
    }

    private double CalculateBridgeConfidence(TypeDefinition type)
    {
        double confidence = 0.4;

        var interfaceFields = type.Fields.Where(f =>
            f.FieldType.Resolve()?.IsInterface == true).ToList();

        if (interfaceFields.Count >= 1)
            confidence += 0.3;

        if (type.Name.Contains("Bridge") || type.Name.Contains("Abstraction"))
            confidence += 0.3;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetBridgeEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();
        evidence.Add("Abstract class");

        var interfaceFields = type.Fields.Where(f =>
            f.FieldType.Resolve()?.IsInterface == true).Take(2).ToList();

        foreach (var field in interfaceFields)
        {
            evidence.Add($"Interface field: {field.Name} ({field.FieldType.Name})");
        }

        return evidence;
    }

    #endregion

    #region Composite Pattern Detection

    private bool IsComposite(TypeDefinition type)
    {
        if (type.IsInterface) return false;

        // 组合模式：类持有自身类型的集合
        var selfCollectionFields = type.Fields.Where(f =>
        {
            var fieldType = f.FieldType;
            if (fieldType is GenericInstanceType git)
            {
                return git.GenericArguments.Any(a => a.FullName == type.FullName);
            }
            return fieldType.FullName.Contains($"<{type.FullName}>");
        }).ToList();

        return selfCollectionFields.Count >= 1;
    }

    private double CalculateCompositeConfidence(TypeDefinition type)
    {
        double confidence = 0.5;

        if (type.Name.Contains("Composite") || type.Name.Contains("Component"))
            confidence += 0.3;

        // 检查是否有 Add/Remove 方法
        if (type.Methods.Any(m => m.Name == "Add" || m.Name == "Remove"))
            confidence += 0.2;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetCompositeEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();
        evidence.Add("Contains collection of same type");

        if (type.Methods.Any(m => m.Name == "Add"))
            evidence.Add("Has Add method");
        if (type.Methods.Any(m => m.Name == "Remove"))
            evidence.Add("Has Remove method");

        return evidence;
    }

    #endregion

    #region Decorator Pattern Detection

    private bool IsDecorator(TypeDefinition type)
    {
        if (type.IsAbstract || type.IsInterface) return false;
        if (type.BaseType == null) return false;

        // 装饰器：继承基类且持有同类型字段
        var baseTypeName = type.BaseType.FullName;
        var hasWrappedField = type.Fields.Any(f =>
            f.FieldType.FullName == baseTypeName ||
            type.Interfaces.Any(i => i.InterfaceType.FullName == f.FieldType.FullName));

        return hasWrappedField && type.Interfaces.Count >= 1;
    }

    private double CalculateDecoratorConfidence(TypeDefinition type)
    {
        double confidence = 0.4;

        if (type.Name.Contains("Decorator") || type.Name.Contains("Wrapper"))
            confidence += 0.3;

        if (type.BaseType != null && !type.BaseType.FullName.StartsWith("System."))
            confidence += 0.2;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetDecoratorEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        if (type.BaseType != null)
            evidence.Add($"Extends {type.BaseType.Name}");

        var wrappedFields = type.Fields.Where(f =>
            type.Interfaces.Any(i => i.InterfaceType.FullName == f.FieldType.FullName)).Take(2).ToList();

        foreach (var field in wrappedFields)
        {
            evidence.Add($"Wrapped field: {field.Name}");
        }

        return evidence;
    }

    #endregion

    #region Facade Pattern Detection

    private bool IsFacade(TypeDefinition type)
    {
        if (type.IsAbstract || type.IsInterface) return false;

        // 外观模式：持有多个不同类型的私有字段且提供简化接口
        var nonPrimitiveFields = type.Fields.Where(f =>
            f.IsPrivate &&
            !f.FieldType.IsPrimitive &&
            !f.FieldType.FullName.StartsWith("System.")).ToList();

        // 至少持有3个不同类型的对象
        var distinctTypes = nonPrimitiveFields.Select(f => f.FieldType.FullName).Distinct().Count();
        return distinctTypes >= 3;
    }

    private double CalculateFacadeConfidence(TypeDefinition type)
    {
        double confidence = 0.4;

        var nonPrimitiveFields = type.Fields.Where(f =>
            f.IsPrivate && !f.FieldType.IsPrimitive).ToList();

        if (nonPrimitiveFields.Count >= 5)
            confidence += 0.3;

        if (type.Name.Contains("Facade") || type.Name.Contains("Service") || type.Name.Contains("Manager"))
            confidence += 0.3;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetFacadeEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        var nonPrimitiveFields = type.Fields.Where(f =>
            f.IsPrivate && !f.FieldType.IsPrimitive).ToList();

        evidence.Add($"Aggregates {nonPrimitiveFields.Count} subsystem(s)");

        foreach (var field in nonPrimitiveFields.Take(3))
        {
            evidence.Add($"Subsystem: {field.FieldType.Name}");
        }

        return evidence;
    }

    #endregion

    #region Flyweight Pattern Detection

    private bool IsFlyweight(TypeDefinition type)
    {
        // 享元工厂：有静态字典缓存实例
        var hasCacheField = type.Fields.Any(f =>
            f.IsStatic &&
            (f.FieldType.FullName.Contains("Dictionary") ||
             f.FieldType.FullName.Contains("ConcurrentDictionary")));

        var hasGetMethod = type.Methods.Any(m =>
            m.IsStatic &&
            (m.Name.StartsWith("Get") || m.Name.StartsWith("Create")) &&
            m.ReturnType.FullName != "System.Void");

        return hasCacheField && hasGetMethod;
    }

    private double CalculateFlyweightConfidence(TypeDefinition type)
    {
        double confidence = 0.5;

        if (type.Name.Contains("Flyweight") || type.Name.Contains("Pool") || type.Name.Contains("Cache"))
            confidence += 0.3;

        if (type.Fields.Any(f => f.IsStatic && f.FieldType.FullName.Contains("Dictionary")))
            confidence += 0.2;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetFlyweightEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        var cacheField = type.Fields.FirstOrDefault(f =>
            f.IsStatic && f.FieldType.FullName.Contains("Dictionary"));
        if (cacheField != null)
            evidence.Add($"Cache field: {cacheField.Name}");

        var getMethod = type.Methods.FirstOrDefault(m =>
            m.IsStatic && m.Name.StartsWith("Get"));
        if (getMethod != null)
            evidence.Add($"Factory method: {getMethod.Name}()");

        return evidence;
    }

    #endregion

    #region Proxy Pattern Detection

    private bool IsProxy(TypeDefinition type)
    {
        if (type.IsAbstract || type.IsInterface) return false;

        // 代理：实现接口且持有同接口类型的字段
        if (type.Interfaces.Count == 0) return false;

        var proxyFields = type.Fields.Where(f =>
            type.Interfaces.Any(i => i.InterfaceType.FullName == f.FieldType.FullName)).ToList();

        return proxyFields.Count >= 1;
    }

    private double CalculateProxyConfidence(TypeDefinition type)
    {
        double confidence = 0.5;

        if (type.Name.Contains("Proxy") || type.Name.EndsWith("Impl"))
            confidence += 0.3;

        if (type.Interfaces.Count >= 1)
            confidence += 0.2;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetProxyEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        foreach (var iface in type.Interfaces.Take(2))
        {
            evidence.Add($"Implements {iface.InterfaceType.Name}");
        }

        var proxyFields = type.Fields.Where(f =>
            type.Interfaces.Any(i => i.InterfaceType.FullName == f.FieldType.FullName)).Take(2).ToList();

        foreach (var field in proxyFields)
        {
            evidence.Add($"Proxied field: {field.Name}");
        }

        return evidence;
    }

    #endregion

    #region Strategy Pattern Detection

    private bool IsStrategy(TypeDefinition type)
    {
        // 策略接口：只有一个抽象方法的接口
        if (!type.IsInterface) return false;

        var abstractMethods = type.Methods.Where(m => m.IsAbstract && !m.IsSpecialName).ToList();
        return abstractMethods.Count >= 1 && abstractMethods.Count <= 3;
    }

    private double CalculateStrategyConfidence(TypeDefinition type)
    {
        double confidence = 0.4;

        if (type.Name.Contains("Strategy") || type.Name.EndsWith("able"))
            confidence += 0.4;

        var abstractMethods = type.Methods.Where(m => m.IsAbstract).ToList();
        if (abstractMethods.Count == 1)
            confidence += 0.2;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetStrategyEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();
        evidence.Add("Interface type");

        var abstractMethods = type.Methods.Where(m => m.IsAbstract && !m.IsSpecialName).ToList();
        evidence.Add($"{abstractMethods.Count} strategy method(s)");

        foreach (var method in abstractMethods.Take(2))
        {
            evidence.Add($"Method: {method.Name}()");
        }

        return evidence;
    }

    #endregion

    #region Command Pattern Detection

    private bool IsCommand(TypeDefinition type)
    {
        // 命令模式：有 Execute 方法
        var hasExecuteMethod = type.Methods.Any(m =>
            m.Name == "Execute" || m.Name == "Run" || m.Name == "Invoke");

        var isCommandLike = type.Name.Contains("Command") ||
                           type.Name.Contains("Action") ||
                           type.Name.EndsWith("Handler");

        return hasExecuteMethod || isCommandLike;
    }

    private double CalculateCommandConfidence(TypeDefinition type)
    {
        double confidence = 0.3;

        if (type.Methods.Any(m => m.Name == "Execute"))
            confidence += 0.4;

        if (type.Name.Contains("Command"))
            confidence += 0.3;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetCommandEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        var executeMethod = type.Methods.FirstOrDefault(m =>
            m.Name == "Execute" || m.Name == "Run" || m.Name == "Invoke");
        if (executeMethod != null)
            evidence.Add($"Execute method: {executeMethod.Name}()");

        if (type.Name.Contains("Command"))
            evidence.Add("Name contains 'Command'");

        return evidence;
    }

    #endregion

    #region State Pattern Detection

    private bool IsState(TypeDefinition type)
    {
        // 状态模式：抽象类或接口，有处理方法
        if (!type.IsAbstract && !type.IsInterface) return false;

        var hasHandleMethod = type.Methods.Any(m =>
            m.Name.StartsWith("Handle") || m.Name.StartsWith("Process") ||
            m.Name.StartsWith("On") || m.Name == "Enter" || m.Name == "Exit");

        return hasHandleMethod || type.Name.Contains("State");
    }

    private double CalculateStateConfidence(TypeDefinition type)
    {
        double confidence = 0.4;

        if (type.Name.Contains("State"))
            confidence += 0.4;

        if (type.Methods.Any(m => m.Name == "Enter" || m.Name == "Exit"))
            confidence += 0.2;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetStateEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        if (type.IsAbstract)
            evidence.Add("Abstract class");
        else if (type.IsInterface)
            evidence.Add("Interface");

        var stateMethods = type.Methods.Where(m =>
            m.Name == "Enter" || m.Name == "Exit" || m.Name.StartsWith("Handle")).Take(3).ToList();

        foreach (var method in stateMethods)
        {
            evidence.Add($"State method: {method.Name}()");
        }

        return evidence;
    }

    #endregion

    #region TemplateMethod Pattern Detection

    private bool IsTemplateMethod(TypeDefinition type)
    {
        if (!type.IsAbstract) return false;

        // 模板方法：抽象类有虚方法和抽象方法
        var hasAbstractMethods = type.Methods.Any(m => m.IsAbstract);
        var hasVirtualMethods = type.Methods.Any(m => m.IsVirtual && !m.IsAbstract);

        return hasAbstractMethods && hasVirtualMethods;
    }

    private double CalculateTemplateMethodConfidence(TypeDefinition type)
    {
        double confidence = 0.5;

        var abstractMethods = type.Methods.Count(m => m.IsAbstract);
        var virtualMethods = type.Methods.Count(m => m.IsVirtual && !m.IsAbstract);

        if (abstractMethods >= 2)
            confidence += 0.2;
        if (virtualMethods >= 1)
            confidence += 0.2;

        if (type.Name.Contains("Template") || type.Name.Contains("Base"))
            confidence += 0.1;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetTemplateMethodEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();
        evidence.Add("Abstract class");

        var abstractMethods = type.Methods.Where(m => m.IsAbstract).Take(2).ToList();
        foreach (var method in abstractMethods)
        {
            evidence.Add($"Abstract step: {method.Name}()");
        }

        var virtualMethods = type.Methods.Where(m => m.IsVirtual && !m.IsAbstract).Take(1).ToList();
        foreach (var method in virtualMethods)
        {
            evidence.Add($"Template method: {method.Name}()");
        }

        return evidence;
    }

    #endregion

    #region Visitor Pattern Detection

    private bool IsVisitor(TypeDefinition type)
    {
        // 访问者：有多个 Visit 方法
        var visitMethods = type.Methods.Where(m =>
            m.Name.StartsWith("Visit") && m.Parameters.Count >= 1).ToList();

        return visitMethods.Count >= 2;
    }

    private double CalculateVisitorConfidence(TypeDefinition type)
    {
        double confidence = 0.4;

        var visitMethods = type.Methods.Where(m => m.Name.StartsWith("Visit")).ToList();
        if (visitMethods.Count >= 3)
            confidence += 0.3;
        if (visitMethods.Count >= 5)
            confidence += 0.2;

        if (type.Name.Contains("Visitor"))
            confidence += 0.1;

        return Math.Min(confidence, 1.0);
    }

    private List<string> GetVisitorEvidence(TypeDefinition type)
    {
        var evidence = new List<string>();

        var visitMethods = type.Methods.Where(m => m.Name.StartsWith("Visit")).Take(3).ToList();
        evidence.Add($"{visitMethods.Count} Visit method(s)");

        foreach (var method in visitMethods)
        {
            var paramType = method.Parameters.FirstOrDefault()?.ParameterType.Name ?? "?";
            evidence.Add($"Visit({paramType})");
        }

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
