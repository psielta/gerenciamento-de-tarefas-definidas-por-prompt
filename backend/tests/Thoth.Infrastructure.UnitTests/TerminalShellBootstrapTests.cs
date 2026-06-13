using FluentAssertions;
using Thoth.Infrastructure.Terminals;

namespace Thoth.Infrastructure.UnitTests;

public sealed class TerminalShellBootstrapTests
{
    [Theory]
    [InlineData(@"C:\PowerShell\7\pwsh.exe", true)]
    [InlineData("pwsh.exe", true)]
    [InlineData(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", true)]
    [InlineData("powershell.exe", true)]
    [InlineData("cmd.exe", false)]
    public void IsPowerShell_detects_supported_shells(string shell, bool expected)
    {
        TerminalShellBootstrap.IsPowerShell(shell).Should().Be(expected);
    }

    [Fact]
    public void BuildPowerShellStartupArgs_escapes_single_quotes_in_path()
    {
        var args = TerminalShellBootstrap.BuildPowerShellStartupArgs(@"D:\repos\it's-here");

        args.Should().Equal(
            "-NoLogo",
            "-NoExit",
            "-Command",
            "Set-Location -LiteralPath 'D:\\repos\\it''s-here'");
    }
}