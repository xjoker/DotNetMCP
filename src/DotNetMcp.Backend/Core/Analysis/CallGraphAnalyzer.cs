using Mono.Cecil;
using Mono.Cecil.Cil;
using DotNetMcp.Backend.Core.Identity;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 调用图分析器 - 构建方法调用关系图
/// </summary>
public class CallGraphAnalyzer
{
    private readonly ModuleDefinition _module;
    private readonly MemberIdGenerator _idGenerator;

    public CallGraphAnalyzer(ModuleDefinition module, Guid mvid)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _idGenerator = new MemberIdGenerator(mvid);
    }

    /// <summary>
    /// 构建完整调用图
    /// </summary>
    public CallGraph BuildCallGraph()
    {
        var graph = new CallGraph();

        foreach (var type in _module.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                var callerId = _idGenerator.GenerateForMethod(method);
                var callerName = $"{type.FullName}.{method.Name}";

                var node = graph.GetOrCreateNode(callerId, callerName);

                foreach (var instruction in method.Body.Instructions)
                {
                    if (IsCallInstruction(instruction) && instruction.Operand is MethodReference targetRef)
                    {
                        var targetName = $"{targetRef.DeclaringType.FullName}.{targetRef.Name}";
                        
                        // 尝试解析为本模块的方法
                        var targetMethod = TryResolve(targetRef);
                        var targetId = targetMethod != null
                            ? _idGenerator.GenerateForMethod(targetMethod)
                            : $"external:{targetName}";

                        var targetNode = graph.GetOrCreateNode(targetId, targetName);
                        
                        var edge = new CallEdge
                        {
                            CallerId = callerId,
                            CalleeId = targetId,
                            ILOffset = instruction.Offset,
                            CallType = GetCallType(instruction.OpCode),
                            IsExternal = targetMethod == null
                        };

                        node.OutgoingCalls.Add(edge);
                        targetNode.IncomingCalls.Add(edge);
                    }
                }
            }
        }

        return graph;
    }

    /// <summary>
    /// 获取方法的直接调用者
    /// </summary>
    public IReadOnlyList<CallGraphNode> GetCallers(MethodDefinition method)
    {
        var graph = BuildCallGraph();
        var methodId = _idGenerator.GenerateForMethod(method);
        var node = graph.GetNode(methodId);

        if (node == null) return Array.Empty<CallGraphNode>();

        return node.IncomingCalls
            .Select(e => graph.GetNode(e.CallerId))
            .Where(n => n != null)
            .Cast<CallGraphNode>()
            .ToList();
    }

    /// <summary>
    /// 获取方法直接调用的其他方法
    /// </summary>
    public IReadOnlyList<CallGraphNode> GetCallees(MethodDefinition method)
    {
        var graph = BuildCallGraph();
        var methodId = _idGenerator.GenerateForMethod(method);
        var node = graph.GetNode(methodId);

        if (node == null) return Array.Empty<CallGraphNode>();

        return node.OutgoingCalls
            .Select(e => graph.GetNode(e.CalleeId))
            .Where(n => n != null)
            .Cast<CallGraphNode>()
            .ToList();
    }

    /// <summary>
    /// 获取调用链深度分析
    /// </summary>
    public CallChainResult GetCallChain(MethodDefinition startMethod, int maxDepth = 5)
    {
        var graph = BuildCallGraph();
        var startId = _idGenerator.GenerateForMethod(startMethod);
        var visited = new HashSet<string>();
        var chain = new List<CallChainLevel>();

        BuildChainRecursive(graph, startId, 0, maxDepth, visited, chain);

        return new CallChainResult
        {
            StartMethodId = startId,
            StartMethodName = $"{startMethod.DeclaringType.FullName}.{startMethod.Name}",
            Levels = chain,
            MaxDepthReached = chain.Count >= maxDepth
        };
    }

    private void BuildChainRecursive(
        CallGraph graph, string nodeId, int depth, int maxDepth,
        HashSet<string> visited, List<CallChainLevel> chain)
    {
        if (depth >= maxDepth || visited.Contains(nodeId)) return;

        visited.Add(nodeId);
        var node = graph.GetNode(nodeId);
        if (node == null) return;

        var level = chain.Count <= depth
            ? new CallChainLevel { Depth = depth, Methods = new List<string>() }
            : chain[depth];

        if (chain.Count <= depth) chain.Add(level);
        level.Methods.Add(node.MethodName);

        foreach (var edge in node.OutgoingCalls)
        {
            BuildChainRecursive(graph, edge.CalleeId, depth + 1, maxDepth, visited, chain);
        }
    }

    private static bool IsCallInstruction(Instruction instruction)
    {
        return instruction.OpCode == OpCodes.Call ||
               instruction.OpCode == OpCodes.Callvirt ||
               instruction.OpCode == OpCodes.Newobj;
    }

    private static CallType GetCallType(OpCode opCode)
    {
        if (opCode == OpCodes.Callvirt) return CallType.Virtual;
        if (opCode == OpCodes.Newobj) return CallType.Constructor;
        return CallType.Static;
    }

    private MethodDefinition? TryResolve(MethodReference methodRef)
    {
        try
        {
            return methodRef.Resolve();
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 调用图
/// </summary>
public class CallGraph
{
    private readonly Dictionary<string, CallGraphNode> _nodes = new();

    public CallGraphNode GetOrCreateNode(string id, string name)
    {
        if (!_nodes.TryGetValue(id, out var node))
        {
            node = new CallGraphNode
            {
                MethodId = id,
                MethodName = name,
                IncomingCalls = new List<CallEdge>(),
                OutgoingCalls = new List<CallEdge>()
            };
            _nodes[id] = node;
        }
        return node;
    }

    public CallGraphNode? GetNode(string id)
        => _nodes.TryGetValue(id, out var node) ? node : null;

    public int NodeCount => _nodes.Count;
    public int EdgeCount => _nodes.Values.Sum(n => n.OutgoingCalls.Count);

    public IReadOnlyList<CallGraphNode> GetAllNodes() => _nodes.Values.ToList();
}

/// <summary>
/// 调用图节点
/// </summary>
public record CallGraphNode
{
    public required string MethodId { get; init; }
    public required string MethodName { get; init; }
    public required List<CallEdge> IncomingCalls { get; init; }
    public required List<CallEdge> OutgoingCalls { get; init; }
}

/// <summary>
/// 调用边
/// </summary>
public record CallEdge
{
    public required string CallerId { get; init; }
    public required string CalleeId { get; init; }
    public required int ILOffset { get; init; }
    public required CallType CallType { get; init; }
    public required bool IsExternal { get; init; }
}

/// <summary>
/// 调用类型
/// </summary>
public enum CallType
{
    Static,
    Virtual,
    Constructor
}

/// <summary>
/// 调用链结果
/// </summary>
public record CallChainResult
{
    public required string StartMethodId { get; init; }
    public required string StartMethodName { get; init; }
    public required IReadOnlyList<CallChainLevel> Levels { get; init; }
    public bool MaxDepthReached { get; init; }
}

/// <summary>
/// 调用链层级
/// </summary>
public record CallChainLevel
{
    public int Depth { get; init; }
    public required List<string> Methods { get; init; }
}
