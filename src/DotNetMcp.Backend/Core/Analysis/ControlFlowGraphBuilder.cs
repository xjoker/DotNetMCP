using Mono.Cecil;
using Mono.Cecil.Cil;
using DotNetMcp.Backend.Core.Identity;
using System.Text;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 控制流图构建器 - 为方法生成控制流图
/// </summary>
public class ControlFlowGraphBuilder
{
    private readonly ModuleDefinition _module;
    private readonly MemberIdGenerator _idGenerator;

    public ControlFlowGraphBuilder(ModuleDefinition module, Guid mvid)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _idGenerator = new MemberIdGenerator(mvid);
    }

    /// <summary>
    /// 为指定方法构建控制流图
    /// </summary>
    public ControlFlowGraph Build(MethodDefinition method, bool includeIL = false)
    {
        if (method == null)
            throw new ArgumentNullException(nameof(method));

        if (!method.HasBody)
        {
            return new ControlFlowGraph
            {
                MethodId = _idGenerator.GenerateForMethod(method),
                MethodName = $"{method.DeclaringType.FullName}.{method.Name}",
                Blocks = new List<BasicBlock>(),
                Edges = new List<ControlFlowEdge>(),
                EntryBlockId = null,
                ExitBlockIds = new List<string>()
            };
        }

        var body = method.Body;
        var instructions = body.Instructions.ToList();

        // 步骤1: 识别基本块边界
        var blockBoundaries = FindBlockBoundaries(body);

        // 步骤2: 创建基本块
        var blocks = CreateBasicBlocks(instructions, blockBoundaries, includeIL);

        // 步骤3: 建立边关系
        var edges = BuildEdges(blocks, instructions);

        // 步骤4: 识别入口和出口块
        var entryBlockId = blocks.FirstOrDefault()?.Id;
        var exitBlockIds = blocks
            .Where(b => b.Type == BlockType.Exit || b.Type == BlockType.Exception)
            .Select(b => b.Id)
            .ToList();

        // 如果没有显式出口块，最后一个块是出口
        if (exitBlockIds.Count == 0 && blocks.Count > 0)
        {
            exitBlockIds.Add(blocks.Last().Id);
        }

        return new ControlFlowGraph
        {
            MethodId = _idGenerator.GenerateForMethod(method),
            MethodName = $"{method.DeclaringType.FullName}.{method.Name}",
            Blocks = blocks,
            Edges = edges,
            EntryBlockId = entryBlockId,
            ExitBlockIds = exitBlockIds
        };
    }

    /// <summary>
    /// 生成 Mermaid 格式的流程图
    /// </summary>
    public string ToMermaid(ControlFlowGraph cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");

        // 节点
        foreach (var block in cfg.Blocks)
        {
            var shape = block.Type switch
            {
                BlockType.Entry => $"([\"{block.Id}: Entry\"])",
                BlockType.Exit => $"([\"{block.Id}: Exit\"])",
                BlockType.Branch => $"{{\"{block.Id}: Branch\"}}",
                BlockType.Exception => $"[[\"{block.Id}: Exception\"]]",
                _ => $"[\"{block.Id}: IL_{block.StartOffset:X4}-IL_{block.EndOffset:X4}\"]"
            };
            sb.AppendLine($"    {block.Id}{shape}");
        }

        sb.AppendLine();

        // 边
        foreach (var edge in cfg.Edges)
        {
            var label = edge.Type switch
            {
                EdgeType.ConditionalTrue => "|true|",
                EdgeType.ConditionalFalse => "|false|",
                EdgeType.Unconditional => "|jump|",
                EdgeType.Exception => "|exception|",
                _ => ""
            };
            sb.AppendLine($"    {edge.FromBlockId} -->{label} {edge.ToBlockId}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 识别基本块边界（跳转目标和跳转后的指令）
    /// </summary>
    private HashSet<int> FindBlockBoundaries(MethodBody body)
    {
        var boundaries = new HashSet<int>();
        var instructions = body.Instructions;

        if (instructions.Count == 0)
            return boundaries;

        // 第一条指令是边界
        boundaries.Add(0);

        foreach (var instruction in instructions)
        {
            // 分支指令后的下一条是边界
            if (IsBranchInstruction(instruction))
            {
                var nextIndex = instructions.IndexOf(instruction) + 1;
                if (nextIndex < instructions.Count)
                {
                    boundaries.Add(instructions[nextIndex].Offset);
                }

                // 跳转目标也是边界
                if (instruction.Operand is Instruction target)
                {
                    boundaries.Add(target.Offset);
                }
                else if (instruction.Operand is Instruction[] targets) // switch
                {
                    foreach (var t in targets)
                    {
                        boundaries.Add(t.Offset);
                    }
                }
            }

            // ret, throw, rethrow 后的下一条是边界
            if (IsExitInstruction(instruction))
            {
                var nextIndex = instructions.IndexOf(instruction) + 1;
                if (nextIndex < instructions.Count)
                {
                    boundaries.Add(instructions[nextIndex].Offset);
                }
            }
        }

        // 异常处理块边界
        foreach (var handler in body.ExceptionHandlers)
        {
            boundaries.Add(handler.TryStart.Offset);
            if (handler.TryEnd != null)
                boundaries.Add(handler.TryEnd.Offset);
            boundaries.Add(handler.HandlerStart.Offset);
            if (handler.HandlerEnd != null)
                boundaries.Add(handler.HandlerEnd.Offset);
            if (handler.FilterStart != null)
                boundaries.Add(handler.FilterStart.Offset);
        }

        return boundaries;
    }

    /// <summary>
    /// 根据边界创建基本块
    /// </summary>
    private List<BasicBlock> CreateBasicBlocks(List<Instruction> instructions, HashSet<int> boundaries, bool includeIL)
    {
        var blocks = new List<BasicBlock>();
        var sortedBoundaries = boundaries.OrderBy(b => b).ToList();

        for (int i = 0; i < sortedBoundaries.Count; i++)
        {
            var startOffset = sortedBoundaries[i];
            var endOffset = i + 1 < sortedBoundaries.Count
                ? sortedBoundaries[i + 1] - 1
                : instructions.Last().Offset;

            var blockInstructions = instructions
                .Where(instr => instr.Offset >= startOffset && instr.Offset <= endOffset)
                .ToList();

            if (blockInstructions.Count == 0)
                continue;

            var lastInstruction = blockInstructions.Last();
            var blockType = DetermineBlockType(blockInstructions, i == 0);

            var block = new BasicBlock
            {
                Id = $"BB{blocks.Count}",
                StartOffset = startOffset,
                EndOffset = blockInstructions.Last().Offset,
                Type = blockType,
                InstructionCount = blockInstructions.Count,
                Instructions = includeIL
                    ? blockInstructions.Select(instr => new ILInstruction
                    {
                        Offset = instr.Offset,
                        OpCode = instr.OpCode.Name,
                        Operand = FormatOperand(instr)
                    }).ToList()
                    : null,
                Predecessors = new List<string>(),
                Successors = new List<string>()
            };

            blocks.Add(block);
        }

        return blocks;
    }

    /// <summary>
    /// 构建边关系
    /// </summary>
    private List<ControlFlowEdge> BuildEdges(List<BasicBlock> blocks, List<Instruction> instructions)
    {
        var edges = new List<ControlFlowEdge>();
        var offsetToBlock = blocks.ToDictionary(b => b.StartOffset, b => b);

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var lastInstrOffset = block.EndOffset;
            var lastInstruction = instructions.FirstOrDefault(instr => instr.Offset == lastInstrOffset);

            if (lastInstruction == null)
                continue;

            // 无条件跳转
            if (IsUnconditionalBranch(lastInstruction))
            {
                if (lastInstruction.Operand is Instruction target && offsetToBlock.TryGetValue(target.Offset, out var targetBlock))
                {
                    edges.Add(new ControlFlowEdge
                    {
                        FromBlockId = block.Id,
                        ToBlockId = targetBlock.Id,
                        Type = EdgeType.Unconditional,
                        Condition = null
                    });
                    block.Successors.Add(targetBlock.Id);
                    targetBlock.Predecessors.Add(block.Id);
                }
            }
            // 条件跳转
            else if (IsConditionalBranch(lastInstruction))
            {
                // true 分支 - 跳转目标
                if (lastInstruction.Operand is Instruction target && offsetToBlock.TryGetValue(target.Offset, out var targetBlock))
                {
                    edges.Add(new ControlFlowEdge
                    {
                        FromBlockId = block.Id,
                        ToBlockId = targetBlock.Id,
                        Type = EdgeType.ConditionalTrue,
                        Condition = lastInstruction.OpCode.Name
                    });
                    block.Successors.Add(targetBlock.Id);
                    targetBlock.Predecessors.Add(block.Id);
                }

                // false 分支 - 顺序执行
                if (i + 1 < blocks.Count)
                {
                    var nextBlock = blocks[i + 1];
                    edges.Add(new ControlFlowEdge
                    {
                        FromBlockId = block.Id,
                        ToBlockId = nextBlock.Id,
                        Type = EdgeType.ConditionalFalse,
                        Condition = null
                    });
                    block.Successors.Add(nextBlock.Id);
                    nextBlock.Predecessors.Add(block.Id);
                }
            }
            // switch
            else if (lastInstruction.OpCode == OpCodes.Switch && lastInstruction.Operand is Instruction[] targets)
            {
                for (int j = 0; j < targets.Length; j++)
                {
                    if (offsetToBlock.TryGetValue(targets[j].Offset, out var targetBlock))
                    {
                        edges.Add(new ControlFlowEdge
                        {
                            FromBlockId = block.Id,
                            ToBlockId = targetBlock.Id,
                            Type = EdgeType.Switch,
                            Condition = $"case {j}"
                        });
                        block.Successors.Add(targetBlock.Id);
                        targetBlock.Predecessors.Add(block.Id);
                    }
                }

                // default 分支
                if (i + 1 < blocks.Count)
                {
                    var nextBlock = blocks[i + 1];
                    edges.Add(new ControlFlowEdge
                    {
                        FromBlockId = block.Id,
                        ToBlockId = nextBlock.Id,
                        Type = EdgeType.Switch,
                        Condition = "default"
                    });
                    block.Successors.Add(nextBlock.Id);
                    nextBlock.Predecessors.Add(block.Id);
                }
            }
            // 退出指令 - 无后继
            else if (IsExitInstruction(lastInstruction))
            {
                // 不添加边
            }
            // 顺序执行
            else if (i + 1 < blocks.Count)
            {
                var nextBlock = blocks[i + 1];
                edges.Add(new ControlFlowEdge
                {
                    FromBlockId = block.Id,
                    ToBlockId = nextBlock.Id,
                    Type = EdgeType.Fallthrough,
                    Condition = null
                });
                block.Successors.Add(nextBlock.Id);
                nextBlock.Predecessors.Add(block.Id);
            }
        }

        return edges;
    }

    private BlockType DetermineBlockType(List<Instruction> instructions, bool isFirst)
    {
        if (isFirst)
            return BlockType.Entry;

        var lastInstruction = instructions.Last();

        if (IsExitInstruction(lastInstruction))
            return BlockType.Exit;

        if (IsBranchInstruction(lastInstruction))
            return BlockType.Branch;

        return BlockType.Normal;
    }

    private static bool IsBranchInstruction(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code == Code.Br || code == Code.Br_S ||
               code == Code.Brtrue || code == Code.Brtrue_S ||
               code == Code.Brfalse || code == Code.Brfalse_S ||
               code == Code.Beq || code == Code.Beq_S ||
               code == Code.Bne_Un || code == Code.Bne_Un_S ||
               code == Code.Blt || code == Code.Blt_S ||
               code == Code.Blt_Un || code == Code.Blt_Un_S ||
               code == Code.Ble || code == Code.Ble_S ||
               code == Code.Ble_Un || code == Code.Ble_Un_S ||
               code == Code.Bgt || code == Code.Bgt_S ||
               code == Code.Bgt_Un || code == Code.Bgt_Un_S ||
               code == Code.Bge || code == Code.Bge_S ||
               code == Code.Bge_Un || code == Code.Bge_Un_S ||
               code == Code.Switch ||
               code == Code.Leave || code == Code.Leave_S;
    }

    private static bool IsUnconditionalBranch(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code == Code.Br || code == Code.Br_S ||
               code == Code.Leave || code == Code.Leave_S;
    }

    private static bool IsConditionalBranch(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code == Code.Brtrue || code == Code.Brtrue_S ||
               code == Code.Brfalse || code == Code.Brfalse_S ||
               code == Code.Beq || code == Code.Beq_S ||
               code == Code.Bne_Un || code == Code.Bne_Un_S ||
               code == Code.Blt || code == Code.Blt_S ||
               code == Code.Blt_Un || code == Code.Blt_Un_S ||
               code == Code.Ble || code == Code.Ble_S ||
               code == Code.Ble_Un || code == Code.Ble_Un_S ||
               code == Code.Bgt || code == Code.Bgt_S ||
               code == Code.Bgt_Un || code == Code.Bgt_Un_S ||
               code == Code.Bge || code == Code.Bge_S ||
               code == Code.Bge_Un || code == Code.Bge_Un_S;
    }

    private static bool IsExitInstruction(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code == Code.Ret ||
               code == Code.Throw ||
               code == Code.Rethrow ||
               code == Code.Endfinally ||
               code == Code.Endfilter;
    }

    private static string? FormatOperand(Instruction instruction)
    {
        if (instruction.Operand == null)
            return null;

        return instruction.Operand switch
        {
            Instruction target => $"IL_{target.Offset:X4}",
            Instruction[] targets => string.Join(", ", targets.Select(t => $"IL_{t.Offset:X4}")),
            MethodReference method => $"{method.DeclaringType.Name}.{method.Name}",
            FieldReference field => $"{field.DeclaringType.Name}.{field.Name}",
            TypeReference type => type.Name,
            string s => $"\"{s}\"",
            _ => instruction.Operand.ToString()
        };
    }
}

#region 数据结构

/// <summary>
/// 控制流图
/// </summary>
public record ControlFlowGraph
{
    public required string MethodId { get; init; }
    public required string MethodName { get; init; }
    public required List<BasicBlock> Blocks { get; init; }
    public required List<ControlFlowEdge> Edges { get; init; }
    public string? EntryBlockId { get; init; }
    public required List<string> ExitBlockIds { get; init; }

    public int BlockCount => Blocks.Count;
    public int EdgeCount => Edges.Count;
}

/// <summary>
/// 基本块
/// </summary>
public record BasicBlock
{
    public required string Id { get; init; }
    public required int StartOffset { get; init; }
    public required int EndOffset { get; init; }
    public required BlockType Type { get; init; }
    public required int InstructionCount { get; init; }
    public List<ILInstruction>? Instructions { get; init; }
    public required List<string> Predecessors { get; init; }
    public required List<string> Successors { get; init; }
}

/// <summary>
/// IL 指令
/// </summary>
public record ILInstruction
{
    public required int Offset { get; init; }
    public required string OpCode { get; init; }
    public string? Operand { get; init; }
}

/// <summary>
/// 基本块类型
/// </summary>
public enum BlockType
{
    Entry,
    Normal,
    Branch,
    Exit,
    Exception
}

/// <summary>
/// 控制流边
/// </summary>
public record ControlFlowEdge
{
    public required string FromBlockId { get; init; }
    public required string ToBlockId { get; init; }
    public required EdgeType Type { get; init; }
    public string? Condition { get; init; }
}

/// <summary>
/// 边类型
/// </summary>
public enum EdgeType
{
    Fallthrough,
    ConditionalTrue,
    ConditionalFalse,
    Unconditional,
    Switch,
    Exception
}

#endregion
