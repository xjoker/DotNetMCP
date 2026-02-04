using DotNetMcp.Server.Backend;
using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Context;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server.Tests.Fixtures;

/// <summary>
/// 测试程序集夹具 - 提供测试用的 Mock 对象
/// </summary>
public class TestAssemblyFixture : IDisposable
{
    public Mock<IBackendRegistry> MockRegistry { get; }
    public Mock<IBackend> MockBackend { get; }
    public Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    public Mock<ILogger<RemoteBackend>> MockRemoteBackendLogger { get; }

    public TestAssemblyFixture()
    {
        MockRegistry = new Mock<IBackendRegistry>();
        MockBackend = new Mock<IBackend>();
        MockHttpClientFactory = new Mock<IHttpClientFactory>();
        MockRemoteBackendLogger = new Mock<ILogger<RemoteBackend>>();

        // 设置默认后端行为
        MockBackend.Setup(b => b.Id).Returns("local");
        MockBackend.Setup(b => b.Name).Returns("Local Backend");
        MockBackend.Setup(b => b.Type).Returns(BackendType.Local);
        MockBackend.Setup(b => b.IsHealthy).Returns(true);

        // 默认注册表返回 Mock 后端
        MockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns(MockBackend.Object);
        MockRegistry.Setup(r => r.GetAll()).Returns(new List<IBackend> { MockBackend.Object });
        MockRegistry.Setup(r => r.DefaultBackendId).Returns("local");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
