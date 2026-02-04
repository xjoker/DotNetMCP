using DotNetMcp.Backend.Core.Compilation;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Compilation;

public class CompilationServiceTests
{
    private readonly ReferenceAssemblyProvider _referenceProvider;
    private readonly CompilationService _service;

    public CompilationServiceTests()
    {
        _referenceProvider = new ReferenceAssemblyProvider();
        _service = new CompilationService(_referenceProvider);
    }

    [Fact]
    public void Compile_SimpleClass_ShouldSucceed()
    {
        // Arrange
        var sourceCode = @"
public class TestClass
{
    public string GetMessage()
    {
        return ""Hello, World!"";
    }
}";

        // Act
        var result = _service.Compile(sourceCode, "TestAssembly");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AssemblyBytes);
        Assert.True(result.AssemblyBytes.Length > 0);
        Assert.Equal("TestAssembly", result.AssemblyName);
    }

    [Fact]
    public void Compile_WithSystemUsing_ShouldSucceed()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.Linq;

public class Calculator
{
    public int Sum(int[] numbers)
    {
        return numbers.Sum();
    }
}";

        // Act
        var result = _service.Compile(sourceCode, "CalculatorAssembly");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AssemblyBytes);
    }

    [Fact]
    public void Compile_SyntaxError_ShouldReturnDiagnostics()
    {
        // Arrange
        var sourceCode = @"
public class BrokenClass
{
    public void Method()
    {
        // 缺少分号
        int x = 5
    }
}";

        // Act
        var result = _service.Compile(sourceCode, "BrokenAssembly");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(CompilationErrorCode.CompilationError, result.ErrorCode);
        Assert.NotNull(result.Diagnostics);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Compile_EmptySource_ShouldFail()
    {
        // Act
        var result = _service.Compile("", "EmptyAssembly");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(CompilationErrorCode.EmptySource, result.ErrorCode);
    }

    [Fact]
    public void ValidateSyntax_ValidCode_ShouldPass()
    {
        // Arrange
        var sourceCode = @"
public class ValidClass
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";

        // Act
        var result = _service.ValidateSyntax(sourceCode);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateSyntax_InvalidCode_ShouldReturnErrors()
    {
        // Arrange
        var sourceCode = @"
public class InvalidClass
{
    public void Method(
    {
        // 缺少参数列表右括号
    }
}";

        // Act
        var result = _service.ValidateSyntax(sourceCode);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotNull(result.Diagnostics);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Compile_WithUnsafeCode_ShouldSucceedWhenAllowed()
    {
        // Arrange
        var sourceCode = @"
public class UnsafeClass
{
    public unsafe void UsePointer(int* ptr)
    {
        *ptr = 42;
    }
}";
        var options = new CompilationOptions { AllowUnsafe = true };

        // Act
        var result = _service.Compile(sourceCode, "UnsafeAssembly", options);

        // Assert
        Assert.True(result.IsSuccess);
    }
}
