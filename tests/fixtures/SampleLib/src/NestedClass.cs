namespace SampleLib.BasicTypes;

/// <summary>
/// 嵌套类 - 用于测试嵌套类型分析
/// </summary>
public class OuterClass
{
    private readonly InnerClass _inner;

    public OuterClass()
    {
        _inner = new InnerClass(this);
    }

    public string GetInnerValue() => _inner.GetValue();

    public class InnerClass
    {
        private readonly OuterClass _outer;

        public InnerClass(OuterClass outer)
        {
            _outer = outer;
        }

        public string GetValue() => "Inner Value";

        public class DeepNestedClass
        {
            public string DeepValue { get; set; } = "Deep";

            public static DeepNestedClass Create() => new();
        }
    }

    private class PrivateInnerClass
    {
        public int Secret { get; set; }
    }

    protected class ProtectedInnerClass
    {
        public int Protected { get; set; }
    }
}

/// <summary>
/// 静态嵌套类
/// </summary>
public static class StaticOuterClass
{
    public static class StaticInnerClass
    {
        public static string StaticValue => "Static Inner";

        public static int Compute(int x) => x * 2;
    }
}
