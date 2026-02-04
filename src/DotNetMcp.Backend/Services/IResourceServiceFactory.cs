namespace DotNetMcp.Backend.Services;

/// <summary>
/// ResourceService 工厂接口
/// </summary>
public interface IResourceServiceFactory
{
    /// <summary>
    /// 获取指定程序集的 ResourceService
    /// </summary>
    /// <param name="mvid">程序集 MVID，为 null 时使用默认实例</param>
    /// <returns>ResourceService 实例，如果程序集不存在返回 null</returns>
    ResourceService? GetService(string? mvid = null);
}

/// <summary>
/// ResourceService 工厂实现
/// </summary>
public class ResourceServiceFactory : IResourceServiceFactory
{
    private readonly IAssemblyManager _assemblyManager;

    public ResourceServiceFactory(IAssemblyManager assemblyManager)
    {
        _assemblyManager = assemblyManager;
    }

    public ResourceService? GetService(string? mvid = null)
    {
        var context = _assemblyManager.Get(mvid);
        return context != null ? new ResourceService(context) : null;
    }
}
