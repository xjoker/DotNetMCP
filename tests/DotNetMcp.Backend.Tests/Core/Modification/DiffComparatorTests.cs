using DotNetMcp.Backend.Core.Modification;
using Mono.Cecil;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Modification;

public class DiffComparatorTests
{
    [Fact]
    public void CompareAssemblies_SameAssembly_ShouldHaveNoDiffs()
    {
        // Arrange
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Test", new Version(1, 0, 0, 0)),
            "Test",
            ModuleKind.Dll);
        var comparator = new DiffComparator();

        // Act
        var diff = comparator.CompareAssemblies(assembly, assembly);

        // Assert
        Assert.Equal(0, diff.TotalChanges);
    }

    [Fact]
    public void CompareAssemblies_AddedType_ShouldDetect()
    {
        // Arrange
        var original = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Test", new Version(1, 0, 0, 0)),
            "Test",
            ModuleKind.Dll);

        var modified = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Test", new Version(1, 0, 0, 0)),
            "Test",
            ModuleKind.Dll);
        var newType = new TypeDefinition("TestNamespace", "NewClass",
            TypeAttributes.Public | TypeAttributes.Class,
            modified.MainModule.ImportReference(typeof(object)));
        modified.MainModule.Types.Add(newType);

        var comparator = new DiffComparator();

        // Act
        var diff = comparator.CompareAssemblies(original, modified);

        // Assert
        Assert.True(diff.TotalChanges > 0);
        Assert.Contains(diff.TypeDiffs, t => t.DiffType == DiffType.Added && t.TypeName.Contains("NewClass"));
    }

    [Fact]
    public void CompareTypes_AddedMethod_ShouldDetect()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        
        var original = new TypeDefinition("TestNamespace", "TestClass",
            TypeAttributes.Public | TypeAttributes.Class,
            module.ImportReference(typeof(object)));

        var modified = new TypeDefinition("TestNamespace", "TestClass",
            TypeAttributes.Public | TypeAttributes.Class,
            module.ImportReference(typeof(object)));
        
        var newMethod = new MethodDefinition("NewMethod",
            MethodAttributes.Public, module.ImportReference(typeof(void)));
        modified.Methods.Add(newMethod);

        var comparator = new DiffComparator();

        // Act
        var diff = comparator.CompareTypes(original, modified);

        // Assert
        Assert.Equal(DiffType.Modified, diff.DiffType);
        Assert.Contains(diff.MemberDiffs, m => m.DiffType == DiffType.Added && m.MemberName == "NewMethod");
    }
}
