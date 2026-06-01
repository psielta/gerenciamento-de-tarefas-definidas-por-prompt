namespace PromptTasks.Application.Common.Interfaces;

public interface IDailyTaskSequenceProvider
{
    Task<int> NextAsync(Guid workingDirectoryId, DateOnly dateUtc, CancellationToken cancellationToken);
}
