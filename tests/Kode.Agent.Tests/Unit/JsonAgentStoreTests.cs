using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Store.Json;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public sealed class JsonAgentStoreTests
{
    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenOnlyDirectoryExistsButMetaMissing()
    {
        using var temp = new TempDir();
        var store = new JsonAgentStore(temp.Path);

        var agentId = "agt_test_no_meta";
        Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, agentId));

        var exists = await store.ExistsAsync(agentId);
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenMetaExists()
    {
        using var temp = new TempDir();
        var store = new JsonAgentStore(temp.Path);

        var agentId = "agt_test_with_meta";
        await store.SaveInfoAsync(agentId, new AgentInfo
        {
            AgentId = agentId,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            Lineage = [],
            Metadata = new Dictionary<string, System.Text.Json.JsonElement>()
        });

        var exists = await store.ExistsAsync(agentId);
        Assert.True(exists);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kode-agent-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }
}

