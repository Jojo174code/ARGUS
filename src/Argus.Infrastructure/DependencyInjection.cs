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

        var llmApiKey = FirstNonEmpty(
            configuration[$"{OpenAiOptions.SectionName}:ApiKey"],
            Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            Environment.GetEnvironmentVariable("LITELLM_API_KEY"));
        var llmModel = FirstNonEmpty(
            configuration[$"{OpenAiOptions.SectionName}:Model"],
            Environment.GetEnvironmentVariable("OPENAI_MODEL"),
            Environment.GetEnvironmentVariable("LITELLM_MODEL"));
        var llmBaseUrl = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPENAI_BASE_URL"),
            Environment.GetEnvironmentVariable("LITELLM_BASE_URL"),
            configuration[$"{OpenAiOptions.SectionName}:BaseUrl"],
            "https://api.openai.com/v1");
        var normalizedLlmBaseUrl = EnsureTrailingSlash(llmBaseUrl ?? "https://api.openai.com/v1");

        services.AddSingleton(Options.Create(new OpenAiOptions
        {
            ApiKey = llmApiKey ?? string.Empty,
            Model = llmModel ?? string.Empty,
            CoordinatorModel = Environment.GetEnvironmentVariable("ARGUS_COORDINATOR_MODEL") ?? string.Empty,
            InvestigatorModel = Environment.GetEnvironmentVariable("ARGUS_INVESTIGATOR_MODEL") ?? string.Empty,
            ResponseModel = Environment.GetEnvironmentVariable("ARGUS_RESPONSE_MODEL") ?? string.Empty,
            BaseUrl = normalizedLlmBaseUrl
        }));

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
        services.AddHttpClient<ILlmClient, OpenAiLlmClient>(client =>
        {
            client.BaseAddress = new Uri(normalizedLlmBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(90);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ARGUS/1.0");
        });

        return services;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith('/') ? value : $"{value}/";
    }
}