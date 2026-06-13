using Microsoft.Extensions.Configuration;
using Porta.Pty;

namespace Thoth.Infrastructure.Terminals;

public sealed class PortaPtyConnectionFactory(IConfiguration configuration) : IPtyConnectionFactory
{
    private readonly string? _userProfileHint = TerminalUserProfileResolver.Resolve(configuration);

    public async Task<IPtyConnection> CreateAsync(
        string shell,
        string cwd,
        int cols,
        int rows,
        CancellationToken cancellationToken)
    {
        var options = new PtyOptions
        {
            Name = "ThothTerminal",
            Cols = cols,
            Rows = rows,
            Cwd = cwd,
            App = shell,
            CommandLine = TerminalShellBootstrap.IsPowerShell(shell)
                ? TerminalShellBootstrap.BuildPowerShellStartupArgs(cwd)
                : [],
            Environment = TerminalEnvironmentBootstrap.BuildSpawnEnvironment(_userProfileHint),
        };

        var connection = await PtyProvider.SpawnAsync(options, cancellationToken);
        return new PortaPtyConnectionAdapter(connection);
    }
}