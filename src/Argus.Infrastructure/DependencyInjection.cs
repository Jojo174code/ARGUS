using Argus.Application.Interfaces;
using Argus.Application.Services;
using Argus.Application.Services.InvestigationTools;
using Argus.Infrastructure.Configuration;
using Argus.Infrastructure.AI;
using Argus.Infrastructure.Email;
using Argus.Infrastructure.Persistence;
using Argus.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Argus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseProvider = configuration[$"{DatabaseOptions.SectionName}:Provider"] ?? "Postgres";
        var connectionString = configuration.GetConnectionString("ArgusDatabase")
            ?? configuration["Database:ConnectionString"]
            ?? "Host=localhost;Port=5432;Database=argus;Username=argus;Password=argus";
        var inMemoryDatabaseName = configuration[$"{DatabaseOptions.SectionName}:DatabaseName"] ?? "argus";

        services.AddSingleton(Options.Create(new UploadOptions
        {
            RootDirectory = configuration[$"{UploadOptions.SectionName}:RootDirectory"] ?? "App_Data/uploads",
            MaxFileSizeBytes = long.TryParse(configuration[$"{UploadOptions.SectionName}:MaxFileSizeBytes"], out var maxFileSizeBytes)
                ? maxFileSizeBytes
                : 2 * 1024 * 1024
        }));

        services.AddSingleton(Options.Create(new PhishingAnalysisOptions
        {
            MaxHostnameLength = int.TryParse(configuration[$"{PhishingAnalysisOptions.SectionName}:MaxHostnameLength"], out var maxHostnameLength)
                ? maxHostnameLength
                : 40,
            MaxSubdomainCount = int.TryParse(configuration[$"{PhishingAnalysisOptions.SectionName}:MaxSubdomainCount"], out var maxSubdomainCount)
                ? maxSubdomainCount
                : 3,
            KnownBrands = configuration.GetSection($"{PhishingAnalysisOptions.SectionName}:KnownBrands").GetChildren()
                .Select(section => new KnownBrandOption
                {
                    Name = section["Name"] ?? string.Empty,
                    AllowedDomains = section.GetSection("AllowedDomains").GetChildren().Select(child => child.Value ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
                })
                .Where(brand => !string.IsNullOrWhiteSpace(brand.Name))
                .ToList()
        }));

        services.AddOptions<OpenRouterOptions>()
            .Configure(options =>
            {
                options.ApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")?.Trim() ?? configuration["OpenRouter:ApiKey"]?.Trim() ?? string.Empty;
                options.Model = Environment.GetEnvironmentVariable("OPENROUTER_MODEL")?.Trim() ?? configuration["OpenRouter:Model"]?.Trim() ?? options.Model;
                options.BaseUrl = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL")?.Trim() ?? configuration["OpenRouter:BaseUrl"]?.Trim() ?? options.BaseUrl;
                options.SiteUrl = Environment.GetEnvironmentVariable("OPENROUTER_SITE_URL")?.Trim() ?? configuration["OpenRouter:SiteUrl"]?.Trim();
                options.AppName = Environment.GetEnvironmentVariable("OPENROUTER_APP_NAME")?.Trim() ?? configuration["OpenRouter:AppName"]?.Trim() ?? options.AppName;
                options.TimeoutSeconds = int.TryParse(Environment.GetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS") ?? configuration["OpenRouter:TimeoutSeconds"], out var timeout) ? timeout : options.TimeoutSeconds;
                options.MaxRetries = int.TryParse(Environment.GetEnvironmentVariable("OPENROUTER_MAX_RETRIES") ?? configuration["OpenRouter:MaxRetries"], out var retries) ? retries : options.MaxRetries;
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<OpenRouterOptions>, OpenRouterOptionsValidator>();

        services.AddDbContext<ArgusDbContext>(options =>
        {
            if (databaseProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                options.UseInMemoryDatabase(inMemoryDatabaseName);
                return;
            }

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IIncidentAnalysisRepository, IncidentAnalysisRepository>();
        services.AddScoped<ICoordinatorRunRepository, CoordinatorRunRepository>();
        services.AddScoped<IInvestigatorRunRepository, InvestigatorRunRepository>();
        services.AddScoped<IResponseEducationRunRepository, ResponseEducationRunRepository>();
        services.AddScoped<IAgenticWorkflowRunRepository, AgenticWorkflowRunRepository>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IIncidentInvestigationService, IncidentInvestigationService>();
        services.AddScoped<ICoordinatorService, CoordinatorService>();
        services.AddScoped<IInvestigatorService, InvestigatorService>();
        services.AddScoped<IResponseEducationService, ResponseEducationService>();
        services.AddScoped<IEducationChatService, EducationChatService>();
        services.AddScoped<IAgenticWorkflowService, AgenticWorkflowService>();
        services.AddScoped<IEvidenceFileStore, LocalEvidenceFileStore>();
        services.AddScoped<IEmailParser, MimeKitEmailParser>();
        services.AddScoped<IMitreAttackMapper, StaticMitreAttackMapper>();
        services.AddScoped<IPhishingAnalyzer, DeterministicPhishingAnalyzer>();
        services.AddScoped<IInvestigationTool, EmailMetadataTool>();
        services.AddScoped<IInvestigationTool, EmailAuthenticationTool>();
        services.AddScoped<IInvestigationTool, UrlInspectionTool>();
        services.AddScoped<IInvestigationTool, MitreMappingTool>();
        services.AddHttpClient<OpenRouterLlmClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenRouterOptions>>().Value;
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ARGUS/1.0");
        });
        services.AddTransient<ILlmClient>(serviceProvider => serviceProvider.GetRequiredService<OpenRouterLlmClient>());

        return services;
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith('/') ? value : $"{value}/";
    }
}