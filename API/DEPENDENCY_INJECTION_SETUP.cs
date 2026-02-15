// =============================================================================
// DEPENDENCY INJECTION SETUP FOR AI & MediatR
// Add this code to your Program.cs after existing service registrations
// =============================================================================

// -------------------------------------
// MediatR Configuration
// -------------------------------------
using MediatR;
using WhatShouldIDo.Application.UseCases.Queries;
using WhatShouldIDo.Infrastructure.Services.AI;

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(SearchPlacesQuery).Assembly);
});

Log.Information("MediatR registered with application handlers");

// -------------------------------------
// AI Configuration & Services
// -------------------------------------
builder.Services.Configure<AIOptions>(builder.Configuration.GetSection("AI"));

// Register AI providers
builder.Services.AddHttpClient<OpenAIProvider>(client =>
{
    // HttpClient configuration for OpenAI
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<NoOpAIProvider>();
builder.Services.AddSingleton<AIProviderFactory>();

// Register AI service with provider factory
builder.Services.AddScoped<IAIService>(provider =>
{
    var factory = provider.GetRequiredService<AIProviderFactory>();
    var cacheService = provider.GetService<ICacheService>();
    var logger = provider.GetRequiredService<ILogger<AIService>>();
    var options = provider.GetRequiredService<IOptions<AIOptions>>();

    var primaryProvider = factory.CreatePrimaryProvider();
    var fallbackProvider = factory.CreateFallbackProvider();

    return new AIService(primaryProvider, options, logger, cacheService, fallbackProvider);
});

var aiProviderName = builder.Configuration["AI:Provider"] ?? "OpenAI";
var aiEnabled = builder.Configuration.GetValue<bool>("AI:Enabled", true);

Log.Information("AI service configured: Enabled={Enabled}, Provider={Provider}",
    aiEnabled, aiProviderName);

// -------------------------------------
// Enhanced Prompt Interpreter (uses AI)
// -------------------------------------
// Replace the existing BasicPromptInterpreter registration with:
builder.Services.AddScoped<IPromptInterpreter>(provider =>
{
    var aiService = provider.GetRequiredService<IAIService>();
    var logger = provider.GetRequiredService<ILogger<AIPromptInterpreter>>();
    return new AIPromptInterpreter(aiService, logger);
});

// Note: Create AIPromptInterpreter class that wraps IAIService.InterpretPromptAsync
// Or keep BasicPromptInterpreter as fallback when AI is disabled

// =============================================================================
// VALIDATION - Add FluentValidation for MediatR pipeline behaviors
// =============================================================================
builder.Services.AddValidatorsFromAssembly(typeof(SearchPlacesQuery).Assembly);

// Add validation pipeline behavior
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// =============================================================================
// Create ValidationBehavior class (Application/Behaviors/ValidationBehavior.cs)
// =============================================================================
/*
using FluentValidation;
using MediatR;

namespace WhatShouldIDo.Application.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!_validators.Any())
            {
                return await next();
            }

            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }

            return await next();
        }
    }
}
*/
