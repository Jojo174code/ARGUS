using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Domain.Models;
using System.Text.Json;

namespace Argus.IntegrationTests;

public sealed class TestCoordinatorLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Func<LlmRequest, LlmResponse> _responseFactory;

    public TestCoordinatorLlmClient()
        : this(BuildDefaultResponse)
    {
    }

    public TestCoordinatorLlmClient(Func<LlmRequest, LlmResponse> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responseFactory(request));
    }

    private static LlmResponse BuildDefaultResponse(LlmRequest request)
    {
        if (string.Equals(request.ResponseSchemaName, "InvestigationReportSynthesis", StringComparison.Ordinal))
        {
            var synthesis = new InvestigationReportSynthesis(
                request.IncidentId,
                "Credential Phishing",
                "High",
                0.86,
                new List<InvestigationFinding>
                {
                    new(
                        "finding-1",
                        "Email authentication failures observed",
                        "SPF and DMARC failures indicate sender authenticity issues consistent with phishing attempts.",
                        "High",
                        0.9,
                        "EmailAuthenticationTool",
                        "auth:spf",
                        "SPF result observed as failing in deterministic evidence.",
                        "AuthenticationAnomaly",
                        "task-1"),
                    new(
                        "finding-2",
                        "Suspicious link patterns detected",
                        "Deterministic URL indicators include mismatched or suspicious link characteristics.",
                        "High",
                        0.82,
                        "UrlInspectionTool",
                        "auth:dkim",
                        "Displayed destination did not match resolved destination domain.",
                        "UrlAnomaly",
                        "task-2")
                },
                new List<AttackTechniqueFinding>
                {
                    new("T1566.002", "Spearphishing Link", "Deterministic URL-focused phishing indicators were triggered.")
                },
                ["Potential credential exposure if recipient interacted with phishing destination."],
                ["Credential entry status remains unknown without user confirmation."]);

            return new LlmResponse("fake-investigator-model", JsonSerializer.Serialize(synthesis, SerializerOptions));
        }

        var plan = new InvestigationPlan(
            request.IncidentId,
            "Credential Phishing",
            "High",
            false,
            new List<InvestigationTask>
            {
                new("task-1", "ReviewEmailAuthentication", "Review sender authentication results", "Confirm SPF, DKIM, and DMARC outcomes for the suspicious message.", 1, ["Authentication header values"], "Investigator", "Authentication verdicts are captured"),
                new("task-2", "InspectSuspiciousLinks", "Inspect suspicious links", "Review extracted URLs for mismatch, punycode, and suspicious host patterns.", 2, ["Extracted URL list"], "Investigator", "Link risk observations are captured"),
                new("task-3", "ReviewEmailMetadata", "Review message metadata", "Review sender metadata, message ID, and attachment metadata.", 3, ["Parsed email metadata"], "Investigator", "Metadata observations are captured"),
                new("task-4", "ReviewMicrosoft365SignIns", "Review Microsoft 365 sign-ins", "Review sign-in activity around the event window.", 4, ["Sign-in logs"], "Investigator", "Sign-in status is determined")
            },
            new List<MissingInformationItem>
            {
                new("Did the user enter credentials on the page?", "This determines whether credential exposure should be assumed.", true),
                new("Was MFA enabled on the account?", "MFA status affects the likelihood of credential misuse.", true)
            },
            ["Only the suspicious email and deterministic analysis were submitted."] ,
            ["Do not perform account-changing actions without human approval."]);

        return new LlmResponse("fake-coordinator-model", JsonSerializer.Serialize(plan, SerializerOptions));
    }
}