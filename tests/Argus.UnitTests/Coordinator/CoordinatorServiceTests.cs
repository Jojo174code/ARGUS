using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Application.Services;
using Argus.Application.Validation;
using Argus.Domain.Entities;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Email;
using Argus.Infrastructure.Persistence;
using Argus.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Argus.UnitTests.Coordinator;

public sealed class CoordinatorServiceTests
{
    [Fact]
    public async Task GeneratePlanAsync_WithValidInput_PersistsPlanAndReturnsTasks()
    {
        var llmClient = new RecordingLlmClient(request => new LlmResponse(
            "test-model",
            JsonSerializer.Serialize(new InvestigationPlan(
                request.IncidentId,
                "Credential Phishing",
                "High",
                false,
                [
                    new InvestigationTask("task-2", "SignInReview", "Review account sign-in activity", "Check for suspicious or unfamiliar sign-ins after the phishing event.", 2, ["Relevant account sign-in logs"], "Investigator", "Recent sign-ins are reviewed"),
                    new InvestigationTask("task-1", "AccountExposureCheck", "Determine whether credentials were entered", "Confirm whether the user submitted credentials to the suspicious page.", 1, ["User confirmation"], "Investigator", "Credential submission status is known")
                ],
                [new MissingInformationItem("Did the user enter credentials on the page?", "This determines whether credential exposure should be assumed.", true)],
                ["The suspicious email was the only evidence submitted."],
                ["Do not perform account-changing actions without human approval."]), new JsonSerializerOptions(JsonSerializerDefaults.Web))));

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, llmClient);
        var incidentId = await SeedIncidentAsync(dbContext, includeAnalysis: true);

        var plan = await service.GeneratePlanAsync(incidentId, CancellationToken.None);

        Assert.Equal(incidentId, plan.IncidentId);
        Assert.Equal("Completed", plan.Status);
        Assert.NotNull(plan.Plan);
        Assert.Equal("Credential Phishing", plan.Plan!.IncidentType);
        Assert.Equal([1, 2], plan.Plan.Tasks.Select(task => task.Priority).ToArray());

        var storedRun = await dbContext.Set<CoordinatorRun>().SingleAsync(run => run.IncidentId == incidentId);
        Assert.Equal(CoordinatorRunStatus.Completed, storedRun.Status);
        Assert.False(string.IsNullOrWhiteSpace(storedRun.OutputJson));
    }

    [Fact]
    public async Task GeneratePlanAsync_WithoutAnalysis_ThrowsAndDoesNotCallLlm()
    {
        var llmClient = new RecordingLlmClient();
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, llmClient);
        var incidentId = await SeedIncidentAsync(dbContext, includeAnalysis: false);

        await Assert.ThrowsAsync<CoordinatorPrerequisiteException>(() => service.GeneratePlanAsync(incidentId, CancellationToken.None));

        Assert.Equal(0, llmClient.CallCount);
        Assert.Empty(await dbContext.Set<CoordinatorRun>().ToListAsync());
    }

    [Fact]
    public async Task GeneratePlanAsync_WithInvalidModelOutput_FailsSafely()
    {
        var llmClient = new RecordingLlmClient(_ => new LlmResponse("test-model", "not-json"));
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, llmClient);
        var incidentId = await SeedIncidentAsync(dbContext, includeAnalysis: true);

        var exception = await Assert.ThrowsAsync<CoordinatorPlanValidationException>(() => service.GeneratePlanAsync(incidentId, CancellationToken.None));

        Assert.NotEmpty(exception.ValidationErrors);
        Assert.Equal(2, llmClient.CallCount);

        var storedRun = await dbContext.Set<CoordinatorRun>().SingleAsync(run => run.IncidentId == incidentId);
        Assert.Equal(CoordinatorRunStatus.Failed, storedRun.Status);
        Assert.Null(storedRun.OutputJson);
    }

    [Fact]
    public async Task GeneratePlanAsync_WithMissingRequiredInformation_ReturnsPlanWithMissingItems()
    {
        var llmClient = new RecordingLlmClient(request => new LlmResponse(
            "test-model",
            JsonSerializer.Serialize(new InvestigationPlan(
                request.IncidentId,
                "Credential Phishing",
                "High",
                false,
                [new InvestigationTask("task-1", "AccountExposureCheck", "Determine whether credentials were entered", "Confirm whether the user submitted credentials to the suspicious page.", 1, ["User confirmation"], "Investigator", "Credential submission status is known")],
                [new MissingInformationItem("Did the user enter credentials on the page?", "This determines whether credential exposure should be assumed.", true)],
                ["Only the suspicious email was submitted."],
                ["Do not perform account-changing actions without human approval."]), new JsonSerializerOptions(JsonSerializerDefaults.Web))));

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, llmClient);
        var incidentId = await SeedIncidentAsync(dbContext, includeAnalysis: true);

        var plan = await service.GeneratePlanAsync(incidentId, CancellationToken.None);

        Assert.False(plan.Plan!.ReadyForInvestigation);
        Assert.Contains(plan.Plan.MissingInformation, item => item.Required);
        Assert.Equal("High", plan.Plan.Priority);
    }

    private static CoordinatorService CreateService(ArgusDbContext dbContext, ILlmClient llmClient)
    {
        return new CoordinatorService(
            new IncidentRepository(dbContext),
            new IncidentAnalysisRepository(dbContext),
            new CoordinatorRunRepository(dbContext),
            llmClient,
            NullLogger<CoordinatorService>.Instance);
    }

    private static ArgusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ArgusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ArgusDbContext(options);
    }

    private static async Task<Guid> SeedIncidentAsync(ArgusDbContext dbContext, bool includeAnalysis)
    {
        var incident = new Incident(
            "Grace Community Church",
            OrganizationType.Church,
            "Our treasurer entered credentials after clicking a Microsoft email.",
            "Avery",
            TechnicalSkillLevel.Beginner);

        await dbContext.Incidents.AddAsync(incident);
        await dbContext.SaveChangesAsync();

        if (includeAnalysis)
        {
            var analysis = new PhishingAnalysisResult(
                incident.Id,
                new ParsedEmail(
                    "Microsoft Security",
                    "notice@alerts-example.net",
                    "support@identity-help.example",
                    "<bounce@mailer-host.example>",
                    "Urgent: verify your login to avoid account suspension",
                    DateTimeOffset.UtcNow,
                    "<message-id@example>",
                    ["from mail.school.example by mx.recipient.example"],
                    new EmailAuthenticationResults(AuthCheckVerdict.Fail, AuthCheckVerdict.Missing, AuthCheckVerdict.Fail, ["spf=fail dkim=missing dmarc=fail"]),
                    [new ExtractedUrl("https://accounts-school.example.com/login", "portal.school.example", "html-anchor")],
                    Array.Empty<EmailAttachmentMetadata>(),
                    "Please verify your login.",
                    null),
                84,
                RiskLevel.High,
                "Multiple phishing indicators suggest elevated risk.",
                [new PhishingRuleResult("EMAIL-AUTH-001", "Authentication failure", "SPF, DKIM, or DMARC failed.", RuleSeverity.High, 40, "spf=fail dkim=missing dmarc=fail", true)],
                [new PhishingRuleResult("EMAIL-AUTH-001", "Authentication failure", "SPF, DKIM, or DMARC failed.", RuleSeverity.High, 40, "spf=fail dkim=missing dmarc=fail", true)],
                [new MitreAttackTechnique("T1566.002", "Spearphishing Link", "Malicious link delivery used to solicit credentials.")],
                DateTimeOffset.UtcNow);

            var analysisRepository = new IncidentAnalysisRepository(dbContext);
            await analysisRepository.UpsertAsync(analysis, CancellationToken.None);
            await analysisRepository.SaveChangesAsync(CancellationToken.None);
        }

        return incident.Id;
    }

    private sealed class RecordingLlmClient : ILlmClient
    {
        private readonly Func<LlmRequest, LlmResponse>? _responseFactory;

        public RecordingLlmClient(Func<LlmRequest, LlmResponse>? responseFactory = null)
        {
            _responseFactory = responseFactory;
        }

        public int CallCount { get; private set; }

        public Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responseFactory?.Invoke(request) ?? new LlmResponse("test-model", "not-json"));
        }
    }
}