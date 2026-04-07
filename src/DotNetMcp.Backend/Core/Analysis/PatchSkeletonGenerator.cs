using System.Text;
using Mono.Cecil;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// Harmony Patch 骨架代码生成器
/// </summary>
public class PatchSkeletonGenerator
{
    /// <summary>
    /// 生成 Harmony Patch 骨架代码
    /// </summary>
    public PatchSkeletonResult Generate(MethodDefinition method, string[] patchKinds)
    {
        var declaringType = method.DeclaringType;
        if (declaringType == null)
            return PatchSkeletonResult.Failure("Method has no declaring type");

        var code = new StringBuilder();
        var notes = new List<string>();

        code.AppendLine("using System;");
        code.AppendLine("using System.Collections.Generic;");
        code.AppendLine("using System.Reflection;");
        code.AppendLine("using System.Reflection.Emit;");
        code.AppendLine("using HarmonyLib;");
        if (!string.IsNullOrEmpty(declaringType.Namespace))
            code.AppendLine($"using {declaringType.Namespace};");
        code.AppendLine();

        code.AppendLine("namespace HarmonyPatches;");
        code.AppendLine();
        code.AppendLine($"[HarmonyPatch(typeof({GetTypeDisplayName(declaringType)}), \"{method.Name}\")]");
        code.AppendLine($"public class {SanitizeName(declaringType.Name)}_{SanitizeName(method.Name)}_Patch");
        code.AppendLine("{");

        foreach (var kind in patchKinds)
        {
            switch (kind.Trim().ToLower())
            {
                case "prefix":
                    GeneratePrefix(code, method);
                    break;
                case "postfix":
                    GeneratePostfix(code, method);
                    break;
                case "transpiler":
                    GenerateTranspiler(code, method);
                    break;
                case "finalizer":
                    GenerateFinalizer(code, method);
                    break;
            }
        }

        code.AppendLine("}");

        notes.Add("Add HarmonyLib NuGet package to your project");
        notes.Add("Call Harmony.CreateAndPatchAll() to apply patches");

        if (method.IsStatic)
            notes.Add("Target method is static — no __instance parameter available");
        else
            notes.Add("Target method is instance — __instance parameter available in patches");

        if (method.ReturnType.FullName != "System.Void")
            notes.Add("Target method has return value — __result parameter available in Postfix/Finalizer");

        return PatchSkeletonResult.Success(code.ToString(), notes);
    }

    private void GeneratePrefix(StringBuilder code, MethodDefinition method)
    {
        code.AppendLine("    [HarmonyPrefix]");
        code.Append("    static bool Prefix(");
        code.Append(BuildPatchParams(method, includeResult: false));
        code.AppendLine(")");
        code.AppendLine("    {");
        code.AppendLine("        // Return false to skip original method");
        code.AppendLine("        return true;");
        code.AppendLine("    }");
        code.AppendLine();
    }

    private void GeneratePostfix(StringBuilder code, MethodDefinition method)
    {
        code.AppendLine("    [HarmonyPostfix]");
        code.Append("    static void Postfix(");
        code.Append(BuildPatchParams(method, includeResult: true));
        code.AppendLine(")");
        code.AppendLine("    {");
        code.AppendLine("        // Runs after the original method");
        code.AppendLine("    }");
        code.AppendLine();
    }

    private void GenerateTranspiler(StringBuilder code, MethodDefinition method)
    {
        code.AppendLine("    [HarmonyTranspiler]");
        code.AppendLine("    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)");
        code.AppendLine("    {");
        code.AppendLine("        var codes = new List<CodeInstruction>(instructions);");
        code.AppendLine("        // Modify IL instructions here");
        code.AppendLine("        return codes;");
        code.AppendLine("    }");
        code.AppendLine();
    }

    private void GenerateFinalizer(StringBuilder code, MethodDefinition method)
    {
        code.AppendLine("    [HarmonyFinalizer]");
        code.Append("    static Exception? Finalizer(Exception __exception");
        if (!method.IsStatic)
            code.Append($", {GetTypeDisplayName(method.DeclaringType)} __instance");
        if (method.ReturnType.FullName != "System.Void")
            code.Append($", ref {GetTypeDisplayName(method.ReturnType)} __result");
        code.AppendLine(")");
        code.AppendLine("    {");
        code.AppendLine("        // Runs after method, even on exception");
        code.AppendLine("        return null; // null = swallow exception");
        code.AppendLine("    }");
        code.AppendLine();
    }

    private string BuildPatchParams(MethodDefinition method, bool includeResult)
    {
        var parts = new List<string>();

        if (!method.IsStatic)
            parts.Add($"{GetTypeDisplayName(method.DeclaringType)} __instance");

        foreach (var param in method.Parameters)
        {
            var name = string.IsNullOrEmpty(param.Name) ? $"param{param.Index}" : param.Name;
            parts.Add($"{GetTypeDisplayName(param.ParameterType)} {name}");
        }

        if (includeResult && method.ReturnType.FullName != "System.Void")
            parts.Add($"ref {GetTypeDisplayName(method.ReturnType)} __result");

        return string.Join(", ", parts);
    }

    private static string GetTypeDisplayName(TypeReference type)
    {
        return type.FullName switch
        {
            "System.String" => "string",
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Boolean" => "bool",
            "System.Void" => "void",
            "System.Object" => "object",
            "System.Double" => "double",
            "System.Single" => "float",
            "System.Byte" => "byte",
            "System.Int16" => "short",
            "System.Char" => "char",
            "System.Decimal" => "decimal",
            _ => type.Name
        };
    }

    private static string SanitizeName(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
        }
        return sb.Length > 0 ? sb.ToString() : "Unknown";
    }
}

/// <summary>
/// Patch 骨架生成结果
/// </summary>
public class PatchSkeletonResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Code { get; set; }
    public List<string> Notes { get; set; } = new();

    public static PatchSkeletonResult Success(string code, List<string> notes)
        => new() { IsSuccess = true, Code = code, Notes = notes };

    public static PatchSkeletonResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}
