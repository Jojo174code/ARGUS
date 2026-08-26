using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Application.Services;
using Argus.Application.Validation;
using Argus.Domain.Entities;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Persistence;
using Argus.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Argus.UnitTests.ResponseEducation;

public sealed class ResponseEducationServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GenerateAsync_WithValidInvestigatorReport_PersistsPackage()
    {
        var llmClient = new RecordingLlmClient(request => new LlmResponse(
            "test-model",
            JsonSerializer.Serialize(CreateValidSynthesis(request.IncidentId), SerializerOptions)));

        await using var dbContext = CreateDbContext();
        var incidentId = await SeedIncidentGraphAsync(dbContext, includeInvestigatorRun: true);
        var service = CreateService(dbContext, llmClient);

        var package = await service.GenerateAsync(incidentId, CancellationToken.None);

        Assert.Equal("Completed", package.Status);
        Assert.NotNull(package.Package);
        Assert.NotEmpty(package.Package!.ImmediateActions);
        Assert.NotEmpty(package.Package.RecoveryActions);
        Assert.NotEmpty(package.Package.PreventionActions);
        Assert.NotNull(package.Package.Education);
        Assert.All(package.Package.ImmediateActions.SelectMany(action => action.SupportingFindingIds), id => Assert.Contains(id, new[] { "finding-1", "finding-2" }));

        var storedRun = await dbContext.Set<ResponseEducationRun>().SingleAsync(run => run.IncidentId == incidentId);
        Assert.Equal(ResponseEducationRunStatus.Completed, storedRun.Status);
        Assert.False(string.IsNullOrWhiteSpace(storedRun.OutputJson));
    }

    [Fact]
    public async Task GenerateAsync_WithoutInvestigatorReport_ThrowsAndDoesNotCallLlm()
    {
        var llmClient = new RecordingLlmClient(request => new LlmResponse(
            "test-model",
            JsonSerializer.Serialize(CreateValidSynthesis(request.IncidentId), SerializerOptions)));

        await using var dbContext = CreateDbContext();
        var incidentId = await SeedIncidentGraphAsync(dbContext, includeInvestigatorRun: false);
        var service = CreateService(dbContext, llmClient);

        await Assert.ThrowsAsync<ResponseEducationPrerequisiteException>(() => service.GenerateAsync(incidentId, CancellationToken.None));

        Assert.Equal(0, llmClient.CallCount);
        Assert.Empty(await dbContext.Set<ResponseEducationRun>().ToListAsync());
    }

    [Fact]
    public async Task GenerateAsync_WithHallucinatedFindingReference_ThrowsValidationAndMarksFailedRun()
    {
        var llmClient = new RecordingLlmClient(request =>
        {
            var synthesis = CreateValidSynthesis(request.IncidentId) with
            {
                ImmediateActions =
                [
                    new ResponseAction(
                        "action-1",
                        "Preserve evidence",
                        "Keep the email available for review.",
                        "The finding should be preserved.",
                        1,
                        "Informational",
                        false,
                        "Reporter",
                        ["finding-that-does-not-exist"])
                ]
            };

            return new LlmResponse("test-model", JsonSerializer.Serialize(synthesis, SerializerOptions));
        });

        await using var dbContext = CreateDbContext();
        var incidentId = await SeedIncidentGraphAsync(dbContext, includeInvestigatorRun: true);
        var service = CreateService(dbContext, llmClient);

        var exception = await Assert.ThrowsAsync<ResponseEducationValidationException>(() => service.GenerateAsync(incidentId, CancellationToken.None));

        Assert.Equal(2, llmClient.CallCount);
        Assert.Contains(exception.ValidationErrors, error => error.Contains("unknown finding", StringComparison.OrdinalIgnoreCase));

        var storedRun = await dbContext.Set<ResponseEducationRun>().SingleAsync(run => run.IncidentId == incidentId);
        Assert.Equal(ResponseEducationRunStatus.Failed, storedRun.Status);
        Assert.Null(storedRun.OutputJson);
    }

    [Fact]
    public async Task GenerateAsync_WithUnsafeAutonomousClaim_ThrowsValidationAndMarksFailedRun()
    {
        var llmClient = new RecordingLlmClient(request =>
        {
            var synthesis = CreateValidSynthesis(request.IncidentId) with
            {
                ImmediateActions =
                [
                    new ResponseAction(
                        "action-1",
                        "ARGUS reset the user's password",
                        "ARGUS reset the user's password and closed the incident.",
                        "Credential phishing was observed.",
                        1,
                        "UserAction",
                        false,
                        "Administrator",
                        ["finding-2"])
                ]
            };

            return new LlmResponse("test-model", JsonSerializer.Serialize(synthesis, SerializerOptions));
        });

        await using var dbContext = CreateDbContext();
        var incidentId = await SeedIncidentGraphAsync(dbContext, includeInvestigatorRun: true);
        var service = CreateService(dbContext, llmClient);

        var exception = await Assert.ThrowsAsync<ResponseEducationValidationException>(() => service.GenerateAsync(incidentId, CancellationToken.None));

        Assert.Contains(exception.ValidationErrors, error => error.Contains("human approval", StringComparison.OrdinalIgnoreCase)
            || error.Contains("prohibited execution claim", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateAsync_WithInvalidEducationQuestions_ThrowsValidationAndMarksFailedRun()
    {
        var llmClient = new RecordingLlmClient(request =>
        {
            var invalidQuestions = new List<EducationQuestion>();
            for (var index = 0; index < 6; index++)
            {
                invalidQuestions.Add(new EducationQuestion(
                    $"question-{index + 1}",
                    $"Question {index + 1}",
                    ["Only one option"],
                    3,
                    "Explanation"));
            }

            var synthesis = CreateValidSynthesis(request.IncidentId) with
            {
                Education = CreateValidSynthesis(request.IncidentId).Education with
                {
                    Questions = invalidQuestions
                }
            };

            return new LlmResponse("test-model", JsonSerializer.Serialize(synthesis, SerializerOptions));
        });

        await using var dbContext = CreateDbContext();
        var incidentId = await SeedIncidentGraphAsync(dbContext, includeInvestigatorRun: true);
        var service = CreateService(dbContext, llmClient);

        var exception = await Assert.ThrowsAsync<ResponseEducationValidationException>(() => service.GenerateAsync(incidentId, CancellationToken.None));

        Assert.Contains(exception.ValidationErrors, error => error.Contains("between 3 and 5 questions", StringComparison.OrdinalIgnoreCase)
            || error.Contains("3 to 4 options", StringComparison.OrdinalIgnoreCase)
            || error.Contains("correct option index", StringComparison.OrdinalIgnoreCase));
    }

    private static ResponseEducationService CreateService(ArgusDbContext dbContext, ILlmClient llmClient)
    {
        return new ResponseEducationService(
            new IncidentRepository(dbContext),
            new IncidentAnalysisRepository(dbContext),
            new CoordinatorRunRepository(dbContext),
            new InvestigatorRunRepository(dbContext),
            new ResponseEducationRunRepository(dbContext),
            llmClient,
            NullLogger<ResponseEducationService>.Instance);
    }

    private static ArgusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ArgusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ArgusDbContext(options);
    }

    private static async Task<Guid> SeedIncidentGraphAsync(ArgusDbContext dbContext, bool includeInvestigatorRun)
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
            [new PhishingRuleResult("URL-DISPLAY-001", "Display mismatch", "The displayed link destination did not match the target domain.", RuleSeverity.High, 30, "display mismatch", true)],
            [new MitreAttackTechnique("T1566.002", "Spearphishing Link", "Malicious link delivery used to solicit credentials.")],
            DateTimeOffset.UtcNow);

        var analysisRepository = new IncidentAnalysisRepository(dbContext);
        await analysisRepository.UpsertAsync(analysis, CancellationToken.None);
        await analysisRepository.SaveChangesAsync(CancellationToken.None);

        var plan = new InvestigationPlan(
            incident.Id,
            "Credential Phishing",
            "High",
            true,
            [
                new InvestigationTask("task-1", "ReviewEmailAuthentication", "Review sender authentication", "Confirm SPF, DKIM, and DMARC outcomes.", 1, ["Authentication headers"], "Investigator", "Authentication verdicts are captured"),
                new InvestigationTask("task-2", "InspectSuspiciousLinks", "Inspect suspicious links", "Review extracted URLs for suspicious characteristics.", 2, ["Extracted URL list"], "Investigator", "Link observations are captured")
            ],
            [],
            ["Only deterministic evidence is currently available."],
            ["Do not perform account changes."]);

        var coordinatorRun = new CoordinatorRun(
            incident.Id,
            "coordinator-test-model",
            "coordinator-v1",
            "{}",
            DateTimeOffset.UtcNow.AddMinutes(-2));

        coordinatorRun.MarkCompleted(JsonSerializer.Serialize(plan, SerializerOptions), DateTimeOffset.UtcNow.AddMinutes(-1));
        await dbContext.CoordinatorRuns.AddAsync(coordinatorRun);

        if (includeInvestigatorRun)
        {
            var report = new InvestigationReport(
                incident.Id,
                "Credential Phishing",
                "High",
                0.86,
                [
                    new InvestigationFinding("finding-1", "Authentication failures observed", "SPF and DMARC failed in deterministic analysis.", "High", 0.92, "EmailAuthenticationTool", "auth:spf", "SPF failed in parsed authentication headers.", "AuthenticationAnomaly", "task-1"),
                    new InvestigationFinding("finding-2", "Suspicious link behavior observed", "Suspicious link characteristics were validated.", "High", 0.84, "UrlInspectionTool", "url:https://accounts-school.example.com/login", "Link analysis showed suspicious destination behavior.", "UrlAnomaly", "task-2")
                ],
                [new AttackTechniqueFinding("T1566.002", "Spearphishing Link", "Validated phishing-linked indicators were observed.")],
                ["Potential credential exposure if the user interacted with the phishing destination."],
                ["Credential-entry status remains unknown without user confirmation."],
                [
                    new TaskExecutionResult("task-1", "ReviewEmailAuthentication", "Completed", ["EmailAuthenticationTool"], ["auth:spf"], "Task completed using deterministic tools."),
                    new TaskExecutionResult("task-2", "InspectSuspiciousLinks", "Completed", ["UrlInspectionTool"], ["url:https://accounts-school.example.com/login"], "Task completed using deterministic tools.")
                ]);

            var investigatorRun = new InvestigatorRun(
                incident.Id,
                coordinatorRun.Id,
                "investigator-test-model",
                "investigator-v1",
                "{}",
                DateTimeOffset.UtcNow.AddMinutes(-1));

            investigatorRun.MarkCompleted(JsonSerializer.Serialize(report, SerializerOptions), DateTimeOffset.UtcNow);
            await dbContext.InvestigatorRuns.AddAsync(investigatorRun);
        }

        await dbContext.SaveChangesAsync();
        return incident.Id;
    }

    private static ResponseEducationSynthesis CreateValidSynthesis(Guid incidentId)
    {
        return new ResponseEducationSynthesis(
            incidentId,
            "Credential Phishing",
            "High",
            "ARGUS found evidence consistent with a credential-phishing attempt. The validated findings point to failed authentication checks and suspicious link behavior.",
            [
                new ResponseAction("action-1", "Preserve the suspicious email", "Keep the email and related notes for review.", "Preserving evidence helps internal review and follow-up.", 1, "Informational", false, "Reporter", ["finding-1"])
            ],
            [
                new ResponseAction("action-2", "Review recent sign-ins", "Review recent account sign-ins for unfamiliar access.", "The validated findings indicate phishing risk and potential credential exposure.", 1, "AdministratorAction", true, "Administrator", ["finding-2"])
            ],
            [
                new ResponseAction("action-3", "Enable multi-factor authentication", "Enable MFA for the relevant account if it is not already enabled.", "MFA reduces the impact of stolen credentials.", 1, "AdministratorAction", true, "Administrator", ["finding-2"])
            ],
            [
                new EscalationRecommendation("InternalIT", "Internal review is appropriate based on the phishing findings.", "Internal IT or designated security contact", false)
            ],
            new EducationModule(
                "Spotting Credential Phishing",
                "Beginner",
                8,
                "Recognize the warning signs that appeared in this incident.",
                "This lesson uses the actual warning signs from the investigated email.",
                [
                    new WarningSign("Failed authentication checks", "The investigation found failed authentication checks tied to the suspicious message.", ["finding-1"]),
                    new WarningSign("Suspicious link behavior", "The investigation found suspicious link behavior tied to the phishing destination.", ["finding-2"])
                ],
                [
                    new EducationQuestion("question-1", "Which warning sign suggested the sender might not be legitimate?", ["Failed authentication checks", "A weekday delivery time", "A short subject line"], 0, "Failed authentication checks were a validated indicator in this incident."),
                    new EducationQuestion("question-2", "What should you verify before signing in from an email prompt?", ["Page colors", "The trusted domain", "Button size"], 1, "The destination domain is more meaningful than visual styling."),
                    new EducationQuestion("question-3", "Why should staff review this incident?", ["It shows real warning signs from their own workflow", "It guarantees future safety", "It replaces technical investigation"], 0, "Incident-specific examples help people recognize similar attacks later.")
                ],
                ["Check trust signals before entering credentials.", "Use known-good login routes when in doubt."]),
            ["Potential credential exposure depends on actual user interaction."],
            ["This package does not confirm compromise beyond the validated investigation findings."]);
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