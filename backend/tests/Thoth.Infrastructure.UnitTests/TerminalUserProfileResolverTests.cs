using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Thoth.Infrastructure.Terminals;

namespace Thoth.Infrastructure.UnitTests;

public sealed class TerminalUserProfileResolverTests
{
    [Fact]
    public void Resolve_prefers_explicit_user_profile()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Terminals:UserProfile"] = profile,
            })
            .Build();

        TerminalUserProfileResolver.Resolve(configuration).Should().Be(profile);
    }

    [Fact]
    public void Resolve_infers_user_profile_from_agent_usage_paths()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentUsage:Claude:CredentialsPath"] = Path.Combine(profile, ".claude", ".credentials.json"),
            })
            .Build();

        TerminalUserProfileResolver.Resolve(configuration).Should().Be(profile);
    }

    [Fact]
    public void Resolve_returns_null_when_no_profile_hint_exists()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        TerminalUserProfileResolver.Resolve(configuration).Should().BeNull();
    }
}