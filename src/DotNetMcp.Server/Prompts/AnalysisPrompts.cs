using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DotNetMcp.Server.Prompts;

/// <summary>
/// 分析相关的 MCP Prompts (Skills)
/// </summary>
[McpServerPromptType]
public sealed class AnalysisPrompts
{
    /// <summary>
    /// 标准类型分析工作流
    /// </summary>
    [McpServerPrompt(Name = "analyze-type")]
    [Description("Standard workflow for thorough type analysis. Guides through decompilation, reference finding, and call graph building.")]
    public string AnalyzeType()
    {
        return """
            # Standard Type Analysis Workflow

            Follow these steps to thoroughly analyze a .NET type:

            ## Step 1: Load the Assembly
            ```
            load_assembly path="/path/to/assembly.dll"
            ```
            Note the returned MVID for subsequent operations.

            ## Step 2: Find the Target Type
            ```
            search_types keyword="ClassName"
            ```
            Locate the exact full type name (e.g., `MyNamespace.MyClass`).

            ## Step 3: Decompile the Type
            ```
            decompile_type typeName="MyNamespace.MyClass" language="csharp"
            ```
            Read and understand the implementation.

            ## Step 4: Find References
            ```
            find_type_references typeName="MyNamespace.MyClass"
            ```
            Understand where and how this type is used.

            ## Step 5: Build Call Graph (Optional)
            For key methods, trace the call hierarchy:
            ```
            get_call_graph typeName="MyNamespace.MyClass" methodName="ImportantMethod" direction="callees"
            ```

            ## Analysis Checklist
            - [ ] Understand the type's purpose and responsibilities
            - [ ] Identify dependencies and consumers
            - [ ] Note any design patterns used
            - [ ] Check for potential issues or code smells
            """;
    }

    /// <summary>
    /// 安全的方法修改工作流
    /// </summary>
    [McpServerPrompt(Name = "patch-method")]
    [Description("Safe method modification workflow with validation. Ensures proper analysis before and verification after patching.")]
    public string PatchMethod()
    {
        return """
            # Safe Method Modification Workflow

            Follow these steps to safely modify a method in a .NET assembly:

            ## Step 1: Analyze Current Implementation
            ```
            decompile_method typeName="MyNamespace.MyClass" methodName="TargetMethod" language="csharp"
            ```
            Understand the current logic before making changes.

            ## Step 2: Check Impact
            ```
            find_method_calls typeName="MyNamespace.MyClass" methodName="TargetMethod"
            ```
            Identify all callers that may be affected by the change.

            ## Step 3: View IL (Optional)
            ```
            decompile_method typeName="MyNamespace.MyClass" methodName="TargetMethod" language="il"
            ```
            Understand the IL structure for complex modifications.

            ## Step 4: Make Modification
            Choose one approach:

            **Option A: Inject at Entry** (add logging/validation)
            ```
            inject_at_entry methodFullName="MyNamespace.MyClass.TargetMethod" instructions=[
              {"opCode":"ldstr","stringValue":"Method called"},
              {"opCode":"call","stringValue":"System.Console::WriteLine"}
            ]
            ```

            **Option B: Replace Body** (complete rewrite)
            ```
            replace_method_body methodFullName="MyNamespace.MyClass.TargetMethod" instructions=[
              {"opCode":"ldc.i4","intValue":0},
              {"opCode":"ret"}
            ]
            ```

            ## Step 5: Save Changes
            ```
            save_assembly outputPath="/path/to/modified.dll"
            ```

            ## Step 6: Verify
            Reload and decompile to verify changes:
            ```
            load_assembly path="/path/to/modified.dll"
            decompile_method typeName="MyNamespace.MyClass" methodName="TargetMethod"
            ```

            ## Safety Checklist
            - [ ] Backup original assembly before modification
            - [ ] Understand all callers and potential impact
            - [ ] Test modified assembly thoroughly
            - [ ] Keep modification minimal and focused
            """;
    }

    /// <summary>
    /// 安全审计工作流
    /// </summary>
    [McpServerPrompt(Name = "find-vulnerability")]
    [Description("Security audit workflow for finding potential vulnerabilities in .NET assemblies. Searches for hardcoded secrets, weak crypto, and suspicious patterns.")]
    public string FindVulnerability()
    {
        return """
            # Security Audit Workflow

            Follow these steps to audit a .NET assembly for potential vulnerabilities:

            ## Step 1: Load the Target Assembly
            ```
            load_assembly path="/path/to/target.dll"
            ```

            ## Step 2: Search for Hardcoded Secrets
            ```mermaid
            flowchart TD
                A[Start Audit] --> B[Search Sensitive Strings]
                B --> C{Found?}
                C -->|Yes| D[Analyze Context]
                C -->|No| E[Continue to Crypto]
                D --> E
                E --> F[Find Crypto Usage]
                F --> G[Trace Data Flow]
                G --> H[Document Findings]
            ```

            Search for common secret patterns:
            ```
            search_strings query="password" mode="contains"
            search_strings query="secret" mode="contains"
            search_strings query="api_key" mode="contains"
            search_strings query="connectionstring" mode="contains"
            search_strings query="bearer" mode="contains"
            ```

            ## Step 3: Find Cryptography Usage
            ```
            search_types keyword="Crypto"
            search_types keyword="Encrypt"
            search_types keyword="Hash"
            search_types keyword="Cipher"
            ```

            Look for weak algorithms:
            - MD5, SHA1 (for security purposes)
            - DES, 3DES, RC2, RC4
            - ECB mode usage

            ## Step 4: Trace Data Flow
            For suspicious methods, build call graphs:
            ```
            get_call_graph typeName="SuspiciousClass" methodName="ProcessCredentials" direction="callers"
            ```

            ## Step 5: Check Input Validation
            Look for potential injection points:
            ```
            search_strings query="SELECT" mode="contains"
            search_strings query="eval" mode="contains"
            search_strings query="Process.Start" mode="contains"
            ```

            ## Common Vulnerabilities Checklist
            - [ ] Hardcoded credentials or API keys
            - [ ] Weak cryptographic algorithms (MD5, SHA1, DES)
            - [ ] SQL injection patterns (string concatenation with queries)
            - [ ] Command injection (unvalidated Process.Start)
            - [ ] Path traversal (unvalidated file paths)
            - [ ] Insecure deserialization (BinaryFormatter, etc.)
            - [ ] Sensitive data in logs or exceptions

            ## Reporting
            Document findings with:
            1. Location (type, method, IL offset)
            2. Severity (Critical/High/Medium/Low)
            3. Description of the vulnerability
            4. Recommended remediation
            """;
    }
}
