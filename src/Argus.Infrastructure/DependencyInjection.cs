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

        services.AddSingleton(Options.Create(new OpenAiOptions
        {
            ApiKey = configuration[$"{OpenAiOptions.SectionName}:ApiKey"]
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? string.Empty,
            Model = configuration[$"{OpenAiOptions.SectionName}:Model"]
                ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
                ?? string.Empty,
            BaseUrl = configuration[$"{OpenAiOptions.SectionName}:BaseUrl"]
                ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
                ?? "https://api.openai.com/v1"
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
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IIncidentInvestigationService, IncidentInvestigationService>();
        services.AddScoped<ICoordinatorService, CoordinatorService>();
        services.AddScoped<IInvestigatorService, InvestigatorService>();
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
            client.BaseAddress = new Uri(configuration[$"{OpenAiOptions.SectionName}:BaseUrl"]
                ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
                ?? "https://api.openai.com/v1");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}