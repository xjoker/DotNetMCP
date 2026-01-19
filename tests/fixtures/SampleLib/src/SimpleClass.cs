namespace SampleLib.BasicTypes;

/// <summary>
/// 简单类 - 用于测试基本类型分析
/// </summary>
public class SimpleClass
{
    private int _value;
    private readonly string _name;

    public SimpleClass(string name, int value)
    {
        _name = name;
        _value = value;
    }

    public int Add(int a, int b)
    {
        return a + b;
    }

    public int Subtract(int a, int b)
    {
        return a - b;
    }

    public int Multiply(int a, int b)
    {
        return a * b;
    }

    public double Divide(double a, double b)
    {
        if (b == 0) throw new DivideByZeroException();
        return a / b;
    }

    public string GetName() => _name;

    public int GetValue() => _value;

    public void SetValue(int value) => _value = value;

    private void PrivateMethod()
    {
        Console.WriteLine("This is private");
    }

    internal void InternalMethod()
    {
        Console.WriteLine("This is internal");
    }

    protected virtual void ProtectedMethod()
    {
        Console.WriteLine("This is protected");
    }
}

/// <summary>
/// 派生类 - 用于测试继承
/// </summary>
public class DerivedClass : SimpleClass
{
    public DerivedClass(string name, int value) : base(name, value)
    {
    }

    protected override void ProtectedMethod()
    {
        Console.WriteLine("Overridden protected method");
        base.ProtectedMethod();
    }

    public new int Add(int a, int b)
    {
        return a + b + 1; // 故意不同
    }
}
