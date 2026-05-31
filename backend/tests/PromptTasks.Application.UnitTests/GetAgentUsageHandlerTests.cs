using FluentAssertions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.AgentUsage.Queries.GetAgentUsage;

namespace PromptTasks.Application.UnitTests;

public sealed class GetAgentUsageHandlerTests
{
    [Fact]
    public async Task Handle_returns_reader_result()
    {
        var expected = new AgentUsageDto(
            new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero),
            new AgentUsageInfo("Claude", AgentUsageStatus.Ok, null, null, "max", Array.Empty<AgentUsageWindow>()),
            new AgentUsageInfo("Codex", AgentUsageStatus.NoData, null, "No snapshot", null, Array.Empty<AgentUsageWindow>()));
        var handler = new GetAgentUsageHandler(new FakeAgentUsageReader(expected));

        var result = await handler.Handle(new GetAgentUsageQuery(), CancellationToken.None);

        result.Should().Be(expected);
    }

    private sealed class FakeAgentUsageReader(AgentUsageDto result) : IAgentUsageReader
    {
        public Task<AgentUsageDto> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
