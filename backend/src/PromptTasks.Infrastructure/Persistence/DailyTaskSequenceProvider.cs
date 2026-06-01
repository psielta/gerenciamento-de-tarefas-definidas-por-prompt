using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PromptTasks.Application.Common.Interfaces;

namespace PromptTasks.Infrastructure.Persistence;

public sealed class DailyTaskSequenceProvider(
    ApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IDailyTaskSequenceProvider
{
    public async Task<int> NextAsync(Guid workingDirectoryId, DateOnly dateUtc, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO daily_task_sequences ("Id","WorkingDirectoryId","SequenceDate","CurrentValue","CreatedAtUtc","UpdatedAtUtc")
                VALUES (@id, @workingDirectoryId, @sequenceDate, 1, @now, @now)
                ON CONFLICT ("WorkingDirectoryId","SequenceDate")
                DO UPDATE SET "CurrentValue" = daily_task_sequences."CurrentValue" + 1, "UpdatedAtUtc" = @now
                RETURNING "CurrentValue";
                """;
            command.Parameters.Add(new NpgsqlParameter<Guid>("id", Guid.CreateVersion7()));
            command.Parameters.Add(new NpgsqlParameter<Guid>("workingDirectoryId", workingDirectoryId));
            command.Parameters.Add(new NpgsqlParameter<DateOnly>("sequenceDate", dateUtc));
            command.Parameters.Add(new NpgsqlParameter<DateTimeOffset>("now", now));

            if (context.Database.CurrentTransaction is { } transaction)
            {
                command.Transaction = transaction.GetDbTransaction();
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
