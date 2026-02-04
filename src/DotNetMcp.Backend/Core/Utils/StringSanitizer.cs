namespace DotNetMcp.Backend.Core.Utils;

/// <summary>
/// 字符串安全处理工具类
/// 用于处理混淆程序集中可能包含的非可读字符
/// </summary>
public static class StringSanitizer
{
    /// <summary>
    /// 转义不可打印字符为安全格式
    /// 确保字符串可以安全地用于 JSON 序列化、日志输出、API 响应
    /// </summary>
    /// <param name="input">原始字符串</param>
    /// <param name="maxLength">最大长度，超出部分截断（0 表示不限制）</param>
    /// <returns>安全的可打印字符串</returns>
    public static string Sanitize(string? input, int maxLength = 0)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = new System.Text.StringBuilder(input.Length * 2);

        foreach (var c in input)
        {
            if (c < 32)
            {
                // 控制字符 -> \xHH
                result.Append($"\\x{(int)c:X2}");
            }
            else if (c == 127)
            {
                // DEL 字符
                result.Append("\\x7F");
            }
            else if (c > 127 && !char.IsLetterOrDigit(c) && !char.IsPunctuation(c) && !char.IsSymbol(c))
            {
                // 非标准 Unicode 字符 -> \uHHHH
                result.Append($"\\u{(int)c:X4}");
            }
            else if (c == '\\')
            {
                result.Append("\\\\");
            }
            else if (c == '"')
            {
                result.Append("\\\"");
            }
            else
            {
                result.Append(c);
            }

            // 长度限制
            if (maxLength > 0 && result.Length >= maxLength)
            {
                result.Length = maxLength - 3;
                result.Append("...");
                break;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 安全处理类型名
    /// </summary>
    public static string SanitizeTypeName(string? typeName) => Sanitize(typeName, 500);

    /// <summary>
    /// 安全处理方法名
    /// </summary>
    public static string SanitizeMethodName(string? methodName) => Sanitize(methodName, 200);

    /// <summary>
    /// 安全处理字段名
    /// </summary>
    public static string SanitizeFieldName(string? fieldName) => Sanitize(fieldName, 200);

    /// <summary>
    /// 安全处理用于日志的字符串（限制长度）
    /// </summary>
    public static string SanitizeForLog(string? input) => Sanitize(input, 100);

    /// <summary>
    /// 检查字符串是否包含非法字符
    /// </summary>
    public static bool ContainsInvalidChars(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        foreach (var c in input)
        {
            if (c < 32 || c == 127 || c == '\0')
                return true;
        }
        return false;
    }

    /// <summary>
    /// 检查是否为有效的 .NET 标识符
    /// </summary>
    public static bool IsValidIdentifier(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // 编译器生成的名称以 < 开头是合法的
        if (name.StartsWith("<"))
            return true;

        // 第一个字符必须是字母或下划线
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        // 后续字符必须是字母、数字或下划线
        for (int i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }

        return true;
    }
}
