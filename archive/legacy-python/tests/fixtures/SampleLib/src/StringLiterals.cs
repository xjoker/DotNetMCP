namespace SampleLib.Strings;

/// <summary>
/// 字符串字面量 - 用于测试字符串搜索
/// </summary>
public static class StringLiterals
{
    public const string AppName = "SampleApplication";
    public const string Version = "1.0.0";
    public const string ApiEndpoint = "https://api.example.com/v1";
    public const string SecretKey = "sk_test_secret_key_12345";
    
    public static readonly string[] Keywords = 
    {
        "hello",
        "world",
        "test",
        "sample",
        "dotnet"
    };

    public static string GetGreeting(string name)
    {
        return $"Hello, {name}! Welcome to {AppName} version {Version}.";
    }

    public static string GetErrorMessage(int code)
    {
        return code switch
        {
            404 => "Resource not found",
            500 => "Internal server error",
            401 => "Unauthorized access",
            403 => "Forbidden",
            _ => $"Unknown error code: {code}"
        };
    }

    public static string MultilineString => @"
        This is a multiline string.
        It spans multiple lines.
        Used for testing string literal detection.
    ";

    public static string UnicodeString => "中文字符串 🎉 émoji";

    public static string InterpolatedString(int value) => $"The value is {value} and doubled is {value * 2}";
}
