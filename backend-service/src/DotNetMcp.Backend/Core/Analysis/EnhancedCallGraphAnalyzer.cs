using Mono.Cecil;
using Mono.Cecil.Cil;
using DotNetMcp.Backend.Core.Identity;
using System.Collections.Concurrent;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 增强调用图分析器 - 支持委托、反射、Lambda 分析
/// </summary>
public class EnhancedCallGraphAnalyzer
{
    private readonly ModuleDefinition _module;
    private readonly MemberIdGenerator _idGenerator;
    private readonly ConcurrentDictionary<string, EnhancedCallGraphNode> _nodeCache = new();

    public EnhancedCallGraphAnalyzer(ModuleDefinition module, Guid mvid)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _idGenerator = new MemberIdGenerator(mvid);
    }

    #region 增强调用图构建

    /// <summary>
    /// 构建增强调用图（包含委托、反射、虚方法解析）
    /// </summary>
    public EnhancedCallGraph BuildCallGraph(CancellationToken cancellationToken = default)
    {
        var graph = new EnhancedCallGraph();
        _nodeCache.Clear();

        // 并行处理所有类型
        var allTypes = GetAllTypes(_module).ToList();

        Parallel.ForEach(allTypes, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, type =>
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                AnalyzeMethod(method, graph, cancellationToken);
            }
        });

        // 构建虚方法解析映射
        ResolveVirtualCalls(graph, cancellationToken);

        return graph;
    }

    private void AnalyzeMethod(MethodDefinition method, EnhancedCallGraph graph, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var callerId = _idGenerator.GenerateForMethod(method);
        var callerName = $"{method.DeclaringType.FullName}.{method.Name}";
        var callerNode = graph.GetOrCreateNode(callerId, callerName);

        MethodReference? pendingDelegateTarget = null;

        foreach (var instruction in method.Body.Instructions)
        {
            ct.ThrowIfCancellationRequested();

            // 直接方法调用
            if (IsDirectCallInstruction(instruction) && instruction.Operand is MethodReference directTarget)
            {
                AddCallEdge(graph, callerNode, directTarget, instruction, EnhancedCallType.Direct);
            }

            // 虚方法调用
            else if (instruction.OpCode == OpCodes.Callvirt && instruction.Operand is MethodReference virtualTarget)
            {
                var edge = AddCallEdge(graph, callerNode, virtualTarget, instruction, EnhancedCallType.Virtual);
                edge.RequiresResolution = true;
            }

            // 构造函数调用
            else if (instruction.OpCode == OpCodes.Newobj && instruction.Operand is MethodReference ctorTarget)
            {
                AddCallEdge(graph, callerNode, ctorTarget, instruction, EnhancedCallType.Constructor);
            }

            // 委托创建 (ldftn + newobj Delegate)
            else if (instruction.OpCode == OpCodes.Ldftn && instruction.Operand is MethodReference delegateMethod)
            {
                pendingDelegateTarget = delegateMethod;
            }
            else if (instruction.OpCode == OpCodes.Ldvirtftn && instruction.Operand is MethodReference virtDelegateMethod)
            {
                pendingDelegateTarget = virtDelegateMethod;
            }
            else if (instruction.OpCode == OpCodes.Newobj && instruction.Operand is MethodReference ctorRef2 
                     && IsDelegate(ctorRef2.DeclaringType))
            {
                if (pendingDelegateTarget != null)
                {
                    var edge = AddCallEdge(graph, callerNode, pendingDelegateTarget, instruction, EnhancedCallType.Delegate);
                    edge.DelegateType = ctorRef2.DeclaringType?.FullName;
                    pendingDelegateTarget = null;
                }
            }

            // Lambda / 匿名方法 (编译器生成的类)
            else if (IsLambdaInstantiation(instruction))
            {
                var lambdaMethod = TryResolveLambdaMethod(instruction);
                if (lambdaMethod != null)
                {
                    var edge = AddCallEdge(graph, callerNode, lambdaMethod, instruction, EnhancedCallType.Lambda);
                    edge.IsCompilerGenerated = true;
                }
            }

            // 反射调用检测
            else if (IsReflectionCall(instruction))
            {
                var reflectionInfo = AnalyzeReflectionCall(method, instruction);
                if (reflectionInfo != null)
                {
                    graph.ReflectionCalls.Add(new ReflectionCallInfo
                    {
                        CallerId = callerId,
                        CallerMethod = callerName,
                        ILOffset = instruction.Offset,
                        ReflectionPattern = reflectionInfo.Pattern,
                        TargetTypeName = reflectionInfo.TargetType,
                        TargetMemberName = reflectionInfo.TargetMember,
                        IsResolved = reflectionInfo.IsResolved
                    });

                    // 如果能解析到具体方法，添加边
                    if (reflectionInfo.ResolvedMethod != null)
                    {
                        var edge = AddCallEdge(graph, callerNode, reflectionInfo.ResolvedMethod, instruction, EnhancedCallType.Reflection);
                        edge.ReflectionPattern = reflectionInfo.Pattern;
                    }
                }
            }
        }
    }

    private EnhancedCallEdge AddCallEdge(
        EnhancedCallGraph graph,
        EnhancedCallGraphNode caller,
        MethodReference target,
        Instruction instruction,
        EnhancedCallType callType)
    {
        var targetName = $"{target.DeclaringType?.FullName ?? "?"}.{target.Name}";
        var targetMethod = TryResolve(target);
        var targetId = targetMethod != null
            ? _idGenerator.GenerateForMethod(targetMethod)
            : $"external:{targetName}";

        var targetNode = graph.GetOrCreateNode(targetId, targetName);

        var edge = new EnhancedCallEdge
        {
            CallerId = caller.MethodId,
            CalleeId = targetId,
            CallerName = caller.MethodName,
            CalleeName = targetName,
            ILOffset = instruction.Offset,
            CallType = callType,
            IsExternal = targetMethod == null,
            OpCode = instruction.OpCode.Name
        };

        lock (caller.OutgoingCalls)
        {
            caller.OutgoingCalls.Add(edge);
        }
        lock (targetNode.IncomingCalls)
        {
            targetNode.IncomingCalls.Add(edge);
        }
        lock (graph.AllEdges)
        {
            graph.AllEdges.Add(edge);
        }

        return edge;
    }

    #endregion

    #region 虚方法解析

    private void ResolveVirtualCalls(EnhancedCallGraph graph, CancellationToken ct)
    {
        var virtualEdges = graph.AllEdges.Where(e => e.RequiresResolution).ToList();
        var typeHierarchy = BuildTypeHierarchy();

        foreach (var edge in virtualEdges)
        {
            ct.ThrowIfCancellationRequested();

            // 查找所有可能的实现
            var implementations = FindPossibleImplementations(edge.CalleeName, typeHierarchy);
            edge.PossibleTargets = implementations;
            edge.IsPolymorphic = implementations.Count > 1;
        }
    }

    private Dictionary<string, List<string>> BuildTypeHierarchy()
    {
        var hierarchy = new Dictionary<string, List<string>>();

        foreach (var type in GetAllTypes(_module))
        {
            if (type.BaseType != null)
            {
                var baseName = type.BaseType.FullName;
                if (!hierarchy.ContainsKey(baseName))
                    hierarchy[baseName] = new List<string>();
                hierarchy[baseName].Add(type.FullName);
            }

            foreach (var iface in type.Interfaces)
            {
                var ifaceName = iface.InterfaceType.FullName;
                if (!hierarchy.ContainsKey(ifaceName))
                    hierarchy[ifaceName] = new List<string>();
                hierarchy[ifaceName].Add(type.FullName);
            }
        }

        return hierarchy;
    }

    private List<string> FindPossibleImplementations(string methodFullName, Dictionary<string, List<string>> hierarchy)
    {
        var implementations = new List<string>();

        // 解析方法名
        var lastDot = methodFullName.LastIndexOf('.');
        if (lastDot < 0) return implementations;

        var typeName = methodFullName[..lastDot];
        var methodName = methodFullName[(lastDot + 1)..];

        // 查找所有派生类型
        if (hierarchy.TryGetValue(typeName, out var derivedTypes))
        {
            foreach (var derivedTypeName in derivedTypes)
            {
                var derivedType = _module.Types.FirstOrDefault(t => t.FullName == derivedTypeName);
                if (derivedType == null) continue;

                var overrideMethod = derivedType.Methods.FirstOrDefault(m => 
                    m.Name == methodName && m.IsVirtual);
                if (overrideMethod != null)
                {
                    implementations.Add($"{derivedTypeName}.{methodName}");
                }
            }
        }

        return implementations;
    }

    #endregion

    #region 反射调用分析

    private bool IsReflectionCall(Instruction instruction)
    {
        if (instruction.OpCode != OpCodes.Callvirt && instruction.OpCode != OpCodes.Call)
            return false;

        if (instruction.Operand is not MethodReference methodRef)
            return false;

        var typeName = methodRef.DeclaringType?.FullName ?? "";
        var methodName = methodRef.Name;

        // 常见反射调用模式
        return (typeName == "System.Type" && methodName is "GetMethod" or "GetProperty" or "GetField" or "GetMember" or "InvokeMember") ||
               (typeName == "System.Reflection.MethodInfo" && methodName == "Invoke") ||
               (typeName == "System.Reflection.MethodBase" && methodName == "Invoke") ||
               (typeName == "System.Reflection.FieldInfo" && methodName is "GetValue" or "SetValue") ||
               (typeName == "System.Reflection.PropertyInfo" && methodName is "GetValue" or "SetValue") ||
               (typeName == "System.Activator" && methodName is "CreateInstance" or "CreateInstanceFrom");
    }

    private ReflectionAnalysisResult? AnalyzeReflectionCall(MethodDefinition method, Instruction callInstruction)
    {
        if (callInstruction.Operand is not MethodReference methodRef)
            return null;

        var pattern = $"{methodRef.DeclaringType?.Name}.{methodRef.Name}";
        string? targetType = null;
        string? targetMember = null;
        bool isResolved = false;
        MethodReference? resolvedMethod = null;

        // 尝试回溯查找字符串参数
        var prev = callInstruction.Previous;
        while (prev != null)
        {
            if (prev.OpCode == OpCodes.Ldstr && prev.Operand is string str)
            {
                if (targetMember == null)
                    targetMember = str;
                else
                    targetType = str;
            }
            else if (prev.OpCode == OpCodes.Ldtoken && prev.Operand is TypeReference typeRef)
            {
                targetType = typeRef.FullName;
            }

            // 最多回溯 10 条指令
            if (callInstruction.Offset - prev.Offset > 50) break;
            prev = prev.Previous;
        }

        // 尝试解析到具体方法
        if (!string.IsNullOrEmpty(targetType) && !string.IsNullOrEmpty(targetMember))
        {
            var type = _module.Types.FirstOrDefault(t => t.FullName == targetType || t.Name == targetType);
            if (type != null)
            {
                var resolvedDef = type.Methods.FirstOrDefault(m => m.Name == targetMember);
                if (resolvedDef != null)
                {
                    resolvedMethod = resolvedDef;
                    isResolved = true;
                }
            }
        }

        return new ReflectionAnalysisResult
        {
            Pattern = pattern,
            TargetType = targetType,
            TargetMember = targetMember,
            IsResolved = isResolved,
            ResolvedMethod = resolvedMethod
        };
    }

    #endregion

    #region Lambda 分析

    private static bool IsLambdaInstantiation(Instruction instruction)
    {
        if (instruction.OpCode != OpCodes.Newobj)
            return false;

        if (instruction.Operand is not MethodReference ctorRef)
            return false;

        var typeName = ctorRef.DeclaringType?.Name ?? "";
        
        // 编译器生成的 Lambda 类名通常包含 <>c__DisplayClass 或类似模式
        return typeName.Contains("<>c__DisplayClass") ||
               typeName.Contains("<>c") ||
               typeName.Contains("d__");
    }

    private MethodReference? TryResolveLambdaMethod(Instruction instruction)
    {
        // 查找前面的 ldftn 指令
        var prev = instruction.Previous;
        while (prev != null)
        {
            if (prev.OpCode == OpCodes.Ldftn && prev.Operand is MethodReference lambdaMethod)
            {
                return lambdaMethod;
            }
            if (instruction.Offset - prev.Offset > 20) break;
            prev = prev.Previous;
        }
        return null;
    }

    #endregion

    #region 递归调用检测

    /// <summary>
    /// 检测递归调用
    /// </summary>
    public List<RecursionInfo> DetectRecursion(EnhancedCallGraph graph)
    {
        var recursions = new List<RecursionInfo>();
        var visited = new HashSet<string>();
        var stack = new Stack<string>();

        foreach (var node in graph.GetAllNodes())
        {
            if (!visited.Contains(node.MethodId))
            {
                DetectRecursionDFS(graph, node.MethodId, visited, stack, recursions);
            }
        }

        return recursions;
    }

    private void DetectRecursionDFS(
        EnhancedCallGraph graph,
        string nodeId,
        HashSet<string> visited,
        Stack<string> stack,
        List<RecursionInfo> recursions)
    {
        visited.Add(nodeId);
        stack.Push(nodeId);

        var node = graph.GetNode(nodeId);
        if (node != null)
        {
            foreach (var edge in node.OutgoingCalls)
            {
                // 直接递归
                if (edge.CalleeId == nodeId)
                {
                    recursions.Add(new RecursionInfo
                    {
                        MethodId = nodeId,
                        MethodName = node.MethodName,
                        RecursionType = RecursionType.Direct,
                        Depth = 0
                    });
                }
                // 间接递归
                else if (stack.Contains(edge.CalleeId))
                {
                    var cycle = stack.TakeWhile(id => id != edge.CalleeId).ToList();
                    cycle.Add(edge.CalleeId);
                    recursions.Add(new RecursionInfo
                    {
                        MethodId = nodeId,
                        MethodName = node.MethodName,
                        RecursionType = RecursionType.Indirect,
                        Depth = cycle.Count,
                        Cycle = cycle
                    });
                }
                else if (!visited.Contains(edge.CalleeId))
                {
                    DetectRecursionDFS(graph, edge.CalleeId, visited, stack, recursions);
                }
            }
        }

        stack.Pop();
    }

    #endregion

    #region 辅助方法

    private static bool IsDirectCallInstruction(Instruction instruction)
    {
        return instruction.OpCode == OpCodes.Call;
    }

    private static bool IsDelegate(TypeReference? type)
    {
        if (type == null) return false;

        try
        {
            var resolved = type.Resolve();
            if (resolved == null) return false;

            var current = resolved.BaseType;
            while (current != null)
            {
                if (current.FullName == "System.MulticastDelegate" ||
                    current.FullName == "System.Delegate")
                    return true;
                current = current.Resolve()?.BaseType;
            }
        }
        catch
        {
            // 解析失败，检查名称
            return type.FullName.Contains("Delegate") ||
                   type.FullName.Contains("Action") ||
                   type.FullName.Contains("Func");
        }

        return false;
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

    private MethodDefinition? TryResolve(MethodReference methodRef)
    {
        try { return methodRef.Resolve(); }
        catch { return null; }
    }

    #endregion
}

#region 增强调用图模型

/// <summary>
/// 增强调用图
/// </summary>
public class EnhancedCallGraph
{
    private readonly ConcurrentDictionary<string, EnhancedCallGraphNode> _nodes = new();

    public List<EnhancedCallEdge> AllEdges { get; } = new();
    public List<ReflectionCallInfo> ReflectionCalls { get; } = new();

    public EnhancedCallGraphNode GetOrCreateNode(string id, string name)
    {
        return _nodes.GetOrAdd(id, _ => new EnhancedCallGraphNode
        {
            MethodId = id,
            MethodName = name,
            IncomingCalls = new List<EnhancedCallEdge>(),
            OutgoingCalls = new List<EnhancedCallEdge>()
        });
    }

    public EnhancedCallGraphNode? GetNode(string id)
        => _nodes.TryGetValue(id, out var node) ? node : null;

    public int NodeCount => _nodes.Count;
    public int EdgeCount => AllEdges.Count;
    public int ReflectionCallCount => ReflectionCalls.Count;

    public IReadOnlyList<EnhancedCallGraphNode> GetAllNodes() => _nodes.Values.ToList();

    /// <summary>
    /// 获取调用统计
    /// </summary>
    public CallGraphStatistics GetStatistics()
    {
        return new CallGraphStatistics
        {
            TotalNodes = NodeCount,
            TotalEdges = EdgeCount,
            DirectCalls = AllEdges.Count(e => e.CallType == EnhancedCallType.Direct),
            VirtualCalls = AllEdges.Count(e => e.CallType == EnhancedCallType.Virtual),
            DelegateCalls = AllEdges.Count(e => e.CallType == EnhancedCallType.Delegate),
            LambdaCalls = AllEdges.Count(e => e.CallType == EnhancedCallType.Lambda),
            ReflectionCalls = ReflectionCalls.Count,
            ExternalCalls = AllEdges.Count(e => e.IsExternal),
            PolymorphicCalls = AllEdges.Count(e => e.IsPolymorphic)
        };
    }
}

/// <summary>
/// 增强调用图节点
/// </summary>
public class EnhancedCallGraphNode
{
    public required string MethodId { get; init; }
    public required string MethodName { get; init; }
    public required List<EnhancedCallEdge> IncomingCalls { get; init; }
    public required List<EnhancedCallEdge> OutgoingCalls { get; init; }

    public int InDegree => IncomingCalls.Count;
    public int OutDegree => OutgoingCalls.Count;
    public bool IsLeaf => OutgoingCalls.Count == 0;
    public bool IsRoot => IncomingCalls.Count == 0;
}

/// <summary>
/// 增强调用边
/// </summary>
public class EnhancedCallEdge
{
    public required string CallerId { get; init; }
    public required string CalleeId { get; init; }
    public required string CallerName { get; init; }
    public required string CalleeName { get; init; }
    public required int ILOffset { get; init; }
    public required EnhancedCallType CallType { get; init; }
    public required bool IsExternal { get; init; }
    public required string OpCode { get; init; }

    // 虚方法解析
    public bool RequiresResolution { get; set; }
    public List<string>? PossibleTargets { get; set; }
    public bool IsPolymorphic { get; set; }

    // 委托信息
    public string? DelegateType { get; set; }
    public bool IsCompilerGenerated { get; set; }

    // 反射信息
    public string? ReflectionPattern { get; set; }
}

/// <summary>
/// 增强调用类型
/// </summary>
public enum EnhancedCallType
{
    Direct,         // call
    Virtual,        // callvirt
    Constructor,    // newobj
    Delegate,       // 委托调用
    Lambda,         // Lambda 表达式
    Reflection,     // 反射调用
    Interface       // 接口方法调用
}

/// <summary>
/// 反射调用信息
/// </summary>
public class ReflectionCallInfo
{
    public required string CallerId { get; init; }
    public required string CallerMethod { get; init; }
    public required int ILOffset { get; init; }
    public required string ReflectionPattern { get; init; }
    public string? TargetTypeName { get; init; }
    public string? TargetMemberName { get; init; }
    public bool IsResolved { get; init; }
}

/// <summary>
/// 反射分析结果
/// </summary>
internal class ReflectionAnalysisResult
{
    public required string Pattern { get; init; }
    public string? TargetType { get; init; }
    public string? TargetMember { get; init; }
    public bool IsResolved { get; init; }
    public MethodReference? ResolvedMethod { get; init; }
}

/// <summary>
/// 递归信息
/// </summary>
public class RecursionInfo
{
    public required string MethodId { get; init; }
    public required string MethodName { get; init; }
    public required RecursionType RecursionType { get; init; }
    public int Depth { get; init; }
    public List<string>? Cycle { get; init; }
}

/// <summary>
/// 递归类型
/// </summary>
public enum RecursionType
{
    Direct,     // 直接递归
    Indirect    // 间接递归 (A -> B -> A)
}

/// <summary>
/// 调用图统计
/// </summary>
public record CallGraphStatistics
{
    public int TotalNodes { get; init; }
    public int TotalEdges { get; init; }
    public int DirectCalls { get; init; }
    public int VirtualCalls { get; init; }
    public int DelegateCalls { get; init; }
    public int LambdaCalls { get; init; }
    public int ReflectionCalls { get; init; }
    public int ExternalCalls { get; init; }
    public int PolymorphicCalls { get; init; }
}

#endregion
