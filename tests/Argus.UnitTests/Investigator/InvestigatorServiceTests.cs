using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Application.Services;
using Argus.Application.Services.InvestigationTools;
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

namespace Argus.UnitTests.Investigator;

public sealed class InvestigatorServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RunAsync_WithSupportedAndUnsupportedTasks_ReturnsReportAndPersistsTaskStatuses()
    {
        var llmClient = new RecordingLlmClient(request => new LlmResponse(
            "test-model",
            JsonSerializer.Serialize(new InvestigationReportSynthesis(
                request.IncidentId,
                "Credential Phishing",
                "High",
                0.87,
                [
                    new InvestigationFinding(
                        "finding-1",
                        "Authentication failures observed",
                        "SPF and DMARC failed in deterministic analysis.",
                        "High",
                        0.92,
                        "EmailAuthenticationTool",
                        "auth:spf",
                        "SPF failed in parsed authentication headers.",
                        "AuthenticationAnomaly",
                        "task-1")
                ],
                [new AttackTechniqueFinding("T1566.002", "Spearphishing Link", "Phishing-linked indicators were observed.")],
                ["Potential credential exposure if user followed the lure."],
                ["User click and credential-entry status are unknown."]),
            SerializerOptions)));

        await using var dbContext = CreateDbContext();
        var incidentId = await SeedIncidentAnalysisAndCoordinatorPlanAsync(dbContext, includeUnsupportedTask: true);
        var service = CreateService(dbContext, llmClient);

        var report = await service.RunAsync(incidentId, CancellationToken.None);

        Assert.Equal("Completed", report.Status);
        Assert.NotNull(report.Report);
        Assert.Equal("Credential Phishing", report.Report!.Classification);

        var taskResults = report.Report.TaskResults;
        Assert.Contains(taskResults, result => result.TaskType == "ReviewEmailAuthentication" && string.Equals(result.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(taskResults, result => result.TaskType == "ReviewMicrosoft365SignIns" && string.Equals(result.Status, "Unsupported", StringComparison.OrdinalIgnoreCase));

        var storedRun = await dbContext.Set<InvestigatorRun>().SingleAsync(run => run.IncidentId == incidentId);
        Assert.Equal(InvestigatorRunStatus.Completed, storedRun.Status);
        Assert.False(string.IsNullOrWhiteSpace(storedRun.OutputJson));
    }

    [Fact]
    public async Task RunAsync_WithInvalidJsonOutput_ThrowsValidationAndMarksFailedRun()
    {
        var llmClient = new RecordingLlmClient(_ => new LlmResponse("test-model", "not-json"));

        await using var dbContext = CreateDbContext();
        var incidentId = await SeedIncidentAnalysisAndCoordinatorPlanAsync(dbContext, includeUnsupportedTask: false);
        var service = CreateService(dbContext, llmClient);

        var exception = await Assert.ThrowsAsync<InvestigatorValidationException>(() => service.RunAsync(incidentId, CancellationToken.None));

        Assert.NotEmpty(exception.ValidationErrors);
        Assert.Equal(2, llmClient.CallCount);

        var storedRun = await dbContext.Set<InvestigatorRun>().SingleAsync(run => run.IncidentId == incidentId);
        Assert.Equal(InvestigatorRunStatus.Failed, storedRun.Status);
        Assert.Null(storedRun.OutputJson);
    }

    [Fact]
    public async Task RunAsync_WithUnknownEvidenceReference_ThrowsValidationAndMarksFailedRun()
    {
        var llmClient = new RecordingLlmClient(request => new LlmResponse(
            "test-model",
            JsonSerializer.Serialize(new InvestigationReportSynthesis(
                request.IncidentId,
                "Credential Phishing",
                "High",
                0.81,
                [
                    new InvestigationFinding(
                        "finding-1",
                        "Unverified claim",
                        "This finding references evidence that was never produced by tools.",
                        "High",
                        0.8,
                        "EmailAuthenticationTool",
                        "unknown:evidence-reference",
                        "No deterministic basis.",
                        "AuthenticationAnomaly",
                        "task-1")
                ],
                [new AttackTechniqueFinding("T1566.002", "Spearphishing Link", "Phishing-linked indicators were observed.")],
                ["Potential credential exposure if user followed the lure."],
                ["User click and credential-entry status are unknown."]),
            SerializerOptions)));

        await using var dbContext = CreateDbContext();
        var incidentId = await SeedIncidentAnalysisAndCoordinatorPlanAsync(dbContext, includeUnsupportedTask: false);
        var service = CreateService(dbContext, llmClient);

        var exception = await Assert.ThrowsAsync<InvestigatorValidationException>(() => service.RunAsync(incidentId, CancellationToken.None));

        Assert.Contains(exception.ValidationErrors, message => message.Contains("unknown evidence", StringComparison.OrdinalIgnoreCase));

        var storedRun = await dbContext.Set<InvestigatorRun>().SingleAsync(run => run.IncidentId == incidentId);
        Assert.Equal(InvestigatorRunStatus.Failed, storedRun.Status);
        Assert.Null(storedRun.OutputJson);
    }

    private static InvestigatorService CreateService(ArgusDbContext dbContext, ILlmClient llmClient)
    {
        return new InvestigatorService(
            new IncidentRepository(dbContext),
            new IncidentAnalysisRepository(dbContext),
            new CoordinatorRunRepository(dbContext),
            new InvestigatorRunRepository(dbContext),
            [
                new EmailAuthenticationTool(),
                new UrlInspectionTool(),
                new EmailMetadataTool(),
                new MitreMappingTool()
            ],
            llmClient,
            NullLogger<InvestigatorService>.Instance);
    }

    private static ArgusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ArgusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ArgusDbContext(options);
    }

    private static async Task<Guid> SeedIncidentAnalysisAndCoordinatorPlanAsync(ArgusDbContext dbContext, bool includeUnsupportedTask)
    {
        var incident = new Incident(
            "Grace Community Church",
            OrganizationType.Church,
            "Our treasurer entered credentials after clicking a Microsoft email.",
            "Avery",
            TechnicalSkillLevel.Beginner);

        await dbContext.Incidents.AddAsync(incident);
        await dbContext.SaveChangesAsync();

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
                [new ExtractedUrl("https://accounts-school.example.com/login", "https://portal.school.example/login", "html-anchor")],
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

        var tasks = new List<InvestigationTask>
        {
            new("task-1", "ReviewEmailAuthentication", "Review sender authentication", "Confirm SPF, DKIM, and DMARC outcomes.", 1, ["Authentication headers"], "Investigator", "Authentication verdicts are captured")
        };

        if (includeUnsupportedTask)
        {
            tasks.Add(new InvestigationTask(
                "task-2",
                "ReviewMicrosoft365SignIns",
                "Review Microsoft 365 sign-ins",
                "Review account sign-in activity around the incident window.",
                2,
                ["Sign-in logs"],
                "Investigator",
                "Sign-in activity is reviewed"));
        }

        var plan = new InvestigationPlan(
            incident.Id,
            "Credential Phishing",
            "High",
            true,
            tasks,
            [],
            ["Only deterministic evidence is currently available."],
            ["Do not perform account changes."]);

        var coordinatorRun = new CoordinatorRun(
            incident.Id,
            "coordinator-test-model",
            "coordinator-v1",
            "{}",
            DateTimeOffset.UtcNow.AddMinutes(-1));

        coordinatorRun.MarkCompleted(JsonSerializer.Serialize(plan, SerializerOptions), DateTimeOffset.UtcNow);

        await dbContext.Set<CoordinatorRun>().AddAsync(coordinatorRun);
        await dbContext.SaveChangesAsync();

        return incident.Id;
    }

    private sealed class RecordingLlmClient : ILlmClient
    {
        private readonly Func<LlmRequest, LlmResponse> _responseFactory;

        public RecordingLlmClient(Func<LlmRequest, LlmResponse> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int CallCount { get; private set; }

        public Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
