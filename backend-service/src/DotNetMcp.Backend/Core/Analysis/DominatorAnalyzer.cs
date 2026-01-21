using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 支配树分析器 - 计算支配关系、支配边界和控制依赖
/// </summary>
public class DominatorAnalyzer
{
    private readonly ControlFlowGraph _cfg;
    private readonly Dictionary<string, List<string>> _successors;
    private readonly Dictionary<string, List<string>> _predecessors;
    private readonly string _entryBlockId;

    public DominatorAnalyzer(ControlFlowGraph cfg)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));

        _successors = new Dictionary<string, List<string>>();
        _predecessors = new Dictionary<string, List<string>>();

        foreach (var block in cfg.BasicBlocks)
        {
            _successors[block.Id] = new List<string>();
            _predecessors[block.Id] = new List<string>();
        }

        foreach (var edge in cfg.Edges)
        {
            if (_successors.ContainsKey(edge.FromBlock))
                _successors[edge.FromBlock].Add(edge.ToBlock);
            if (_predecessors.ContainsKey(edge.ToBlock))
                _predecessors[edge.ToBlock].Add(edge.FromBlock);
        }

        _entryBlockId = cfg.BasicBlocks.FirstOrDefault()?.Id ?? "";
    }

    #region 支配树计算

    /// <summary>
    /// 计算支配树（使用 Lengauer-Tarjan 算法的简化版）
    /// </summary>
    public DominatorTree ComputeDominatorTree()
    {
        var tree = new DominatorTree();
        var allBlocks = _cfg.BasicBlocks.Select(b => b.Id).ToList();

        // 初始化: 入口节点只被自己支配，其他节点被所有节点支配
        var dom = new Dictionary<string, HashSet<string>>();
        foreach (var block in allBlocks)
        {
            if (block == _entryBlockId)
            {
                dom[block] = new HashSet<string> { block };
            }
            else
            {
                dom[block] = new HashSet<string>(allBlocks);
            }
        }

        // 迭代计算支配关系
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var block in allBlocks)
            {
                if (block == _entryBlockId) continue;

                var preds = _predecessors[block];
                if (preds.Count == 0) continue;

                // Dom(n) = {n} ∪ (∩ Dom(p) for all predecessors p of n)
                var newDom = new HashSet<string>(dom[preds[0]]);
                for (int i = 1; i < preds.Count; i++)
                {
                    newDom.IntersectWith(dom[preds[i]]);
                }
                newDom.Add(block);

                if (!newDom.SetEquals(dom[block]))
                {
                    dom[block] = newDom;
                    changed = true;
                }
            }
        }

        tree.Dominators = dom;

        // 计算直接支配者 (idom)
        var idom = new Dictionary<string, string?>();
        foreach (var block in allBlocks)
        {
            if (block == _entryBlockId)
            {
                idom[block] = null;
                continue;
            }

            // idom(n) 是支配 n 的所有节点中，除了 n 本身，最接近 n 的那个
            var strictDom = dom[block].Where(d => d != block).ToList();
            foreach (var candidate in strictDom)
            {
                // candidate 是 idom 如果它被所有其他 strictDom 成员支配
                bool isIdom = strictDom.All(other => 
                    other == candidate || dom[other].Contains(candidate));
                if (isIdom)
                {
                    idom[block] = candidate;
                    break;
                }
            }
        }

        tree.ImmediateDominators = idom;

        // 构建支配树子节点映射
        var children = new Dictionary<string, List<string>>();
        foreach (var block in allBlocks)
        {
            children[block] = new List<string>();
        }
        foreach (var (block, dominator) in idom)
        {
            if (dominator != null)
            {
                children[dominator].Add(block);
            }
        }
        tree.DominatorTreeChildren = children;

        return tree;
    }

    /// <summary>
    /// 计算后支配树（反向 CFG 的支配树）
    /// </summary>
    public DominatorTree ComputePostDominatorTree()
    {
        // 构建反向 CFG
        var reversePreds = new Dictionary<string, List<string>>(_successors);
        var reverseSuccs = new Dictionary<string, List<string>>(_predecessors);

        // 找出所有出口节点（ret/throw 结束的块）
        var exitBlocks = _cfg.BasicBlocks
            .Where(b => b.TerminatorType == TerminatorType.Return || 
                        b.TerminatorType == TerminatorType.Throw)
            .Select(b => b.Id)
            .ToList();

        // 添加虚拟退出节点
        var virtualExit = "EXIT";
        reverseSuccs[virtualExit] = exitBlocks;
        foreach (var exit in exitBlocks)
        {
            if (!reversePreds.ContainsKey(exit))
                reversePreds[exit] = new List<string>();
            reversePreds[exit].Add(virtualExit);
        }

        var tree = new DominatorTree();
        var allBlocks = _cfg.BasicBlocks.Select(b => b.Id).Append(virtualExit).ToList();

        // 使用反向 CFG 计算支配关系
        var pdom = new Dictionary<string, HashSet<string>>();
        foreach (var block in allBlocks)
        {
            if (block == virtualExit)
            {
                pdom[block] = new HashSet<string> { block };
            }
            else
            {
                pdom[block] = new HashSet<string>(allBlocks);
            }
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var block in allBlocks)
            {
                if (block == virtualExit) continue;

                var succs = reverseSuccs.GetValueOrDefault(block, new List<string>());
                if (succs.Count == 0) continue;

                var newPdom = new HashSet<string>(pdom[succs[0]]);
                for (int i = 1; i < succs.Count; i++)
                {
                    newPdom.IntersectWith(pdom[succs[i]]);
                }
                newPdom.Add(block);

                if (!newPdom.SetEquals(pdom[block]))
                {
                    pdom[block] = newPdom;
                    changed = true;
                }
            }
        }

        tree.Dominators = pdom;
        tree.IsPostDominator = true;

        return tree;
    }

    #endregion

    #region 支配边界

    /// <summary>
    /// 计算支配边界（用于 SSA 构造）
    /// </summary>
    public Dictionary<string, HashSet<string>> ComputeDominanceFrontier(DominatorTree domTree)
    {
        var df = new Dictionary<string, HashSet<string>>();
        foreach (var block in _cfg.BasicBlocks)
        {
            df[block.Id] = new HashSet<string>();
        }

        foreach (var block in _cfg.BasicBlocks)
        {
            var preds = _predecessors[block.Id];
            if (preds.Count < 2) continue;

            foreach (var pred in preds)
            {
                var runner = pred;
                while (runner != null && 
                       runner != domTree.ImmediateDominators.GetValueOrDefault(block.Id))
                {
                    df[runner].Add(block.Id);
                    runner = domTree.ImmediateDominators.GetValueOrDefault(runner);
                }
            }
        }

        return df;
    }

    #endregion

    #region 控制依赖分析

    /// <summary>
    /// 计算控制依赖关系
    /// </summary>
    public Dictionary<string, List<string>> ComputeControlDependence()
    {
        // 控制依赖 = 后支配边界
        var pdomTree = ComputePostDominatorTree();
        var controlDep = new Dictionary<string, List<string>>();

        foreach (var block in _cfg.BasicBlocks)
        {
            controlDep[block.Id] = new List<string>();
        }

        // 使用后支配边界计算控制依赖
        foreach (var block in _cfg.BasicBlocks)
        {
            var succs = _successors[block.Id];
            if (succs.Count < 2) continue;  // 只有条件分支有控制依赖

            foreach (var succ in succs)
            {
                // 从 succ 开始向上走到后支配 block 的节点
                var current = succ;
                var visited = new HashSet<string>();
                while (current != null && !visited.Contains(current))
                {
                    visited.Add(current);

                    // current 控制依赖于 block
                    if (!pdomTree.Dominators.GetValueOrDefault(block.Id)?.Contains(current) ?? true)
                    {
                        controlDep[block.Id].Add(current);
                    }

                    // 移动到后支配者
                    current = pdomTree.ImmediateDominators.GetValueOrDefault(current);
                }
            }
        }

        return controlDep;
    }

    #endregion
}

#region 支配树模型

/// <summary>
/// 支配树
/// </summary>
public class DominatorTree
{
    /// <summary>
    /// 支配关系: Dom[n] = 支配 n 的所有节点
    /// </summary>
    public Dictionary<string, HashSet<string>> Dominators { get; set; } = new();

    /// <summary>
    /// 直接支配者: IDom[n] = n 的直接支配者
    /// </summary>
    public Dictionary<string, string?> ImmediateDominators { get; set; } = new();

    /// <summary>
    /// 支配树子节点
    /// </summary>
    public Dictionary<string, List<string>> DominatorTreeChildren { get; set; } = new();

    /// <summary>
    /// 是否是后支配树
    /// </summary>
    public bool IsPostDominator { get; set; }

    /// <summary>
    /// 检查 a 是否支配 b
    /// </summary>
    public bool Dominates(string a, string b)
    {
        return Dominators.TryGetValue(b, out var doms) && doms.Contains(a);
    }

    /// <summary>
    /// 检查 a 是否严格支配 b
    /// </summary>
    public bool StrictlyDominates(string a, string b)
    {
        return a != b && Dominates(a, b);
    }
}

#endregion

#region 数据流分析

/// <summary>
/// 基本数据流分析器
/// </summary>
public class DataFlowAnalyzer
{
    private readonly ControlFlowGraph _cfg;
    private readonly Dictionary<string, List<string>> _successors;
    private readonly Dictionary<string, List<string>> _predecessors;

    public DataFlowAnalyzer(ControlFlowGraph cfg)
    {
        _cfg = cfg;
        _successors = new Dictionary<string, List<string>>();
        _predecessors = new Dictionary<string, List<string>>();

        foreach (var block in cfg.BasicBlocks)
        {
            _successors[block.Id] = new List<string>();
            _predecessors[block.Id] = new List<string>();
        }

        foreach (var edge in cfg.Edges)
        {
            if (_successors.ContainsKey(edge.FromBlock))
                _successors[edge.FromBlock].Add(edge.ToBlock);
            if (_predecessors.ContainsKey(edge.ToBlock))
                _predecessors[edge.ToBlock].Add(edge.FromBlock);
        }
    }

    /// <summary>
    /// 活跃变量分析 (Liveness Analysis)
    /// </summary>
    public LivenessResult ComputeLiveness(MethodBody body)
    {
        var result = new LivenessResult();

        // 计算每个基本块的 DEF 和 USE 集合
        var def = new Dictionary<string, HashSet<int>>(); // 定义的变量
        var use = new Dictionary<string, HashSet<int>>(); // 使用的变量

        foreach (var block in _cfg.BasicBlocks)
        {
            def[block.Id] = new HashSet<int>();
            use[block.Id] = new HashSet<int>();

            // 分析块内指令
            foreach (var instr in block.Instructions)
            {
                var bodyInstr = body.Instructions.FirstOrDefault(i => i.Offset == instr.Offset);
                if (bodyInstr == null) continue;

                // 获取使用的变量（在定义之前）
                var used = GetUsedVariables(bodyInstr);
                foreach (var v in used)
                {
                    if (!def[block.Id].Contains(v))
                        use[block.Id].Add(v);
                }

                // 获取定义的变量
                var defined = GetDefinedVariables(bodyInstr);
                foreach (var v in defined)
                {
                    def[block.Id].Add(v);
                }
            }
        }

        // 迭代计算 LiveIn 和 LiveOut
        var liveIn = new Dictionary<string, HashSet<int>>();
        var liveOut = new Dictionary<string, HashSet<int>>();

        foreach (var block in _cfg.BasicBlocks)
        {
            liveIn[block.Id] = new HashSet<int>();
            liveOut[block.Id] = new HashSet<int>();
        }

        bool changed = true;
        while (changed)
        {
            changed = false;

            // 反向遍历
            foreach (var block in _cfg.BasicBlocks.AsEnumerable().Reverse())
            {
                // LiveOut(B) = ∪ LiveIn(S) for all successors S
                var newOut = new HashSet<int>();
                foreach (var succ in _successors[block.Id])
                {
                    newOut.UnionWith(liveIn[succ]);
                }

                // LiveIn(B) = Use(B) ∪ (LiveOut(B) - Def(B))
                var newIn = new HashSet<int>(use[block.Id]);
                var outMinusDef = new HashSet<int>(newOut);
                outMinusDef.ExceptWith(def[block.Id]);
                newIn.UnionWith(outMinusDef);

                if (!newIn.SetEquals(liveIn[block.Id]) || !newOut.SetEquals(liveOut[block.Id]))
                {
                    liveIn[block.Id] = newIn;
                    liveOut[block.Id] = newOut;
                    changed = true;
                }
            }
        }

        result.LiveIn = liveIn;
        result.LiveOut = liveOut;
        result.Def = def;
        result.Use = use;

        return result;
    }

    /// <summary>
    /// 到达定义分析 (Reaching Definitions)
    /// </summary>
    public ReachingDefsResult ComputeReachingDefinitions(MethodBody body)
    {
        var result = new ReachingDefsResult();

        // 收集所有定义点
        var allDefs = new List<(string BlockId, int Offset, int Variable)>();

        foreach (var block in _cfg.BasicBlocks)
        {
            foreach (var instr in block.Instructions)
            {
                var bodyInstr = body.Instructions.FirstOrDefault(i => i.Offset == instr.Offset);
                if (bodyInstr == null) continue;

                foreach (var v in GetDefinedVariables(bodyInstr))
                {
                    allDefs.Add((block.Id, instr.Offset, v));
                }
            }
        }

        // GEN 和 KILL 集合
        var gen = new Dictionary<string, HashSet<int>>();  // 块内生成的定义
        var kill = new Dictionary<string, HashSet<int>>(); // 块内杀死的定义

        foreach (var block in _cfg.BasicBlocks)
        {
            gen[block.Id] = new HashSet<int>();
            kill[block.Id] = new HashSet<int>();

            // 找出块内最后一个对每个变量的定义
            var lastDef = new Dictionary<int, int>();
            foreach (var (_, offset, variable) in allDefs.Where(d => d.BlockId == block.Id))
            {
                lastDef[variable] = offset;
            }

            for (int d = 0; d < allDefs.Count; d++)
            {
                var (blkId, offset, variable) = allDefs[d];
                if (blkId == block.Id)
                {
                    if (lastDef[variable] == offset)
                    {
                        gen[block.Id].Add(d);
                    }
                }
                else
                {
                    // KILL 掉其他块中对相同变量的定义
                    if (lastDef.ContainsKey(variable))
                    {
                        kill[block.Id].Add(d);
                    }
                }
            }
        }

        // 迭代计算
        var reachIn = new Dictionary<string, HashSet<int>>();
        var reachOut = new Dictionary<string, HashSet<int>>();

        foreach (var block in _cfg.BasicBlocks)
        {
            reachIn[block.Id] = new HashSet<int>();
            reachOut[block.Id] = new HashSet<int>(gen[block.Id]);
        }

        bool changed = true;
        while (changed)
        {
            changed = false;

            foreach (var block in _cfg.BasicBlocks)
            {
                // ReachIn(B) = ∪ ReachOut(P) for all predecessors P
                var newIn = new HashSet<int>();
                foreach (var pred in _predecessors[block.Id])
                {
                    newIn.UnionWith(reachOut[pred]);
                }

                // ReachOut(B) = Gen(B) ∪ (ReachIn(B) - Kill(B))
                var inMinusKill = new HashSet<int>(newIn);
                inMinusKill.ExceptWith(kill[block.Id]);
                var newOut = new HashSet<int>(gen[block.Id]);
                newOut.UnionWith(inMinusKill);

                if (!newIn.SetEquals(reachIn[block.Id]) || !newOut.SetEquals(reachOut[block.Id]))
                {
                    reachIn[block.Id] = newIn;
                    reachOut[block.Id] = newOut;
                    changed = true;
                }
            }
        }

        result.ReachIn = reachIn;
        result.ReachOut = reachOut;
        result.AllDefinitions = allDefs;

        return result;
    }

    private static IEnumerable<int> GetUsedVariables(Instruction instr)
    {
        if (instr.Operand is VariableDefinition varDef)
        {
            if (instr.OpCode.Code is Code.Ldloc or Code.Ldloc_S or 
                Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3 or
                Code.Ldloca or Code.Ldloca_S)
            {
                yield return varDef.Index;
            }
        }
        else if (instr.OpCode.Code is Code.Ldloc_0)
            yield return 0;
        else if (instr.OpCode.Code is Code.Ldloc_1)
            yield return 1;
        else if (instr.OpCode.Code is Code.Ldloc_2)
            yield return 2;
        else if (instr.OpCode.Code is Code.Ldloc_3)
            yield return 3;
    }

    private static IEnumerable<int> GetDefinedVariables(Instruction instr)
    {
        if (instr.Operand is VariableDefinition varDef)
        {
            if (instr.OpCode.Code is Code.Stloc or Code.Stloc_S or
                Code.Stloc_0 or Code.Stloc_1 or Code.Stloc_2 or Code.Stloc_3)
            {
                yield return varDef.Index;
            }
        }
        else if (instr.OpCode.Code is Code.Stloc_0)
            yield return 0;
        else if (instr.OpCode.Code is Code.Stloc_1)
            yield return 1;
        else if (instr.OpCode.Code is Code.Stloc_2)
            yield return 2;
        else if (instr.OpCode.Code is Code.Stloc_3)
            yield return 3;
    }
}

#region 数据流分析结果

/// <summary>
/// 活跃变量分析结果
/// </summary>
public class LivenessResult
{
    /// <summary>
    /// 块入口处活跃的变量
    /// </summary>
    public Dictionary<string, HashSet<int>> LiveIn { get; set; } = new();

    /// <summary>
    /// 块出口处活跃的变量
    /// </summary>
    public Dictionary<string, HashSet<int>> LiveOut { get; set; } = new();

    /// <summary>
    /// 块内定义的变量
    /// </summary>
    public Dictionary<string, HashSet<int>> Def { get; set; } = new();

    /// <summary>
    /// 块内使用的变量
    /// </summary>
    public Dictionary<string, HashSet<int>> Use { get; set; } = new();
}

/// <summary>
/// 到达定义分析结果
/// </summary>
public class ReachingDefsResult
{
    /// <summary>
    /// 到达块入口的定义
    /// </summary>
    public Dictionary<string, HashSet<int>> ReachIn { get; set; } = new();

    /// <summary>
    /// 到达块出口的定义
    /// </summary>
    public Dictionary<string, HashSet<int>> ReachOut { get; set; } = new();

    /// <summary>
    /// 所有定义点 (BlockId, Offset, Variable)
    /// </summary>
    public List<(string BlockId, int Offset, int Variable)> AllDefinitions { get; set; } = new();
}

#endregion

#endregion
