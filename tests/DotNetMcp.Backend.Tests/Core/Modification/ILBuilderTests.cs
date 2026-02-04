using DotNetMcp.Backend.Core.Modification;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Compilation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Modification;

public class ILBuilderTests
{
    [Fact]
    public void LoadInt_ShouldOptimizeSmallValues()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var method = new MethodDefinition("Test", MethodAttributes.Public, module.ImportReference(typeof(void)));
        method.Body = new MethodBody(method);
        var processor = method.Body.GetILProcessor();
        var builder = new ILBuilder(processor);

        // Act
        builder.LoadInt(0).LoadInt(1).LoadInt(8).LoadInt(100).Ret();
        var instructions = builder.Build();

        // Assert
        Assert.Equal(5, instructions.Count);
        Assert.Equal(OpCodes.Ldc_I4_0, instructions[0].OpCode);
        Assert.Equal(OpCodes.Ldc_I4_1, instructions[1].OpCode);
        Assert.Equal(OpCodes.Ldc_I4_8, instructions[2].OpCode);
        Assert.Equal(OpCodes.Ldc_I4_S, instructions[3].OpCode);
    }

    [Fact]
    public void LoadString_ShouldWork()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var method = new MethodDefinition("Test", MethodAttributes.Public, module.ImportReference(typeof(void)));
        method.Body = new MethodBody(method);
        var processor = method.Body.GetILProcessor();
        var builder = new ILBuilder(processor);

        // Act
        builder.LoadString("Hello").Ret();
        var instructions = builder.Build();

        // Assert
        Assert.Equal(2, instructions.Count);
        Assert.Equal(OpCodes.Ldstr, instructions[0].OpCode);
        Assert.Equal("Hello", instructions[0].Operand);
    }

    [Fact]
    public void ArithmeticOperations_ShouldWork()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var method = new MethodDefinition("Test", MethodAttributes.Public, module.ImportReference(typeof(void)));
        method.Body = new MethodBody(method);
        var processor = method.Body.GetILProcessor();
        var builder = new ILBuilder(processor);

        // Act
        builder.LoadInt(5).LoadInt(3).Add().Ret();
        var instructions = builder.Build();

        // Assert
        Assert.Equal(4, instructions.Count);
        Assert.Equal(OpCodes.Add, instructions[2].OpCode);
    }

    [Fact]
    public void ApplyTo_ShouldReplaceMethodBody()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var method = new MethodDefinition("Test", MethodAttributes.Public, module.ImportReference(typeof(int)));
        method.Body = new MethodBody(method);
        var processor = method.Body.GetILProcessor();
        var builder = new ILBuilder(processor);

        // 添加一些初始指令
        processor.Append(processor.Create(OpCodes.Nop));

        // Act
        builder.LoadInt(42).Ret();
        builder.ApplyTo(method.Body);

        // Assert
        Assert.Equal(2, method.Body.Instructions.Count);
        Assert.Equal(OpCodes.Ldc_I4_S, method.Body.Instructions[0].OpCode);
    }
}
