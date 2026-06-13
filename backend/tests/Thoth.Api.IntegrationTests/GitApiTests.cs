using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Thoth.Application.Common.Models;

namespace Thoth.Api.IntegrationTests;

public sealed class GitApiTests(ThothApiFactory factory) : IClassFixture<ThothApiFactory>, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"prompttasks-git-api-{Guid.NewGuid():N}");

    [Fact]
    public async Task Git_endpoints_return_status_original_content_and_diff()
    {
        SkipIfGitUnavailable();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src"));
        await RunGit("init");
        await RunGit("config", "user.name", "Test");
        await RunGit("config", "user.email", "test@example.com");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "src", "app.txt"), "original app");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "old-name.txt"), "rename me");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "deleted.txt"), "remove me");
        await RunGit("add", ".");
        await RunGit("-c", "commit.gpgsign=false", "commit", "-m", "init");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "src", "app.txt"), "changed app");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "untracked.txt"), "new content");
        File.Delete(Path.Combine(_tempRoot, "deleted.txt"));
        await RunGit("mv", "old-name.txt", "new-name.txt");

        var client = factory.CreateClient();
        var wd = await CreateWorkingDirectory(client, _tempRoot);

        var status = await client.GetFromJsonAsync<GitFileStatusDto[]>(
            $"/api/git/status?workingDirectoryId={wd.Id}",
            JsonOptions);
        status.Should().Contain(item => item.Path == "src/app.txt" && item.Status == GitFileChangeStatus.Modified);
        status.Should().Contain(item => item.Path == "untracked.txt" && item.Status == GitFileChangeStatus.Untracked);
        status.Should().Contain(item => item.Path == "deleted.txt" && item.Status == GitFileChangeStatus.Deleted);
        status.Should().Contain(item =>
            item.Path == "new-name.txt" &&
            item.OriginalPath == "old-name.txt" &&
            item.Status == GitFileChangeStatus.Renamed);

        var original = await client.GetFromJsonAsync<GitOriginalFileDto>(
            $"/api/git/original-file?workingDirectoryId={wd.Id}&path={Uri.EscapeDataString("src/app.txt")}",
            JsonOptions);
        original!.Content.Should().Be("original app");

        var untrackedOriginal = await client.GetFromJsonAsync<GitOriginalFileDto>(
            $"/api/git/original-file?workingDirectoryId={wd.Id}&path={Uri.EscapeDataString("untracked.txt")}",
            JsonOptions);
        untrackedOriginal!.Content.Should().BeEmpty();

        var diff = await client.GetFromJsonAsync<GitDiffDto>(
            $"/api/git/diff?workingDirectoryId={wd.Id}&path={Uri.EscapeDataString("src/app.txt")}",
            JsonOptions);
        diff!.Diff.Should().Contain("diff --git");
        diff.Diff.Should().Contain("src/app.txt");
    }

    [Fact]
    public async Task Git_status_returns_empty_for_non_repo_directory()
    {
        Directory.CreateDirectory(_tempRoot);
        var client = factory.CreateClient();
        var wd = await CreateWorkingDirectory(client, _tempRoot);

        var status = await client.GetFromJsonAsync<GitFileStatusDto[]>(
            $"/api/git/status?workingDirectoryId={wd.Id}",
            JsonOptions);

        status.Should().BeEmpty();
    }

    [Fact]
    public async Task Git_history_and_file_content_return_expected_results()
    {
        SkipIfGitUnavailable();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src"));
        await RunGit("init");
        await RunGit("config", "user.name", "Test");
        await RunGit("config", "user.email", "test@example.com");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "src", "app.txt"), "version one");
        await RunGit("add", ".");
        await RunGit("-c", "commit.gpgsign=false", "commit", "-m", "first");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "src", "app.txt"), "version two");
        await RunGit("add", "src/app.txt");
        await RunGit("-c", "commit.gpgsign=false", "commit", "-m", "second");

        var client = factory.CreateClient();
        var wd = await CreateWorkingDirectory(client, _tempRoot);

        var history = await client.GetFromJsonAsync<GitFileHistoryDto>(
            $"/api/git/history?workingDirectoryId={wd.Id}&path={Uri.EscapeDataString("src/app.txt")}",
            JsonOptions);
        history!.IsRepository.Should().BeTrue();
        history.Commits.Should().HaveCountGreaterThanOrEqualTo(2);
        history.Commits[0].Message.Should().Be("second");
        history.Commits[0].Hash.Should().HaveLength(40);
        history.Commits[0].ShortHash.Should().NotBeNullOrWhiteSpace();
        history.Commits[0].ParentHash.Should().HaveLength(40);
        history.Commits[^1].ParentHash.Should().BeEmpty();
        DateTimeOffset.Parse(history.Commits[0].Date);

        var firstCommitHash = history.Commits[^1].Hash;
        var content = await client.GetFromJsonAsync<GitFileContentAtCommitDto>(
            $"/api/git/file-content?workingDirectoryId={wd.Id}&path={Uri.EscapeDataString("src/app.txt")}&hash={firstCommitHash}",
            JsonOptions);
        content!.Content.Should().Be("version one");
        content.Exists.Should().BeTrue();
        content.IsBinary.Should().BeFalse();
        content.Truncated.Should().BeFalse();

        var untrackedHistory = await client.GetFromJsonAsync<GitFileHistoryDto>(
            $"/api/git/history?workingDirectoryId={wd.Id}&path={Uri.EscapeDataString("missing.txt")}",
            JsonOptions);
        untrackedHistory!.IsRepository.Should().BeTrue();
        untrackedHistory.Commits.Should().BeEmpty();
    }

    [Fact]
    public async Task Git_history_returns_not_repository_for_plain_directory()
    {
        Directory.CreateDirectory(_tempRoot);
        var client = factory.CreateClient();
        var wd = await CreateWorkingDirectory(client, _tempRoot);

        var history = await client.GetFromJsonAsync<GitFileHistoryDto>(
            $"/api/git/history?workingDirectoryId={wd.Id}&path={Uri.EscapeDataString("src/app.txt")}",
            JsonOptions);

        history!.IsRepository.Should().BeFalse();
        history.Commits.Should().BeEmpty();
    }

    [Fact]
    public async Task Git_file_content_detects_binary_files()
    {
        SkipIfGitUnavailable();
        Directory.CreateDirectory(_tempRoot);
        await RunGit("init");
        await RunGit("config", "user.name", "Test");
        await RunGit("config", "user.email", "test@example.com");
        await File.WriteAllBytesAsync(Path.Combine(_tempRoot, "binary.bin"), [0x00, 0x01, 0x02]);
        await RunGit("add", ".");
        await RunGit("-c", "commit.gpgsign=false", "commit", "-m", "binary");

        var client = factory.CreateClient();
        var wd = await CreateWorkingDirectory(client, _tempRoot);
        var history = await client.GetFromJsonAsync<GitFileHistoryDto>(
            $"/api/git/history?workingDirectoryId={wd.Id}&path={Uri.EscapeDataString("binary.bin")}",
            JsonOptions);
        var hash = history!.Commits[0].Hash;

        var content = await client.GetFromJsonAsync<GitFileContentAtCommitDto>(
            $"/api/git/file-content?workingDirectoryId={wd.Id}&path={Uri.EscapeDataString("binary.bin")}&hash={hash}",
            JsonOptions);

        content!.Exists.Should().BeTrue();
        content.IsBinary.Should().BeTrue();
        content.Content.Should().BeEmpty();
    }

    public void Dispose()
    {
        if (!Directory.Exists(_tempRoot))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_tempRoot, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_tempRoot, recursive: true);
    }

    private static async Task<WorkingDirectoryDto> CreateWorkingDirectory(HttpClient client, string absolutePath)
    {
        var response = await client.PostAsJsonAsync(
            "/api/working-directories",
            new { name = $"repo-{Guid.NewGuid():N}", absolutePath, respectGitignore = true },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var workingDirectory = await response.Content.ReadFromJsonAsync<WorkingDirectoryDto>(JsonOptions);
        return workingDirectory!;
    }

    private async Task RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _tempRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        process.ExitCode.Should().Be(0, error);
    }

    private sealed record GitFileHistoryDto(bool IsRepository, IReadOnlyList<GitCommitDto> Commits);

    private sealed record GitCommitDto(
        string Hash,
        string ShortHash,
        string Author,
        string Date,
        string Message,
        string ParentHash);

    private sealed record GitFileContentAtCommitDto(
        string Content,
        bool Exists,
        bool IsBinary,
        bool Truncated);

    private static void SkipIfGitUnavailable()
    {
        if (!GitAvailable.Value)
        {
            throw Xunit.Sdk.SkipException.ForSkip("Git executable is not available on this machine.");
        }
    }

    private static readonly Lazy<bool> GitAvailable = new(() =>
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--version");
            using var process = Process.Start(startInfo);
            process?.WaitForExit(5_000);
            return process?.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    });
}
