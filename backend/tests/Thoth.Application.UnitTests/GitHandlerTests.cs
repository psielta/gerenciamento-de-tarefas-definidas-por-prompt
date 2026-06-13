using FluentAssertions;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Thoth.Application.Common.Exceptions;
using Thoth.Application.Common.Interfaces;
using Thoth.Application.Common.Models;
using Thoth.Application.Features.Git.Queries.GetFileGitContent;
using Thoth.Application.Features.Git.Queries.GetFileGitHistory;
using Thoth.Application.Features.Git.Queries.GetGitDiff;
using Thoth.Application.Features.Git.Queries.GetGitStatus;
using Thoth.Application.Features.Git.Queries.GetOriginalFileContent;
using Thoth.Domain.Prompts;
using Thoth.Domain.Users;
using Thoth.Domain.WorkingDirectories;

namespace Thoth.Application.UnitTests;

public sealed class GitHandlerTests
{
    [Fact]
    public async Task GetGitStatus_maps_porcelain_and_strips_workspace_prefix()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context, "C:/repo/sub");
        var git = new FakeGitCommandRunner(
            new GitCommandResult(0, "sub/\n", string.Empty),
            new GitCommandResult(0, " M sub/src/app.ts\0?? sub/new file.txt\0R  sub/new-name.ts\0sub/old-name.ts\0", string.Empty));
        var handler = new GetGitStatusHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetGitStatusHandler>.Instance);

        var result = await handler.Handle(new GetGitStatusQuery(directory.Id), CancellationToken.None);

        result.Should().BeEquivalentTo(
        [
            new GitFileStatusDto("src/app.ts", GitFileChangeStatus.Modified),
            new GitFileStatusDto("new file.txt", GitFileChangeStatus.Untracked),
            new GitFileStatusDto("new-name.ts", GitFileChangeStatus.Renamed, "old-name.ts")
        ]);
        git.Calls.Should().HaveCount(2);
        git.Calls[0].WorkingDirectory.Should().Be("C:/repo/sub");
        git.Calls[0].Arguments.Should().Equal("rev-parse", "--show-prefix");
        git.Calls[1].Arguments.Should().Equal(
            "-c",
            "core.quotepath=false",
            "--no-optional-locks",
            "status",
            "--porcelain=v1",
            "-z",
            "--untracked-files=all",
            "--",
            ".");
    }

    [Fact]
    public async Task GetGitStatus_returns_empty_for_non_repo_without_running_status()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(new GitCommandResult(128, string.Empty, "not a git repository"));
        var handler = new GetGitStatusHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetGitStatusHandler>.Instance);

        var result = await handler.Handle(new GetGitStatusQuery(directory.Id), CancellationToken.None);

        result.Should().BeEmpty();
        git.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task GetGitStatus_rejects_unknown_or_foreign_working_directory()
    {
        var context = new FakeApplicationDbContext();
        SeedWorkingDirectory(context, ownerId: Guid.CreateVersion7());
        var handler = new GetGitStatusHandler(
            context,
            new FakeGitCommandRunner(),
            new FakeCurrentUser(),
            NullLogger<GetGitStatusHandler>.Instance);

        var act = () => handler.Handle(new GetGitStatusQuery(Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetOriginalFileContent_returns_content_and_normalizes_backslashes()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(new GitCommandResult(0, "const total = 1;", string.Empty));
        var handler = new GetOriginalFileContentHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetOriginalFileContentHandler>.Instance);

        var result = await handler.Handle(
            new GetOriginalFileContentQuery(directory.Id, "src\\app.ts"),
            CancellationToken.None);

        result.Content.Should().Be("const total = 1;");
        git.Calls.Should().ContainSingle().Which.Arguments.Should().Equal("show", "HEAD:./src/app.ts");
    }

    [Fact]
    public async Task GetOriginalFileContent_returns_empty_when_git_cannot_show_file()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(new GitCommandResult(128, string.Empty, "fatal"));
        var handler = new GetOriginalFileContentHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetOriginalFileContentHandler>.Instance);

        var result = await handler.Handle(new GetOriginalFileContentQuery(directory.Id, "new.ts"), CancellationToken.None);

        result.Content.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOriginalFileContent_rejects_parent_traversal()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var handler = new GetOriginalFileContentHandler(
            context,
            new FakeGitCommandRunner(),
            new FakeCurrentUser(),
            NullLogger<GetOriginalFileContentHandler>.Instance);

        var act = () => handler.Handle(new GetOriginalFileContentQuery(directory.Id, "../secret.txt"), CancellationToken.None);

        await act.Should().ThrowAsync<PathTraversalException>();
    }

    [Theory]
    [InlineData(0, "diff --git", "diff --git")]
    [InlineData(1, "diff --git", "diff --git")]
    [InlineData(128, "", "")]
    public async Task GetGitDiff_handles_expected_exit_codes(int exitCode, string output, string expected)
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(new GitCommandResult(exitCode, output, "fatal"));
        var handler = new GetGitDiffHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetGitDiffHandler>.Instance);

        var result = await handler.Handle(new GetGitDiffQuery(directory.Id, "src/app.ts"), CancellationToken.None);

        result.Diff.Should().Be(expected);
        git.Calls.Should().ContainSingle().Which.Arguments.Should().Equal(
            "-c",
            "core.quotepath=false",
            "diff",
            "HEAD",
            "--",
            "src/app.ts");
    }

    [Fact]
    public void Validators_require_working_directory_and_path()
    {
        ValidationResult statusResult = new GetGitStatusValidator().Validate(new GetGitStatusQuery(Guid.Empty));
        ValidationResult originalResult = new GetOriginalFileContentValidator().Validate(new GetOriginalFileContentQuery(Guid.Empty, ""));
        ValidationResult diffResult = new GetGitDiffValidator().Validate(new GetGitDiffQuery(Guid.Empty, ""));
        ValidationResult historyResult = new GetFileGitHistoryValidator().Validate(new GetFileGitHistoryQuery(Guid.Empty, ""));

        statusResult.IsValid.Should().BeFalse();
        originalResult.IsValid.Should().BeFalse();
        diffResult.IsValid.Should().BeFalse();
        historyResult.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetFileGitHistory_returns_not_repository_without_running_log()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(new GitCommandResult(128, string.Empty, "not a git repository"));
        var handler = new GetFileGitHistoryHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetFileGitHistoryHandler>.Instance);

        var result = await handler.Handle(new GetFileGitHistoryQuery(directory.Id, "src/app.ts"), CancellationToken.None);

        result.IsRepository.Should().BeFalse();
        result.Commits.Should().BeEmpty();
        git.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task GetFileGitHistory_returns_empty_commits_when_log_succeeds_with_no_output()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(
            new GitCommandResult(0, string.Empty, string.Empty),
            new GitCommandResult(0, string.Empty, string.Empty));
        var handler = new GetFileGitHistoryHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetFileGitHistoryHandler>.Instance);

        var result = await handler.Handle(new GetFileGitHistoryQuery(directory.Id, "src/app.ts"), CancellationToken.None);

        result.IsRepository.Should().BeTrue();
        result.Commits.Should().BeEmpty();
        git.Calls.Should().HaveCount(2);
        git.Calls[1].Arguments.Should().Equal(
            "-c",
            "core.quotepath=false",
            "log",
            "--no-color",
            "-z",
            "--format=%H%x1f%h%x1f%an%x1f%aI%x1f%P%x1f%s",
            "-n",
            "100",
            "--",
            "src/app.ts");
    }

    [Fact]
    public async Task GetFileGitHistory_returns_empty_commits_when_log_exit_code_is_128()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(
            new GitCommandResult(0, string.Empty, string.Empty),
            new GitCommandResult(128, string.Empty, "fatal"));
        var handler = new GetFileGitHistoryHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetFileGitHistoryHandler>.Instance);

        var result = await handler.Handle(new GetFileGitHistoryQuery(directory.Id, "untracked.ts"), CancellationToken.None);

        result.IsRepository.Should().BeTrue();
        result.Commits.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFileGitHistory_parses_commits_on_happy_path()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var logOutput =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\x1f" +
            "aaaaaaa\x1f" +
            "Author\x1f" +
            "2026-01-01T00:00:00+00:00\x1f" +
            "\x1f" +
            "Initial\x0";
        var git = new FakeGitCommandRunner(
            new GitCommandResult(0, string.Empty, string.Empty),
            new GitCommandResult(0, logOutput, string.Empty));
        var handler = new GetFileGitHistoryHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetFileGitHistoryHandler>.Instance);

        var result = await handler.Handle(new GetFileGitHistoryQuery(directory.Id, "src/app.ts"), CancellationToken.None);

        result.IsRepository.Should().BeTrue();
        result.Commits.Should().ContainSingle();
        result.Commits[0].Hash.Should().Be("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        result.Commits[0].Message.Should().Be("Initial");
    }

    [Fact]
    public async Task GetFileGitHistory_rejects_unknown_working_directory()
    {
        var context = new FakeApplicationDbContext();
        SeedWorkingDirectory(context, ownerId: Guid.CreateVersion7());
        var handler = new GetFileGitHistoryHandler(
            context,
            new FakeGitCommandRunner(),
            new FakeCurrentUser(),
            NullLogger<GetFileGitHistoryHandler>.Instance);

        var act = () => handler.Handle(new GetFileGitHistoryQuery(Guid.CreateVersion7(), "src/app.ts"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetFileGitContent_returns_content_when_show_succeeds()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(
            new GitCommandResult(0, "12", string.Empty),
            new GitCommandResult(0, "const total = 1;", string.Empty));
        var handler = new GetFileGitContentHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetFileGitContentHandler>.Instance);

        var result = await handler.Handle(
            new GetFileGitContentQuery(directory.Id, "src/app.ts", "abcdef0123456"),
            CancellationToken.None);

        result.Should().BeEquivalentTo(new GitFileContentAtCommitDto("const total = 1;", true, false, false));
        git.Calls[0].Arguments.Should().Equal("cat-file", "-s", "abcdef0123456:./src/app.ts");
        git.Calls[1].Arguments.Should().Equal("show", "abcdef0123456:./src/app.ts");
    }

    [Fact]
    public async Task GetFileGitContent_returns_not_exists_when_cat_file_fails()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(new GitCommandResult(128, string.Empty, "fatal"));
        var handler = new GetFileGitContentHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetFileGitContentHandler>.Instance);

        var result = await handler.Handle(
            new GetFileGitContentQuery(directory.Id, "src/app.ts", "abcdef0123456"),
            CancellationToken.None);

        result.Should().BeEquivalentTo(new GitFileContentAtCommitDto(string.Empty, false, false, false));
        git.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task GetFileGitContent_detects_binary_content()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context);
        var git = new FakeGitCommandRunner(
            new GitCommandResult(0, "4", string.Empty),
            new GitCommandResult(0, "a\0b", string.Empty));
        var handler = new GetFileGitContentHandler(
            context,
            git,
            new FakeCurrentUser(),
            NullLogger<GetFileGitContentHandler>.Instance);

        var result = await handler.Handle(
            new GetFileGitContentQuery(directory.Id, "bin.dat", "abcdef0123456"),
            CancellationToken.None);

        result.Should().BeEquivalentTo(new GitFileContentAtCommitDto(string.Empty, true, true, false));
    }

    private static WorkingDirectory SeedWorkingDirectory(
        FakeApplicationDbContext context,
        string absolutePath = "C:/repo",
        Guid? ownerId = null)
    {
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = absolutePath,
            OwnerId = ownerId ?? User.SystemUserId
        };

        context.WorkingDirectoryItems.Add(directory);
        return directory;
    }

    private sealed class FakeGitCommandRunner : IGitCommandRunner
    {
        private readonly Queue<GitCommandResult> _results;

        public FakeGitCommandRunner(params GitCommandResult[] results)
        {
            _results = new Queue<GitCommandResult>(results);
        }

        public List<(string WorkingDirectory, IReadOnlyList<string> Arguments)> Calls { get; } = new();

        public Task<GitCommandResult> RunAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            Calls.Add((workingDirectory, arguments.ToArray()));
            return Task.FromResult(_results.Count == 0
                ? new GitCommandResult(0, string.Empty, string.Empty)
                : _results.Dequeue());
        }
    }

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public List<WorkingDirectory> WorkingDirectoryItems { get; } = new();

        public IQueryable<User> Users => Enumerable.Empty<User>().AsQueryable();
        public IQueryable<WorkingDirectory> WorkingDirectories => WorkingDirectoryItems.AsQueryable();
        public IQueryable<Thoth.Domain.FutureTasks.FutureTask> FutureTasks => Enumerable.Empty<Thoth.Domain.FutureTasks.FutureTask>().AsQueryable();
        public IQueryable<Thoth.Domain.FutureTasks.FutureTaskLabel> FutureTaskLabels => Enumerable.Empty<Thoth.Domain.FutureTasks.FutureTaskLabel>().AsQueryable();
        public IQueryable<Prompt> Prompts => Enumerable.Empty<Prompt>().AsQueryable();
        public IQueryable<PromptVersion> PromptVersions => Enumerable.Empty<PromptVersion>().AsQueryable();
        public IQueryable<PromptFileReference> PromptFileReferences => Enumerable.Empty<PromptFileReference>().AsQueryable();
        public IQueryable<LinkedDocument> LinkedDocuments => Enumerable.Empty<LinkedDocument>().AsQueryable();
        public IQueryable<LinkedDocumentVersion> LinkedDocumentVersions => Enumerable.Empty<LinkedDocumentVersion>().AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.WorkflowTemplate> WorkflowTemplates => Enumerable.Empty<Thoth.Domain.Workflows.WorkflowTemplate>().AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.WorkflowTemplatePhase> WorkflowTemplatePhases => Enumerable.Empty<Thoth.Domain.Workflows.WorkflowTemplatePhase>().AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.PromptWorkflow> PromptWorkflows => Enumerable.Empty<Thoth.Domain.Workflows.PromptWorkflow>().AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.PromptWorkflowPhase> PromptWorkflowPhases => Enumerable.Empty<Thoth.Domain.Workflows.PromptWorkflowPhase>().AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.PromptWorkflowEvent> PromptWorkflowEvents => Enumerable.Empty<Thoth.Domain.Workflows.PromptWorkflowEvent>().AsQueryable();
        public IQueryable<Thoth.Domain.Ai.AiChatSession> AiChatSessions => Enumerable.Empty<Thoth.Domain.Ai.AiChatSession>().AsQueryable();
        public IQueryable<Thoth.Domain.Ai.AiChatMessage> AiChatMessages => Enumerable.Empty<Thoth.Domain.Ai.AiChatMessage>().AsQueryable();
        public IQueryable<Thoth.Domain.Ai.AiUserSettings> AiUserSettings => Enumerable.Empty<Thoth.Domain.Ai.AiUserSettings>().AsQueryable();
        public IQueryable<Thoth.Domain.Notebooks.Notebook> Notebooks => Enumerable.Empty<Thoth.Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<Thoth.Domain.Notebooks.Note> Notes => Enumerable.Empty<Thoth.Domain.Notebooks.Note>().AsQueryable();
        public IQueryable<Thoth.Domain.Diagrams.Diagram> Diagrams => Enumerable.Empty<Thoth.Domain.Diagrams.Diagram>().AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
        }

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
        }

        public void Remove<TEntity>(TEntity entity) where TEntity : class
        {
        }

        public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => User.SystemUserId;
    }
}
