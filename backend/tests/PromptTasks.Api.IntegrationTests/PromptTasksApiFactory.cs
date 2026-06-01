using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PromptTasks.Api.IntegrationTests;

public sealed class PromptTasksApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("prompttasks_test")
        .WithUsername("prompttasks")
        .WithPassword("prompttasks")
        .Build();

    public MutableDateTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                ["AgentUsage:Enabled"] = "false"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDateTimeProvider>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
            services.AddSingleton<IDateTimeProvider>(Clock);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public sealed class MutableDateTimeProvider(DateTimeOffset utcNow) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Set(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }
    }
}
