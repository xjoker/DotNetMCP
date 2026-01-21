using DotNetMcp.Backend.Core.Context;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 控制流图构建器 - 分析方法内的基本块和分支控制流
/// </summary>
public class ControlFlowGraphBuilder
{
    /// <summary>
    /// 构建方法的控制流图
    /// </summary>
    public ControlFlowGraph BuildCFG(AssemblyContext context, string typeName, string methodName)
    {
        var method = FindMethod(context, typeName, methodName);
        if (method == null)
        {
            return new ControlFlowGraph
            {
                MethodName = $"{typeName}.{methodName}",
                Error = $"Method '{typeName}.{methodName}' not found"
            };
        }

        if (!method.HasBody)
        {
            return new ControlFlowGraph
            {
                MethodName = method.FullName,
                Error = "Method has no body (abstract/extern)"
            };
        }

        var cfg = new ControlFlowGraph
        {
            MethodName = method.FullName,
            ReturnType = method.ReturnType.Name
        };

        // 识别基本块领导者 (leaders)
        var leaders = IdentifyLeaders(method.Body);
        
        // 构建基本块
        var blocks = BuildBasicBlocks(method.Body, leaders);
        cfg.BasicBlocks = blocks;

        // 构建边 (控制流转移)
        BuildEdges(cfg, method.Body);

        // 检测异常处理区域
        DetectExceptionRegions(cfg, method.Body);

        // 检测循环
        DetectLoops(cfg);

        return cfg;
    }

    /// <summary>
    /// 检测异常处理区域
    /// </summary>
    private void DetectExceptionRegions(ControlFlowGraph cfg, MethodBody body)
    {
        var blockByOffset = cfg.BasicBlocks.ToDictionary(b => b.StartOffset);
        int regionId = 0;

        foreach (var handler in body.ExceptionHandlers)
        {
            var region = new ExceptionRegion
            {
                Id = $"EH{regionId++}",
                TryStartOffset = handler.TryStart.Offset,
                TryEndOffset = handler.TryEnd?.Offset ?? 0,
                HandlerStartOffset = handler.HandlerStart.Offset,
                HandlerEndOffset = handler.HandlerEnd?.Offset ?? 0,
                FilterStartOffset = handler.FilterStart?.Offset,
                HandlerType = handler.HandlerType switch
                {
                    Mono.Cecil.Cil.ExceptionHandlerType.Catch => ExceptionHandlerType.Catch,
                    Mono.Cecil.Cil.ExceptionHandlerType.Filter => ExceptionHandlerType.Filter,
                    Mono.Cecil.Cil.ExceptionHandlerType.Finally => ExceptionHandlerType.Finally,
                    Mono.Cecil.Cil.ExceptionHandlerType.Fault => ExceptionHandlerType.Fault,
                    _ => ExceptionHandlerType.Catch
                },
                CatchType = handler.CatchType?.FullName
            };

            // 标记 try 块中的基本块
            foreach (var block in cfg.BasicBlocks)
            {
                if (block.StartOffset >= region.TryStartOffset && block.StartOffset < region.TryEndOffset)
                {
                    block.IsInTryBlock = true;
                    region.TryBlocks.Add(block.Id);
                }

                if (block.StartOffset >= region.HandlerStartOffset && block.StartOffset < region.HandlerEndOffset)
                {
                    block.IsExceptionHandler = true;
                    block.HandlerType = region.HandlerType.ToString().ToLowerInvariant();
                    region.HandlerBlocks.Add(block.Id);
                }
            }

            // 添加异常边（从 try 块最后一个基本块到 handler）
            if (region.TryBlocks.Count > 0 && region.HandlerBlocks.Count > 0)
            {
                var lastTryBlock = region.TryBlocks.Last();
                var firstHandlerBlock = region.HandlerBlocks.First();
                
                cfg.Edges.Add(new CFGEdge
                {
                    FromBlock = lastTryBlock,
                    ToBlock = firstHandlerBlock,
                    EdgeType = EdgeType.Exception,
                    Label = region.CatchType ?? region.HandlerType.ToString()
                });
            }

            cfg.ExceptionRegions.Add(region);
        }
    }

    /// <summary>
    /// 使用 Tarjan 算法检测循环（基于强连通分量和回边检测）
    /// </summary>
    private void DetectLoops(ControlFlowGraph cfg)
    {
        if (cfg.BasicBlocks.Count == 0) return;

        var blockById = cfg.BasicBlocks.ToDictionary(b => b.Id);
        var successors = new Dictionary<string, List<string>>();
        var predecessors = new Dictionary<string, List<string>>();

        // 构建邻接表
        foreach (var block in cfg.BasicBlocks)
        {
            successors[block.Id] = new List<string>();
            predecessors[block.Id] = new List<string>();
        }

        foreach (var edge in cfg.Edges)
        {
            if (successors.ContainsKey(edge.FromBlock) && successors.ContainsKey(edge.ToBlock))
            {
                successors[edge.FromBlock].Add(edge.ToBlock);
                predecessors[edge.ToBlock].Add(edge.FromBlock);
            }
        }

        // DFS 计算支配关系和回边
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();
        var dfsOrder = new List<string>();
        var backEdges = new List<(string from, string to)>();

        void DFS(string blockId)
        {
            visited.Add(blockId);
            inStack.Add(blockId);
            dfsOrder.Add(blockId);

            foreach (var succ in successors[blockId])
            {
                if (!visited.Contains(succ))
                {
                    DFS(succ);
                }
                else if (inStack.Contains(succ))
                {
                    // 发现回边
                    backEdges.Add((blockId, succ));
                    
                    // 更新边类型
                    var edge = cfg.Edges.FirstOrDefault(e => 
                        e.FromBlock == blockId && e.ToBlock == succ);
                    if (edge != null)
                    {
                        edge.EdgeType = EdgeType.BackEdge;
                    }
                }
            }

            inStack.Remove(blockId);
        }

        // 从入口开始 DFS
        if (cfg.BasicBlocks.Count > 0)
        {
            DFS(cfg.BasicBlocks[0].Id);
        }

        // 为每个回边构建自然循环
        int loopId = 0;
        var loops = new List<LoopInfo>();

        foreach (var (from, to) in backEdges)
        {
            var loop = new LoopInfo
            {
                LoopId = loopId++,
                HeaderBlockId = to,
                BackEdgeFrom = new List<string> { from }
            };

            // 找出循环体（从 header 可达到 back edge 源的所有节点）
            var loopBody = new HashSet<string> { to };
            var worklist = new Stack<string>();
            worklist.Push(from);

            while (worklist.Count > 0)
            {
                var node = worklist.Pop();
                if (!loopBody.Contains(node))
                {
                    loopBody.Add(node);
                    foreach (var pred in predecessors[node])
                    {
                        if (!loopBody.Contains(pred))
                        {
                            worklist.Push(pred);
                        }
                    }
                }
            }

            loop.BodyBlocks = loopBody.ToList();

            // 标记循环头
            if (blockById.TryGetValue(to, out var headerBlock))
            {
                headerBlock.IsLoopHeader = true;
                headerBlock.LoopId = loop.LoopId;
            }

            // 标记循环体块
            foreach (var bodyBlockId in loopBody)
            {
                if (blockById.TryGetValue(bodyBlockId, out var bodyBlock))
                {
                    if (bodyBlock.LoopId == null)
                    {
                        bodyBlock.LoopId = loop.LoopId;
                    }
                }
            }

            // 找出循环出口（循环体中有边指向循环体外的块）
            foreach (var bodyBlockId in loopBody)
            {
                foreach (var succ in successors[bodyBlockId])
                {
                    if (!loopBody.Contains(succ))
                    {
                        if (!loop.ExitBlocks.Contains(bodyBlockId))
                        {
                            loop.ExitBlocks.Add(bodyBlockId);
                        }
                    }
                }
            }

            loops.Add(loop);
        }

        // 计算循环嵌套关系
        foreach (var innerLoop in loops)
        {
            foreach (var outerLoop in loops)
            {
                if (innerLoop.LoopId == outerLoop.LoopId) continue;
                
                // 如果内层循环的所有块都在外层循环中
                if (innerLoop.BodyBlocks.All(b => outerLoop.BodyBlocks.Contains(b)) &&
                    innerLoop.BodyBlocks.Count < outerLoop.BodyBlocks.Count)
                {
                    if (innerLoop.ParentLoopId == null || 
                        loops.First(l => l.LoopId == innerLoop.ParentLoopId).BodyBlocks.Count > outerLoop.BodyBlocks.Count)
                    {
                        innerLoop.ParentLoopId = outerLoop.LoopId;
                    }
                }
            }
        }

        // 计算嵌套层级
        foreach (var loop in loops)
        {
            int level = 0;
            int? parentId = loop.ParentLoopId;
            while (parentId != null)
            {
                level++;
                parentId = loops.FirstOrDefault(l => l.LoopId == parentId)?.ParentLoopId;
            }
            loop.NestingLevel = level;
        }

        cfg.DetectedLoops = loops;
    }

    private HashSet<int> IdentifyLeaders(MethodBody body)
    {
        var leaders = new HashSet<int> { 0 }; // 第一条指令总是 leader

        foreach (var instr in body.Instructions)
        {
            // 分支目标是 leader
            if (instr.Operand is Instruction target)
            {
                leaders.Add(target.Offset);
            }
            else if (instr.Operand is Instruction[] targets) // switch
            {
                foreach (var t in targets)
                {
                    leaders.Add(t.Offset);
                }
            }

            // 分支/跳转后的下一条指令是 leader
            if (IsBranchInstruction(instr.OpCode) || instr.OpCode == OpCodes.Ret || instr.OpCode == OpCodes.Throw)
            {
                var next = instr.Next;
                if (next != null)
                {
                    leaders.Add(next.Offset);
                }
            }
        }

        // 异常处理边界也是 leader
        foreach (var handler in body.ExceptionHandlers)
        {
            leaders.Add(handler.TryStart.Offset);
            if (handler.TryEnd != null) leaders.Add(handler.TryEnd.Offset);
            leaders.Add(handler.HandlerStart.Offset);
            if (handler.HandlerEnd != null) leaders.Add(handler.HandlerEnd.Offset);
            if (handler.FilterStart != null) leaders.Add(handler.FilterStart.Offset);
        }

        return leaders;
    }

    private List<BasicBlock> BuildBasicBlocks(MethodBody body, HashSet<int> leaders)
    {
        var blocks = new List<BasicBlock>();
        var sortedLeaders = leaders.OrderBy(x => x).ToList();

        for (int i = 0; i < sortedLeaders.Count; i++)
        {
            var startOffset = sortedLeaders[i];
            var endOffset = (i + 1 < sortedLeaders.Count) 
                ? sortedLeaders[i + 1] 
                : int.MaxValue;

            var block = new BasicBlock
            {
                Id = $"BB{blocks.Count}",
                StartOffset = startOffset
            };

            foreach (var instr in body.Instructions)
            {
                if (instr.Offset >= startOffset && instr.Offset < endOffset)
                {
                    block.Instructions.Add(new CFGInstruction
                    {
                        Offset = instr.Offset,
                        OpCode = instr.OpCode.Name,
                        Operand = FormatOperand(instr)
                    });
                    block.EndOffset = instr.Offset;
                }
            }

            if (block.Instructions.Count > 0)
            {
                var lastInstr = block.Instructions.Last();
                block.TerminatorType = ClassifyTerminator(lastInstr.OpCode);
                blocks.Add(block);
            }
        }

        return blocks;
    }

    private void BuildEdges(ControlFlowGraph cfg, MethodBody body)
    {
        var blockByOffset = cfg.BasicBlocks.ToDictionary(b => b.StartOffset);

        foreach (var block in cfg.BasicBlocks)
        {
            var lastInstrOffset = block.EndOffset;
            var lastInstr = body.Instructions.FirstOrDefault(i => i.Offset == lastInstrOffset);
            if (lastInstr == null) continue;

            // 无条件跳转
            if (lastInstr.OpCode == OpCodes.Br || lastInstr.OpCode == OpCodes.Br_S)
            {
                if (lastInstr.Operand is Instruction target && blockByOffset.TryGetValue(target.Offset, out var targetBlock))
                {
                    cfg.Edges.Add(new CFGEdge
                    {
                        FromBlock = block.Id,
                        ToBlock = targetBlock.Id,
                        EdgeType = EdgeType.Unconditional
                    });
                }
            }
            // 条件跳转
            else if (IsConditionalBranch(lastInstr.OpCode))
            {
                // 跳转目标
                if (lastInstr.Operand is Instruction target && blockByOffset.TryGetValue(target.Offset, out var targetBlock))
                {
                    cfg.Edges.Add(new CFGEdge
                    {
                        FromBlock = block.Id,
                        ToBlock = targetBlock.Id,
                        EdgeType = EdgeType.ConditionalTrue
                    });
                }
                // 顺序执行 (条件不满足)
                if (lastInstr.Next != null && blockByOffset.TryGetValue(lastInstr.Next.Offset, out var fallthrough))
                {
                    cfg.Edges.Add(new CFGEdge
                    {
                        FromBlock = block.Id,
                        ToBlock = fallthrough.Id,
                        EdgeType = EdgeType.ConditionalFalse
                    });
                }
            }
            // Switch
            else if (lastInstr.OpCode == OpCodes.Switch)
            {
                if (lastInstr.Operand is Instruction[] targets)
                {
                    for (int i = 0; i < targets.Length; i++)
                    {
                        if (blockByOffset.TryGetValue(targets[i].Offset, out var targetBlock))
                        {
                            cfg.Edges.Add(new CFGEdge
                            {
                                FromBlock = block.Id,
                                ToBlock = targetBlock.Id,
                                EdgeType = EdgeType.Switch,
                                Label = $"case {i}"
                            });
                        }
                    }
                }
                // default (fall-through)
                if (lastInstr.Next != null && blockByOffset.TryGetValue(lastInstr.Next.Offset, out var defaultBlock))
                {
                    cfg.Edges.Add(new CFGEdge
                    {
                        FromBlock = block.Id,
                        ToBlock = defaultBlock.Id,
                        EdgeType = EdgeType.Switch,
                        Label = "default"
                    });
                }
            }
            // 顺序执行 (非跳转结尾)
            else if (block.TerminatorType != TerminatorType.Return && block.TerminatorType != TerminatorType.Throw)
            {
                if (lastInstr.Next != null && blockByOffset.TryGetValue(lastInstr.Next.Offset, out var nextBlock))
                {
                    cfg.Edges.Add(new CFGEdge
                    {
                        FromBlock = block.Id,
                        ToBlock = nextBlock.Id,
                        EdgeType = EdgeType.FallThrough
                    });
                }
            }
        }
    }

    private bool IsBranchInstruction(OpCode opCode)
    {
        return opCode.FlowControl == FlowControl.Branch ||
               opCode.FlowControl == FlowControl.Cond_Branch;
    }

    private bool IsConditionalBranch(OpCode opCode)
    {
        return opCode.FlowControl == FlowControl.Cond_Branch && opCode != OpCodes.Switch;
    }

    private TerminatorType ClassifyTerminator(string opCode)
    {
        return opCode.ToLowerInvariant() switch
        {
            "ret" => TerminatorType.Return,
            "throw" => TerminatorType.Throw,
            "br" or "br.s" => TerminatorType.Branch,
            "switch" => TerminatorType.Switch,
            var s when s.StartsWith("b") => TerminatorType.ConditionalBranch,
            _ => TerminatorType.FallThrough
        };
    }

    private string FormatOperand(Instruction instr)
    {
        if (instr.Operand == null) return "";
        
        return instr.Operand switch
        {
            Instruction target => $"IL_{target.Offset:X4}",
            Instruction[] targets => string.Join(", ", targets.Select(t => $"IL_{t.Offset:X4}")),
            MethodReference method => method.Name,
            FieldReference field => field.Name,
            TypeReference type => type.Name,
            string s => $"\"{s}\"",
            _ => instr.Operand.ToString() ?? ""
        };
    }

    private MethodDefinition? FindMethod(AssemblyContext context, string typeName, string methodName)
    {
        foreach (var type in context.Assembly!.MainModule.Types)
        {
            if (type.FullName == typeName)
            {
                return type.Methods.FirstOrDefault(m => m.Name == methodName);
            }
            foreach (var nested in type.NestedTypes)
            {
                if (nested.FullName == typeName)
                {
                    return nested.Methods.FirstOrDefault(m => m.Name == methodName);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 生成 Mermaid 格式的控制流图
    /// </summary>
    public string GenerateMermaid(ControlFlowGraph cfg)
    {
        if (!string.IsNullOrEmpty(cfg.Error))
        {
            return $"graph TD\n    error[\"Error: {cfg.Error}\"]";
        }

        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        // 绘制基本块
        foreach (var block in cfg.BasicBlocks)
        {
            var label = $"{block.Id}\\nIL_{block.StartOffset:X4}-IL_{block.EndOffset:X4}";
            var shape = block.TerminatorType switch
            {
                TerminatorType.Return => $"{block.Id}[[\"{label}\"]]",
                TerminatorType.Throw => $"{block.Id}((\"{label}\"))",
                TerminatorType.ConditionalBranch => $"{block.Id}{{\"{label}\"}}",
                TerminatorType.Switch => $"{block.Id}{{\"{label}\"}}",
                _ => $"{block.Id}[\"{label}\"]"
            };
            sb.AppendLine($"    {shape}");

            // 样式
            var style = block.TerminatorType switch
            {
                TerminatorType.Return => "fill:#4CAF50,color:#fff",
                TerminatorType.Throw => "fill:#F44336,color:#fff",
                TerminatorType.ConditionalBranch => "fill:#FF9800",
                _ => "fill:#2196F3,color:#fff"
            };
            sb.AppendLine($"    style {block.Id} {style}");
        }

        // 绘制边
        foreach (var edge in cfg.Edges)
        {
            var arrow = edge.EdgeType switch
            {
                EdgeType.ConditionalTrue => "-->|true|",
                EdgeType.ConditionalFalse => "-.->|false|",
                EdgeType.Switch => $"-->|{edge.Label}|",
                EdgeType.Unconditional => "==>",
                _ => "-->"
            };
            sb.AppendLine($"    {edge.FromBlock} {arrow} {edge.ToBlock}");
        }

        return sb.ToString();
    }
}

#region Models

public class ControlFlowGraph
{
    public string MethodName { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public string? Error { get; set; }
    public List<BasicBlock> BasicBlocks { get; set; } = new();
    public List<CFGEdge> Edges { get; set; } = new();
    public List<ExceptionRegion> ExceptionRegions { get; set; } = new();
    public List<LoopInfo> DetectedLoops { get; set; } = new();
}

public class BasicBlock
{
    public string Id { get; set; } = "";
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public List<CFGInstruction> Instructions { get; set; } = new();
    public TerminatorType TerminatorType { get; set; }
    public bool IsInTryBlock { get; set; }
    public bool IsExceptionHandler { get; set; }
    public string? HandlerType { get; set; }  // catch, finally, filter, fault
    public bool IsLoopHeader { get; set; }
    public int? LoopId { get; set; }
}

public class CFGInstruction
{
    public int Offset { get; set; }
    public string OpCode { get; set; } = "";
    public string Operand { get; set; } = "";
}

public class CFGEdge
{
    public string FromBlock { get; set; } = "";
    public string ToBlock { get; set; } = "";
    public EdgeType EdgeType { get; set; }
    public string? Label { get; set; }
}

public enum TerminatorType
{
    FallThrough,
    Branch,
    ConditionalBranch,
    Switch,
    Return,
    Throw
}

public enum EdgeType
{
    FallThrough,
    Unconditional,
    ConditionalTrue,
    ConditionalFalse,
    Switch,
    Exception,      // 异常边（try -> handler）
    BackEdge        // 循环回边
}

/// <summary>
/// 异常处理区域
/// </summary>
public class ExceptionRegion
{
    public string Id { get; set; } = "";
    public ExceptionHandlerType HandlerType { get; set; }
    public int TryStartOffset { get; set; }
    public int TryEndOffset { get; set; }
    public int HandlerStartOffset { get; set; }
    public int HandlerEndOffset { get; set; }
    public int? FilterStartOffset { get; set; }
    public string? CatchType { get; set; }
    public List<string> TryBlocks { get; set; } = new();
    public List<string> HandlerBlocks { get; set; } = new();
}

/// <summary>
/// 循环信息
/// </summary>
public class LoopInfo
{
    public int LoopId { get; set; }
    public string HeaderBlockId { get; set; } = "";
    public List<string> BodyBlocks { get; set; } = new();
    public List<string> ExitBlocks { get; set; } = new();
    public List<string> BackEdgeFrom { get; set; } = new();
    public int NestingLevel { get; set; }
    public int? ParentLoopId { get; set; }
}

public enum ExceptionHandlerType
{
    Catch,
    Finally,
    Filter,
    Fault
}

#endregion
