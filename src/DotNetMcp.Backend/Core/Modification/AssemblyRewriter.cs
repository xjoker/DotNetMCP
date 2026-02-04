using Mono.Cecil;

namespace DotNetMcp.Backend.Core.Modification;

/// <summary>
/// 程序集重写器 - 对程序集进行修改并保存
/// </summary>
public class AssemblyRewriter
{
    private readonly AssemblyDefinition _assembly;
    private readonly List<ModificationRecord> _modifications = new();

    public AssemblyRewriter(AssemblyDefinition assembly)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
    }

    /// <summary>
    /// 添加新类型
    /// </summary>
    public ModificationResult AddType(TypeDefinition type)
    {
        try
        {
            _assembly.MainModule.Types.Add(type);
            RecordModification(ModificationType.TypeAdded, type.FullName);
            return ModificationResult.Success();
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure($"Failed to add type: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除类型
    /// </summary>
    public ModificationResult RemoveType(TypeDefinition type)
    {
        try
        {
            var removed = _assembly.MainModule.Types.Remove(type);
            if (!removed)
                return ModificationResult.Failure("Type not found in module");

            RecordModification(ModificationType.TypeRemoved, type.FullName);
            return ModificationResult.Success();
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure($"Failed to remove type: {ex.Message}");
        }
    }

    /// <summary>
    /// 添加方法到类型
    /// </summary>
    public ModificationResult AddMethod(TypeDefinition type, MethodDefinition method)
    {
        try
        {
            type.Methods.Add(method);
            RecordModification(ModificationType.MethodAdded, $"{type.FullName}.{method.Name}");
            return ModificationResult.Success();
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure($"Failed to add method: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除方法
    /// </summary>
    public ModificationResult RemoveMethod(TypeDefinition type, MethodDefinition method)
    {
        try
        {
            var removed = type.Methods.Remove(method);
            if (!removed)
                return ModificationResult.Failure("Method not found in type");

            RecordModification(ModificationType.MethodRemoved, $"{type.FullName}.{method.Name}");
            return ModificationResult.Success();
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure($"Failed to remove method: {ex.Message}");
        }
    }

    /// <summary>
    /// 添加字段
    /// </summary>
    public ModificationResult AddField(TypeDefinition type, FieldDefinition field)
    {
        try
        {
            type.Fields.Add(field);
            RecordModification(ModificationType.FieldAdded, $"{type.FullName}.{field.Name}");
            return ModificationResult.Success();
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure($"Failed to add field: {ex.Message}");
        }
    }

    /// <summary>
    /// 修改类型属性
    /// </summary>
    public ModificationResult ModifyTypeAttributes(TypeDefinition type, TypeAttributes newAttributes)
    {
        try
        {
            var oldAttributes = type.Attributes;
            type.Attributes = newAttributes;
            RecordModification(ModificationType.TypeModified, 
                $"{type.FullName}: {oldAttributes} -> {newAttributes}");
            return ModificationResult.Success();
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure($"Failed to modify type: {ex.Message}");
        }
    }

    /// <summary>
    /// 修改方法属性
    /// </summary>
    public ModificationResult ModifyMethodAttributes(MethodDefinition method, MethodAttributes newAttributes)
    {
        try
        {
            var oldAttributes = method.Attributes;
            method.Attributes = newAttributes;
            RecordModification(ModificationType.MethodModified,
                $"{method.DeclaringType.FullName}.{method.Name}: {oldAttributes} -> {newAttributes}");
            return ModificationResult.Success();
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure($"Failed to modify method: {ex.Message}");
        }
    }

    /// <summary>
    /// 重命名方法
    /// </summary>
    public ModificationResult RenameMethod(MethodDefinition method, string newName)
    {
        try
        {
            var oldName = method.Name;
            method.Name = newName;
            RecordModification(ModificationType.MethodRenamed,
                $"{method.DeclaringType.FullName}: {oldName} -> {newName}");
            return ModificationResult.Success();
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure($"Failed to rename method: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存修改后的程序集
    /// </summary>
    public SaveResult Save(string outputPath)
    {
        try
        {
            _assembly.Write(outputPath);
            return new SaveResult
            {
                IsSuccess = true,
                OutputPath = outputPath,
                ModificationCount = _modifications.Count,
                Modifications = _modifications.ToList()
            };
        }
        catch (Exception ex)
        {
            return new SaveResult
            {
                IsSuccess = false,
                ErrorMessage = $"Failed to save: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 保存到内存流
    /// </summary>
    public SaveResult SaveToMemory(out byte[]? bytes)
    {
        bytes = null;
        try
        {
            using var ms = new MemoryStream();
            _assembly.Write(ms);
            bytes = ms.ToArray();

            return new SaveResult
            {
                IsSuccess = true,
                ModificationCount = _modifications.Count,
                Modifications = _modifications.ToList()
            };
        }
        catch (Exception ex)
        {
            return new SaveResult
            {
                IsSuccess = false,
                ErrorMessage = $"Failed to save: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 获取修改记录
    /// </summary>
    public IReadOnlyList<ModificationRecord> GetModifications() => _modifications.ToList();

    private void RecordModification(ModificationType type, string description)
    {
        _modifications.Add(new ModificationRecord
        {
            Type = type,
            Description = description,
            Timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// 修改结果
/// </summary>
public record ModificationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }

    public static ModificationResult Success() => new() { IsSuccess = true };
    public static ModificationResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}

/// <summary>
/// 保存结果
/// </summary>
public record SaveResult
{
    public bool IsSuccess { get; init; }
    public string? OutputPath { get; init; }
    public int ModificationCount { get; init; }
    public IReadOnlyList<ModificationRecord>? Modifications { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 修改记录
/// </summary>
public record ModificationRecord
{
    public required ModificationType Type { get; init; }
    public required string Description { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// 修改类型
/// </summary>
public enum ModificationType
{
    TypeAdded,
    TypeRemoved,
    TypeModified,
    MethodAdded,
    MethodRemoved,
    MethodModified,
    MethodRenamed,
    FieldAdded,
    FieldRemoved,
    PropertyModified
}
