using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Api.IntegrationTests;

public sealed class AgentUsageApiTests(PromptTasksApiFactory factory) : IClassFixture<PromptTasksApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Get_agent_usage_returns_disabled_when_feature_is_disabled()
    {
        var client = factory.CreateClient();

        var usage = await client.GetFromJsonAsync<AgentUsageDto>("/api/agent-usage", JsonOptions);

        usage.Should().NotBeNull();
        usage!.Claude.Status.Should().Be(AgentUsageStatus.Disabled);
        usage.Codex.Status.Should().Be(AgentUsageStatus.Disabled);
        usage.Claude.Windows.Should().BeEmpty();
        usage.Codex.Windows.Should().BeEmpty();
    }
}
