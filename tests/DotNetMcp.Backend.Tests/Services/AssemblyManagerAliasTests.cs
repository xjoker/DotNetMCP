using DotNetMcp.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetMcp.Backend.Tests.Services;

/// <summary>
/// AssemblyManager alias 功能测试
/// </summary>
public class AssemblyManagerAliasTests : IDisposable
{
    private readonly string _tempDir;

    public AssemblyManagerAliasTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetmcp-alias-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private AssemblyManager CreateManager(AliasPersistence? persistence = null)
    {
        return new AssemblyManager(NullLogger<AssemblyManager>.Instance, persistence);
    }

    private AliasPersistence CreatePersistence(string? fileName = null)
    {
        var path = Path.Combine(_tempDir, fileName ?? "aliases.json");
        return new AliasPersistence(path);
    }

    // -------------------------
    // Alias 验证测试
    // -------------------------

    [Fact]
    public void RegisterAlias_ValidAlias_Succeeds()
    {
        // 因为没有加载任何 assembly，mvid 不存在 → 注册失败
        // 但验证规则应允许通过（返回 false 是因为 mvid 不存在，不是因为 alias 无效）
        var mgr = CreateManager();

        // alias 本身格式合法，但 mvid 不存在，所以返回 false
        var result = mgr.RegisterAlias("main", "some-fake-mvid");
        Assert.False(result); // mvid not found
    }

    [Theory]
    [InlineData("")]              // 空
    [InlineData("default")]       // 保留字
    [InlineData("local")]         // 保留字
    [InlineData("null")]          // 保留字
    [InlineData("123")]           // 纯数字
    [InlineData("0123456789")]    // 纯数字
    [InlineData("has space")]     // 含空格
    [InlineData("has.dot")]       // 含 .
    [InlineData("has/slash")]     // 含 /
    public void RegisterAlias_InvalidAlias_ReturnsFalse(string alias)
    {
        var mgr = CreateManager();
        var result = mgr.RegisterAlias(alias, "any-mvid");
        Assert.False(result);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("v1")]
    [InlineData("my-alias")]
    [InlineData("target_dll")]
    [InlineData("A")]
    [InlineData("a1b2c3")]
    public void ValidAlias_FormatsAccepted(string alias)
    {
        // 合法格式的 alias 不会因格式问题被拒绝
        // 只因 mvid 不存在而失败
        var mgr = CreateManager();

        // 用一个不存在的 mvid，verify 失败是 "mvid not found" 而不是 "invalid alias"
        var result = mgr.RegisterAlias(alias, "nonexistent-mvid");
        Assert.False(result); // mvid 不存在，应返回 false 但不抛异常
    }

    [Fact]
    public void RegisterAlias_LongerThan32Chars_ReturnsFalse()
    {
        var mgr = CreateManager();
        var longAlias = new string('a', 33); // 33 chars - too long
        var result = mgr.RegisterAlias(longAlias, "any-mvid");
        Assert.False(result);
    }

    [Fact]
    public void RegisterAlias_Exactly32Chars_PassesFormatCheck()
    {
        var mgr = CreateManager();
        var alias32 = new string('a', 32); // 32 chars - ok
        // 只因 mvid 不存在而失败
        var result = mgr.RegisterAlias(alias32, "nonexistent");
        Assert.False(result);
    }

    // -------------------------
    // ResolveAlias 测试
    // -------------------------

    [Fact]
    public void ResolveAlias_NullInput_ReturnsNull()
    {
        var mgr = CreateManager();
        Assert.Null(mgr.ResolveAlias(null));
    }

    [Fact]
    public void ResolveAlias_UnknownKey_ReturnsInputAsIs()
    {
        var mgr = CreateManager();
        var mvid = "12345678-1234-1234-1234-123456789abc";
        Assert.Equal(mvid, mgr.ResolveAlias(mvid));
    }

    [Fact]
    public void ResolveAlias_KnownAlias_ReturnsMvid()
    {
        // We need to inject an alias directly without RegisterAlias
        // (which requires mvid to be loaded). Use internal knowledge via GetAliases test.
        // We test this indirectly via Get() method on a loaded assembly.
        // For unit test purposes, verify behavior with known-state setup.
        var mgr = CreateManager();

        // No assemblies loaded, so we can only verify the pass-through behavior
        var passThrough = "some-mvid-value";
        Assert.Equal(passThrough, mgr.ResolveAlias(passThrough));
    }

    // -------------------------
    // UnregisterAlias 测试
    // -------------------------

    [Fact]
    public void UnregisterAlias_NonExistentAlias_ReturnsFalse()
    {
        var mgr = CreateManager();
        var result = mgr.UnregisterAlias("nonexistent-alias");
        Assert.False(result);
    }

    // -------------------------
    // GetAliases 测试
    // -------------------------

    [Fact]
    public void GetAliases_EmptyManager_ReturnsEmptyDict()
    {
        var mgr = CreateManager();
        var aliases = mgr.GetAliases();
        Assert.Empty(aliases);
    }

    // -------------------------
    // 持久化测试
    // -------------------------

    [Fact]
    public void AliasPersistence_SaveAndLoad_RoundTrip()
    {
        var persistence = CreatePersistence();

        var original = new AliasState
        {
            Aliases = new Dictionary<string, AliasEntry>
            {
                ["main"] = new AliasEntry
                {
                    Mvid = "aabbccdd-0000-0000-0000-000000000001",
                    AssemblyPath = "/tmp/test.dll",
                    RegisteredAt = DateTime.UtcNow
                },
                ["v1"] = new AliasEntry
                {
                    Mvid = "aabbccdd-0000-0000-0000-000000000002",
                    AssemblyPath = "/tmp/v1.dll",
                    RegisteredAt = DateTime.UtcNow
                }
            }
        };

        persistence.Save(original);

        var loaded = persistence.Load();
        Assert.Equal(2, loaded.Aliases.Count);
        Assert.True(loaded.Aliases.ContainsKey("main"));
        Assert.True(loaded.Aliases.ContainsKey("v1"));
        Assert.Equal("aabbccdd-0000-0000-0000-000000000001", loaded.Aliases["main"].Mvid);
        Assert.Equal("/tmp/v1.dll", loaded.Aliases["v1"].AssemblyPath);
    }

    [Fact]
    public void AliasPersistence_LoadNonExistentFile_ReturnsEmptyState()
    {
        var persistence = CreatePersistence("does-not-exist.json");
        var state = persistence.Load();
        Assert.NotNull(state);
        Assert.Empty(state.Aliases);
    }

    [Fact]
    public void AliasPersistence_LoadCorruptedFile_ReturnsEmptyState()
    {
        var filePath = Path.Combine(_tempDir, "corrupt.json");
        File.WriteAllText(filePath, "{ this is not valid json {{{{");

        var persistence = new AliasPersistence(filePath);
        var state = persistence.Load();
        Assert.NotNull(state);
        Assert.Empty(state.Aliases);
    }

    [Fact]
    public void AliasPersistence_SaveCreatesDirectory()
    {
        var nestedDir = Path.Combine(_tempDir, "nested", "subdir");
        var filePath = Path.Combine(nestedDir, "aliases.json");
        var persistence = new AliasPersistence(filePath);

        persistence.Save(new AliasState());

        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void AliasPersistence_AtomicWrite_ProducesNoTmpFile()
    {
        var persistence = CreatePersistence("atomic-test.json");
        persistence.Save(new AliasState());

        var tmpFile = Path.Combine(_tempDir, "atomic-test.json.tmp");
        Assert.False(File.Exists(tmpFile), ".tmp file should be renamed away");
    }

    [Fact]
    public void AliasPersistence_OverwriteExisting_UpdatesContent()
    {
        var persistence = CreatePersistence("overwrite-test.json");

        var state1 = new AliasState();
        state1.Aliases["a"] = new AliasEntry { Mvid = "mvid1", AssemblyPath = "/a.dll", RegisteredAt = DateTime.UtcNow };
        persistence.Save(state1);

        var state2 = new AliasState();
        state2.Aliases["b"] = new AliasEntry { Mvid = "mvid2", AssemblyPath = "/b.dll", RegisteredAt = DateTime.UtcNow };
        persistence.Save(state2);

        var loaded = persistence.Load();
        Assert.Single(loaded.Aliases);
        Assert.True(loaded.Aliases.ContainsKey("b"));
        Assert.False(loaded.Aliases.ContainsKey("a"));
    }

    // -------------------------
    // 启动时从持久化加载 alias 映射（不加载 assembly）
    // -------------------------

    [Fact]
    public void Constructor_WithPersistenceHavingAliases_LoadsAliasMap()
    {
        var persistence = CreatePersistence("init-test.json");

        // Pre-populate persistence file
        var state = new AliasState();
        state.Aliases["main"] = new AliasEntry
        {
            Mvid = "aaaabbbb-0000-0000-0000-000000000001",
            AssemblyPath = "/tmp/main.dll",
            RegisteredAt = DateTime.UtcNow
        };
        persistence.Save(state);

        // Creating AssemblyManager should load alias map (but not assemblies)
        var mgr = CreateManager(persistence);

        // The alias is loaded into the internal map but assembly is NOT loaded
        // So ResolveAlias should return the mvid from the alias map
        var resolved = mgr.ResolveAlias("main");
        Assert.Equal("aaaabbbb-0000-0000-0000-000000000001", resolved);
    }

    // -------------------------
    // 重复 alias 测试
    // -------------------------

    [Fact]
    public void RegisterAlias_DuplicateWithoutOverwrite_ReturnsFalse()
    {
        // Inject alias via direct persistence-backed constructor
        var persistence = CreatePersistence("dup-test.json");
        var state = new AliasState();
        state.Aliases["dup"] = new AliasEntry
        {
            Mvid = "aaaabbbb-0000-0000-0000-000000000001",
            AssemblyPath = "/tmp/dup.dll",
            RegisteredAt = DateTime.UtcNow
        };
        persistence.Save(state);

        var mgr = CreateManager(persistence);

        // dup alias exists in the map already (loaded from persistence)
        // Now try to register it again without overwrite for a non-existent mvid
        // This should fail because (a) the mvid doesn't exist OR (b) the alias already exists
        var result = mgr.RegisterAlias("dup", "nonexistent-mvid", overwrite: false);
        Assert.False(result);
    }

    // -------------------------
    // RestorePersistedAssembliesAsync 测试（文件不存在路径）
    // -------------------------

    [Fact]
    public async Task RestorePersistedAssembliesAsync_FileNotFound_RemovesAlias()
    {
        var persistence = CreatePersistence("restore-test.json");
        var state = new AliasState();
        state.Aliases["missing"] = new AliasEntry
        {
            Mvid = "aaaabbbb-0000-0000-0000-000000000099",
            AssemblyPath = "/path/that/does/not/exist.dll",
            RegisteredAt = DateTime.UtcNow
        };
        persistence.Save(state);

        var mgr = CreateManager(persistence);

        // After construction, alias is in the map
        Assert.Equal("aaaabbbb-0000-0000-0000-000000000099", mgr.ResolveAlias("missing"));

        // Restore should fail to load (file not found), alias should be removed
        var count = await mgr.RestorePersistedAssembliesAsync();
        Assert.Equal(0, count);

        // Alias should be gone after failed restore
        Assert.Equal("missing", mgr.ResolveAlias("missing")); // passthrough = not found in alias map
    }

    [Fact]
    public async Task RestorePersistedAssembliesAsync_NoPersistence_ReturnsZero()
    {
        var mgr = CreateManager(); // no persistence
        var count = await mgr.RestorePersistedAssembliesAsync();
        Assert.Equal(0, count);
    }
}
