using DotNetMcp.Backend.Core.Modification;
using Mono.Cecil;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Modification;

public class AssemblyRewriterTests
{
    [Fact]
    public void AddType_ShouldAddTypeToModule()
    {
        // Arrange
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Test", new Version(1, 0, 0, 0)),
            "Test",
            ModuleKind.Dll);
        var rewriter = new AssemblyRewriter(assembly);
        var factory = new TypeFactory(assembly.MainModule);

        var newType = factory.CreateClass("TestNamespace", "NewClass");

        // Act
        var result = rewriter.AddType(newType);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(assembly.MainModule.Types, t => t.Name == "NewClass");
    }

    [Fact]
    public void AddMethod_ShouldAddMethodToType()
    {
        // Arrange
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Test", new Version(1, 0, 0, 0)),
            "Test",
            ModuleKind.Dll);
        var factory = new TypeFactory(assembly.MainModule);
        var type = factory.CreateClass("TestNamespace", "TestClass");
        assembly.MainModule.Types.Add(type);

        var rewriter = new AssemblyRewriter(assembly);
        var method = factory.CreateMethod("NewMethod", assembly.MainModule.ImportReference(typeof(void)));

        // Act
        var result = rewriter.AddMethod(type, method);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(type.Methods, m => m.Name == "NewMethod");
    }

    [Fact]
    public void RenameMethod_ShouldChangeMethodName()
    {
        // Arrange
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Test", new Version(1, 0, 0, 0)),
            "Test",
            ModuleKind.Dll);
        var factory = new TypeFactory(assembly.MainModule);
        var type = factory.CreateClass("TestNamespace", "TestClass");
        var method = factory.CreateMethod("OldName", assembly.MainModule.ImportReference(typeof(void)));
        type.Methods.Add(method);
        assembly.MainModule.Types.Add(type);

        var rewriter = new AssemblyRewriter(assembly);

        // Act
        var result = rewriter.RenameMethod(method, "NewName");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("NewName", method.Name);
    }

    [Fact]
    public void GetModifications_ShouldReturnAllChanges()
    {
        // Arrange
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Test", new Version(1, 0, 0, 0)),
            "Test",
            ModuleKind.Dll);
        var factory = new TypeFactory(assembly.MainModule);
        var rewriter = new AssemblyRewriter(assembly);

        var type = factory.CreateClass("TestNamespace", "TestClass");
        var method = factory.CreateMethod("TestMethod", assembly.MainModule.ImportReference(typeof(void)));

        // Act
        rewriter.AddType(type);
        rewriter.AddMethod(type, method);
        var modifications = rewriter.GetModifications();

        // Assert
        Assert.Equal(2, modifications.Count);
        Assert.Contains(modifications, m => m.Type == ModificationType.TypeAdded);
        Assert.Contains(modifications, m => m.Type == ModificationType.MethodAdded);
    }

    [Fact]
    public void SaveToMemory_ShouldReturnBytes()
    {
        // Arrange
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Test", new Version(1, 0, 0, 0)),
            "Test",
            ModuleKind.Dll);
        var factory = new TypeFactory(assembly.MainModule);
        var type = factory.CreateClass("TestNamespace", "TestClass");
        assembly.MainModule.Types.Add(type);

        var rewriter = new AssemblyRewriter(assembly);

        // Act
        var result = rewriter.SaveToMemory(out var bytes);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }
}
