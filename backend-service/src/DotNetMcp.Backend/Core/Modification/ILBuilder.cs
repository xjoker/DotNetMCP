using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DotNetMcp.Backend.Core.Modification;

/// <summary>
/// IL 指令构建器 - 构建 IL 指令序列
/// </summary>
public class ILBuilder
{
    private readonly List<Instruction> _instructions = new();
    private readonly ILProcessor _processor;

    public ILBuilder(ILProcessor processor)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    /// <summary>
    /// 添加 nop 指令
    /// </summary>
    public ILBuilder Nop()
    {
        _instructions.Add(_processor.Create(OpCodes.Nop));
        return this;
    }

    /// <summary>
    /// 加载整数常量
    /// </summary>
    public ILBuilder LoadInt(int value)
    {
        var instruction = value switch
        {
            -1 => _processor.Create(OpCodes.Ldc_I4_M1),
            0 => _processor.Create(OpCodes.Ldc_I4_0),
            1 => _processor.Create(OpCodes.Ldc_I4_1),
            2 => _processor.Create(OpCodes.Ldc_I4_2),
            3 => _processor.Create(OpCodes.Ldc_I4_3),
            4 => _processor.Create(OpCodes.Ldc_I4_4),
            5 => _processor.Create(OpCodes.Ldc_I4_5),
            6 => _processor.Create(OpCodes.Ldc_I4_6),
            7 => _processor.Create(OpCodes.Ldc_I4_7),
            8 => _processor.Create(OpCodes.Ldc_I4_8),
            >= sbyte.MinValue and <= sbyte.MaxValue => _processor.Create(OpCodes.Ldc_I4_S, (sbyte)value),
            _ => _processor.Create(OpCodes.Ldc_I4, value)
        };
        _instructions.Add(instruction);
        return this;
    }

    /// <summary>
    /// 加载长整数常量
    /// </summary>
    public ILBuilder LoadLong(long value)
    {
        _instructions.Add(_processor.Create(OpCodes.Ldc_I8, value));
        return this;
    }

    /// <summary>
    /// 加载字符串常量
    /// </summary>
    public ILBuilder LoadString(string value)
    {
        _instructions.Add(_processor.Create(OpCodes.Ldstr, value));
        return this;
    }

    /// <summary>
    /// 加载 null
    /// </summary>
    public ILBuilder LoadNull()
    {
        _instructions.Add(_processor.Create(OpCodes.Ldnull));
        return this;
    }

    /// <summary>
    /// 加载参数
    /// </summary>
    public ILBuilder LoadArg(int index)
    {
        var instruction = index switch
        {
            0 => _processor.Create(OpCodes.Ldarg_0),
            1 => _processor.Create(OpCodes.Ldarg_1),
            2 => _processor.Create(OpCodes.Ldarg_2),
            3 => _processor.Create(OpCodes.Ldarg_3),
            <= byte.MaxValue => _processor.Create(OpCodes.Ldarg_S, (byte)index),
            _ => _processor.Create(OpCodes.Ldarg, index)
        };
        _instructions.Add(instruction);
        return this;
    }

    /// <summary>
    /// 加载局部变量
    /// </summary>
    public ILBuilder LoadLocal(int index)
    {
        var instruction = index switch
        {
            0 => _processor.Create(OpCodes.Ldloc_0),
            1 => _processor.Create(OpCodes.Ldloc_1),
            2 => _processor.Create(OpCodes.Ldloc_2),
            3 => _processor.Create(OpCodes.Ldloc_3),
            <= byte.MaxValue => _processor.Create(OpCodes.Ldloc_S, (byte)index),
            _ => _processor.Create(OpCodes.Ldloc, index)
        };
        _instructions.Add(instruction);
        return this;
    }

    /// <summary>
    /// 存储局部变量
    /// </summary>
    public ILBuilder StoreLocal(int index)
    {
        var instruction = index switch
        {
            0 => _processor.Create(OpCodes.Stloc_0),
            1 => _processor.Create(OpCodes.Stloc_1),
            2 => _processor.Create(OpCodes.Stloc_2),
            3 => _processor.Create(OpCodes.Stloc_3),
            <= byte.MaxValue => _processor.Create(OpCodes.Stloc_S, (byte)index),
            _ => _processor.Create(OpCodes.Stloc, index)
        };
        _instructions.Add(instruction);
        return this;
    }

    /// <summary>
    /// 加载字段
    /// </summary>
    public ILBuilder LoadField(FieldReference field)
    {
        _instructions.Add(_processor.Create(OpCodes.Ldfld, field));
        return this;
    }

    /// <summary>
    /// 加载静态字段
    /// </summary>
    public ILBuilder LoadStaticField(FieldReference field)
    {
        _instructions.Add(_processor.Create(OpCodes.Ldsfld, field));
        return this;
    }

    /// <summary>
    /// 存储字段
    /// </summary>
    public ILBuilder StoreField(FieldReference field)
    {
        _instructions.Add(_processor.Create(OpCodes.Stfld, field));
        return this;
    }

    /// <summary>
    /// 调用方法
    /// </summary>
    public ILBuilder Call(MethodReference method)
    {
        _instructions.Add(_processor.Create(OpCodes.Call, method));
        return this;
    }

    /// <summary>
    /// 虚拟调用
    /// </summary>
    public ILBuilder CallVirt(MethodReference method)
    {
        _instructions.Add(_processor.Create(OpCodes.Callvirt, method));
        return this;
    }

    /// <summary>
    /// 创建新对象
    /// </summary>
    public ILBuilder NewObj(MethodReference constructor)
    {
        _instructions.Add(_processor.Create(OpCodes.Newobj, constructor));
        return this;
    }

    /// <summary>
    /// 返回
    /// </summary>
    public ILBuilder Ret()
    {
        _instructions.Add(_processor.Create(OpCodes.Ret));
        return this;
    }

    /// <summary>
    /// 算术运算
    /// </summary>
    public ILBuilder Add() { _instructions.Add(_processor.Create(OpCodes.Add)); return this; }
    public ILBuilder Sub() { _instructions.Add(_processor.Create(OpCodes.Sub)); return this; }
    public ILBuilder Mul() { _instructions.Add(_processor.Create(OpCodes.Mul)); return this; }
    public ILBuilder Div() { _instructions.Add(_processor.Create(OpCodes.Div)); return this; }

    /// <summary>
    /// 比较
    /// </summary>
    public ILBuilder Ceq() { _instructions.Add(_processor.Create(OpCodes.Ceq)); return this; }
    public ILBuilder Cgt() { _instructions.Add(_processor.Create(OpCodes.Cgt)); return this; }
    public ILBuilder Clt() { _instructions.Add(_processor.Create(OpCodes.Clt)); return this; }

    /// <summary>
    /// 复制栈顶
    /// </summary>
    public ILBuilder Dup() { _instructions.Add(_processor.Create(OpCodes.Dup)); return this; }

    /// <summary>
    /// 弹出栈顶
    /// </summary>
    public ILBuilder Pop() { _instructions.Add(_processor.Create(OpCodes.Pop)); return this; }

    /// <summary>
    /// 获取构建的指令列表
    /// </summary>
    public IReadOnlyList<Instruction> Build() => _instructions.ToList();

    /// <summary>
    /// 应用到方法体
    /// </summary>
    public void ApplyTo(MethodBody body)
    {
        body.Instructions.Clear();
        foreach (var instruction in _instructions)
        {
            body.Instructions.Add(instruction);
        }
    }

    /// <summary>
    /// 在指定位置插入
    /// </summary>
    public void InsertBefore(MethodBody body, Instruction target)
    {
        var idx = body.Instructions.IndexOf(target);
        if (idx < 0) throw new ArgumentException("Target instruction not found");

        for (int i = _instructions.Count - 1; i >= 0; i--)
        {
            body.Instructions.Insert(idx, _instructions[i]);
        }
    }

    /// <summary>
    /// 在指定位置之后插入
    /// </summary>
    public void InsertAfter(MethodBody body, Instruction target)
    {
        var idx = body.Instructions.IndexOf(target);
        if (idx < 0) throw new ArgumentException("Target instruction not found");

        for (int i = 0; i < _instructions.Count; i++)
        {
            body.Instructions.Insert(idx + 1 + i, _instructions[i]);
        }
    }
}
