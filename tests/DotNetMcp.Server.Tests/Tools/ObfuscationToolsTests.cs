using DotNetMcp.Server.Backend;
using DotNetMcp.Server.Tools;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Server.Tests.Tools;

/// <summary>
/// detect_obfuscation MCP 工具单元测试
/// </summary>
public class ObfuscationToolsTests
{
    private readonly Mock<IBackendRegistry> _mockRegistry;
    private readonly Mock<IBackend> _mockBackend;
    private readonly AnalysisTools _tools;

    public ObfuscationToolsTests()
    {
        _mockRegistry = new Mock<IBackendRegistry>();
        _mockBackend = new Mock<IBackend>();
        _mockBackend.Setup(b => b.Id).Returns("local");
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns(_mockBackend.Object);
        _tools = new AnalysisTools(_mockRegistry.Object);
    }

    [Fact]
    public async Task DetectObfuscation_WhenNotObfuscated_ReturnsSuccessWithScore()
    {
        // Arrange
        var serviceResult = new ObfuscationDetectionServiceResult
        {
            IsSuccess = true,
            IsObfuscated = false,
            ObfuscationScore = 5,
            Confidence = "High",
            DetectedObfuscators = new List<string>(),
            Indicators = new List<ObfuscationIndicatorInfo>(),
            Statistics = new ObfuscationStatsInfo
            {
                TotalTypes = 20,
                TotalMethods = 80,
                TotalFields = 30,
                ShortTypeNames = 0,
                InvalidTypeNames = 0,
                InvalidMethodNames = 0,
                InvalidFieldNames = 0,
                RandomTypeNames = 0,
                FlattenedMethods = 0,
                ProxyMethods = 0
            }
        };

        _mockBackend.Setup(b => b.DetectObfuscationAsync("", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _tools.DetectObfuscation();

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsObfuscated);
        Assert.IsType<int>(result.ObfuscationScore);
        Assert.True(result.ObfuscationScore >= 0 && result.ObfuscationScore <= 100);
        Assert.Equal("High", result.Confidence);
        Assert.NotNull(result.Stats);
        Assert.Equal(20, result.Stats!.TotalTypes);
    }

    [Fact]
    public async Task DetectObfuscation_WhenObfuscated_ReturnsTopIndicatorsSortedBySeverity()
    {
        // Arrange
        var indicators = new List<ObfuscationIndicatorInfo>
        {
            new ObfuscationIndicatorInfo
            {
                Category = "NamingConvention",
                Severity = "Low",
                Description = "Some low severity indicator",
                Location = "TypeA",
                Evidence = new List<string> { "evidence1" }
            },
            new ObfuscationIndicatorInfo
            {
                Category = "ControlFlow",
                Severity = "High",
                Description = "Control flow flattening detected",
                Location = "TypeB.MethodX",
                Evidence = new List<string> { "evidence2" }
            },
            new ObfuscationIndicatorInfo
            {
                Category = "NamingConvention",
                Severity = "Critical",
                Description = "All type names are 1-2 chars",
                Location = "Assembly",
                Evidence = new List<string> { "a", "b", "c" }
            }
        };

        var serviceResult = new ObfuscationDetectionServiceResult
        {
            IsSuccess = true,
            IsObfuscated = true,
            ObfuscationScore = 85,
            Confidence = "High",
            DetectedObfuscators = new List<string> { "ConfuserEx" },
            Indicators = indicators,
            Statistics = new ObfuscationStatsInfo
            {
                TotalTypes = 50,
                TotalMethods = 200,
                ShortTypeNames = 45,
                InvalidTypeNames = 12,
                FlattenedMethods = 30
            }
        };

        _mockBackend.Setup(b => b.DetectObfuscationAsync("", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _tools.DetectObfuscation();

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsObfuscated);
        Assert.Equal(85, result.ObfuscationScore);
        Assert.NotNull(result.DetectedObfuscators);
        Assert.Contains("ConfuserEx", result.DetectedObfuscators!);

        // TopIndicators 应按 Severity 排序：Critical → High → Low
        Assert.NotNull(result.TopIndicators);
        Assert.Equal(3, result.TopIndicators!.Length);
        Assert.Equal("Critical", result.TopIndicators[0].Severity);
        Assert.Equal("High", result.TopIndicators[1].Severity);
        Assert.Equal("Low", result.TopIndicators[2].Severity);
    }

    [Fact]
    public async Task DetectObfuscation_WhenBackendUnavailable_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AnalysisTools(_mockRegistry.Object);

        // Act
        var result = await tools.DetectObfuscation();

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("No backend available", result.Error);
    }
}
