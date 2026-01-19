using DotNetMcp.Backend.Core.Modification;
using Mono.Cecil;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Modification;

public class TypeFactoryTests
{
    [Fact]
    public void CreateClass_ShouldCreatePublicClass()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var factory = new TypeFactory(module);

        // Act
        var type = factory.CreateClass("TestNamespace", "TestClass");

        // Assert
        Assert.Equal("TestNamespace", type.Namespace);
        Assert.Equal("TestClass", type.Name);
        Assert.True((type.Attributes & TypeAttributes.Public) != 0);
        Assert.Equal("System.Object", type.BaseType.FullName);
    }

    [Fact]
    public void CreateInterface_ShouldCreateInterface()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var factory = new TypeFactory(module);

        // Act
        var type = factory.CreateInterface("TestNamespace", "ITestInterface");

        // Assert
        Assert.Equal("ITestInterface", type.Name);
        Assert.True((type.Attributes & TypeAttributes.Interface) != 0);
        Assert.True((type.Attributes & TypeAttributes.Abstract) != 0);
    }

    [Fact]
    public void CreateEnum_ShouldCreateEnumWithValues()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var factory = new TypeFactory(module);

        // Act
        var type = factory.CreateEnum("TestNamespace", "TestEnum",
            ("Value1", 0),
            ("Value2", 1),
            ("Value3", 2));

        // Assert
        Assert.Equal("TestEnum", type.Name);
        Assert.Equal(4, type.Fields.Count); // value__ + 3 values
        Assert.Contains(type.Fields, f => f.Name == "Value1");
        Assert.Contains(type.Fields, f => f.Name == "Value2");
        Assert.Contains(type.Fields, f => f.Name == "Value3");
    }

    [Fact]
    public void CreateMethod_ShouldCreateMethodWithParameters()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var factory = new TypeFactory(module);

        // Act
        var method = factory.CreateMethod(
            "TestMethod",
            module.ImportReference(typeof(int)),
            MethodAttributes.Public,
            ("a", module.ImportReference(typeof(int))),
            ("b", module.ImportReference(typeof(int)))
        );

        // Assert
        Assert.Equal("TestMethod", method.Name);
        Assert.Equal(2, method.Parameters.Count);
        Assert.Equal("a", method.Parameters[0].Name);
        Assert.Equal("b", method.Parameters[1].Name);
    }

    [Fact]
    public void CreateConstructor_ShouldCreateCtor()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var factory = new TypeFactory(module);

        // Act
        var ctor = factory.CreateConstructor(
            ("value", module.ImportReference(typeof(string)))
        );

        // Assert
        Assert.Equal(".ctor", ctor.Name);
        Assert.True(ctor.IsConstructor);
        Assert.Equal(1, ctor.Parameters.Count);
    }
}
