using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Infrastructure.Ai;
using PromptTasks.Infrastructure.AgentUsage;
using PromptTasks.Infrastructure.FileSystem;
using PromptTasks.Infrastructure.Persistence;
using PromptTasks.Infrastructure.Services;

namespace PromptTasks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment? environment = null)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=prompttasks;Username=prompttasks;Password=prompttasks";

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IDailyTaskSequenceProvider, DailyTaskSequenceProvider>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddMemoryCache();
        services.AddScoped<IWorkspaceFileService, WorkspaceFileService>();
        ConfigureAgentUsage(services, configuration);
        services.Configure<LinkedDocumentOptions>(options =>
        {
            var section = configuration.GetSection("LinkedDocuments");
            if (long.TryParse(section["MaxFileSizeBytes"], out var maxFileSizeBytes))
            {
                options.MaxFileSizeBytes = maxFileSizeBytes;
            }

            if (int.TryParse(section["DebounceMilliseconds"], out var debounceMilliseconds))
            {
                options.DebounceMilliseconds = debounceMilliseconds;
            }

            if (int.TryParse(section["ReconcileSeconds"], out var reconcileSeconds))
            {
                options.ReconcileSeconds = reconcileSeconds;
            }

            if (bool.TryParse(section["AllowUncPaths"], out var allowUncPaths))
            {
                options.AllowUncPaths = allowUncPaths;
            }
        });
        services.AddScoped<ILinkedDocumentFileService, LinkedDocumentFileService>();
        services.AddScoped<ILinkedDocumentSyncService, LinkedDocumentSyncService>();
        services.AddSingleton<LinkedDocumentWatcherService>();
        services.AddSingleton<ILinkedDocumentWatchCoordinator>(provider =>
            provider.GetRequiredService<LinkedDocumentWatcherService>());
        services.AddSingleton<IHostedService>(provider =>
            provider.GetRequiredService<LinkedDocumentWatcherService>());

        ConfigureGemini(services, configuration, environment);

        return services;
    }

    private static void ConfigureGemini(IServiceCollection services, IConfiguration configuration, IHostEnvironment? environment)
    {
        // Load .env file in development
        if (environment?.IsDevelopment() ?? false)
        {
            var envFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".env");
            if (!File.Exists(envFile))
            {
                // Try repo root relative to ContentRootPath
                envFile = Path.Combine(environment.ContentRootPath, "..", "..", "..", "..", ".env");
            }

            if (File.Exists(envFile))
            {
                DotNetEnv.Env.Load(envFile);
            }
        }

        services.Configure<GeminiOptions>(opts =>
        {
            var section = configuration.GetSection("Gemini");
            section.Bind(opts);

            // Allow env var override for ApiKey
            var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
                opts.ApiKey = envKey;
        });

        services.AddSingleton<IGeminiModelCatalog, GeminiModelCatalog>();

        services.AddHttpClient<IGeminiClient, GeminiApiClient>((provider, client) =>
        {
            var geminiOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
            var baseUrl = geminiOptions.BaseUrl;
            if (!baseUrl.EndsWith('/'))
                baseUrl += "/";
            client.BaseAddress = new Uri(baseUrl);
            var httpTimeoutSeconds = Math.Max(
                Math.Max(geminiOptions.StreamTimeoutSeconds, geminiOptions.RequestTimeoutSeconds),
                60);
            client.Timeout = TimeSpan.FromSeconds(httpTimeoutSeconds);
        });
    }

    private static void ConfigureAgentUsage(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentUsageOptions>(options =>
        {
            var section = configuration.GetSection("AgentUsage");
            if (bool.TryParse(section["Enabled"], out var enabled))
            {
                options.Enabled = enabled;
            }

            if (int.TryParse(section["ReconcileSeconds"], out var reconcileSeconds))
            {
                options.ReconcileSeconds = reconcileSeconds;
            }

            if (int.TryParse(section["DebounceMilliseconds"], out var debounceMilliseconds))
            {
                options.DebounceMilliseconds = debounceMilliseconds;
            }

            var claude = section.GetSection("Claude");
            options.Claude.CredentialsPath = ReadString(claude["CredentialsPath"], options.Claude.CredentialsPath);
            options.Claude.ProjectsDir = ReadString(claude["ProjectsDir"], options.Claude.ProjectsDir);
            options.Claude.UsageEndpoint = ReadString(claude["UsageEndpoint"], options.Claude.UsageEndpoint)!;
            options.Claude.AnthropicBetaHeader = ReadString(claude["AnthropicBetaHeader"], options.Claude.AnthropicBetaHeader)!;
            options.Claude.UserAgent = ReadString(claude["UserAgent"], options.Claude.UserAgent)!;
            if (int.TryParse(claude["RequestTimeoutSeconds"], out var requestTimeoutSeconds))
            {
                options.Claude.RequestTimeoutSeconds = requestTimeoutSeconds;
            }

            if (int.TryParse(claude["CacheTtlSeconds"], out var claudeCacheTtlSeconds))
            {
                options.Claude.CacheTtlSeconds = claudeCacheTtlSeconds;
            }

            if (bool.TryParse(claude["EnableTranscriptFallback"], out var enableTranscriptFallback))
            {
                options.Claude.EnableTranscriptFallback = enableTranscriptFallback;
            }

            var tierBudgets = claude.GetSection("TierBudgets");
            foreach (var tier in tierBudgets.GetChildren())
            {
                if (long.TryParse(tier["FiveHourTokens"], out var fiveHourTokens) &&
                    long.TryParse(tier["WeeklyTokens"], out var weeklyTokens))
                {
                    options.Claude.TierBudgets[tier.Key] = new AgentUsageTierBudget
                    {
                        FiveHourTokens = fiveHourTokens,
                        WeeklyTokens = weeklyTokens
                    };
                }
            }

            var codex = section.GetSection("Codex");
            options.Codex.SessionsDir = ReadString(codex["SessionsDir"], options.Codex.SessionsDir);
            if (int.TryParse(codex["MaxFilesToScan"], out var maxFilesToScan))
            {
                options.Codex.MaxFilesToScan = maxFilesToScan;
            }

            if (int.TryParse(codex["CacheTtlSeconds"], out var codexCacheTtlSeconds))
            {
                options.Codex.CacheTtlSeconds = codexCacheTtlSeconds;
            }
        });

        services.AddHttpClient<IClaudeUsageReader, ClaudeUsageReader>((provider, client) =>
        {
            var agentUsageOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentUsageOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(agentUsageOptions.Claude.RequestTimeoutSeconds, 1));
        });
        services.AddScoped<ICodexUsageReader, CodexUsageReader>();
        services.AddScoped<IAgentUsageReader, AgentUsageReader>();
        services.AddSingleton<AgentUsageRefreshService>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<AgentUsageRefreshService>());
    }

    private static string? ReadString(string? configured, string? fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured;
}
