using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PromptTasks.Application.Common.Behaviors;
using PromptTasks.Application.Features.PromptTemplates;
using PromptTasks.Application.Features.PromptTemplates.Definitions;

namespace PromptTasks.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton<IPromptTemplateDefinition, ReviewPlanTemplate>();
        services.AddSingleton<IPromptTemplateDefinition, ImplementPlanTemplate>();
        services.AddSingleton<IPromptTemplateDefinition, ReviewPlanWithParentPromptTemplate>();
        services.AddSingleton<IPromptTemplateDefinition, ReReviewPlanTemplate>();
        services.AddSingleton<IPromptTemplateDefinition, ImplementPlanInWorktreeTemplate>();
        services.AddSingleton<IPromptTemplateDefinition, ReviewPullRequestTemplate>();
        services.AddSingleton<IPromptTemplateDefinition, MergePullRequestTemplate>();
        services.AddSingleton<IPromptTemplateDefinition, RebaseCurrentBranchTemplate>();
        services.AddSingleton<IPromptTemplateCatalog, PromptTemplateCatalog>();

        return services;
    }
}
