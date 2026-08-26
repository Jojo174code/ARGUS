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
        if (string.Equals(request.ResponseSchemaName, "ResponseEducationPackage", StringComparison.Ordinal))
        {
            var package = new ResponseEducationSynthesis(
                request.IncidentId,
                "Credential Phishing",
                "High",
                "ARGUS found evidence consistent with a credential-phishing attempt targeting account credentials. The strongest indicators were failed email authentication checks and suspicious link behavior identified in the validated investigation findings.",
                [
                    new ResponseAction(
                        "action-1",
                        "Preserve the suspicious email and related notes",
                        "Keep the suspicious message, any screenshots, and the incident notes available for review.",
                        "The investigation identified phishing indicators that may need to be referenced during internal review.",
                        1,
                        "Informational",
                        false,
                        "Incident Reporter",
                        ["finding-1", "finding-2"]),
                    new ResponseAction(
                        "action-2",
                        "Change potentially exposed credentials from a trusted device",
                        "If the user may have entered credentials, change the relevant password using a trusted device and trusted login page.",
                        "The investigation found evidence consistent with credential phishing and possible credential exposure.",
                        2,
                        "UserAction",
                        true,
                        "Impacted User",
                        ["finding-2"])
                ],
                [
                    new ResponseAction(
                        "action-3",
                        "Review recent account sign-ins",
                        "Review recent sign-in history for unfamiliar access after the phishing event.",
                        "Account review helps determine whether suspicious access followed the phishing attempt.",
                        1,
                        "AdministratorAction",
                        true,
                        "Administrator",
                        ["finding-2"])
                ],
                [
                    new ResponseAction(
                        "action-4",
                        "Enable multi-factor authentication for high-value accounts",
                        "Require MFA for sensitive accounts involved in finance, leadership, or administration.",
                        "MFA reduces the impact of stolen credentials from phishing attempts.",
                        1,
                        "AdministratorAction",
                        true,
                        "Administrator",
                        ["finding-2"]),
                    new ResponseAction(
                        "action-5",
                        "Run a short phishing-awareness lesson using this incident",
                        "Review the warning signs from this message with staff so they recognize similar phishing attempts.",
                        "The investigation produced specific warning signs that can be used for targeted awareness training.",
                        2,
                        "Informational",
                        false,
                        "Manager",
                        ["finding-1", "finding-2"])
                ],
                [
                    new EscalationRecommendation(
                        "InternalIT",
                        "Internal review is appropriate because the findings indicate phishing risk and possible credential exposure, but no confirmed post-compromise activity was established.",
                        "Internal IT administrator or designated security contact",
                        false)
                ],
                new EducationModule(
                    "Recognizing Credential Phishing in Email",
                    "Beginner",
                    10,
                    "Learn how to spot the warning signs that appeared in this phishing message before interacting with it.",
                    "This incident showed that phishing emails often combine sender trust cues with links that do not behave the way the branding suggests.",
                    [
                        new WarningSign(
                            "Failed authentication checks",
                            "The investigation found failed authentication results, which is a warning sign that the sender may not be authorized to send on behalf of the claimed brand.",
                            ["finding-1"]),
                        new WarningSign(
                            "Suspicious link behavior",
                            "The investigation found suspicious link characteristics, which means the visible message cues did not fully match the destination risk.",
                            ["finding-2"])
                    ],
                    [
                        new EducationQuestion(
                            "question-1",
                            "Which warning sign in this incident most directly suggested the sender might not be legitimate?",
                            ["Failed authentication checks", "A short message", "A weekday delivery time", "A missing attachment"],
                            0,
                            "Failed authentication checks were one of the strongest validated warning signs in this incident."),
                        new EducationQuestion(
                            "question-2",
                            "What should a user verify before signing in from an email link?",
                            ["The page color", "The registered domain or trusted login destination", "The size of the button", "The font used in the message"],
                            1,
                            "Attackers can copy logos and page styles, but the destination domain is a stronger indicator to verify."),
                        new EducationQuestion(
                            "question-3",
                            "Why is this incident useful for staff awareness training?",
                            ["It includes real warning signs staff can learn from", "It proves every suspicious email is harmless", "It replaces technical review", "It automatically blocks future phishing"],
                            0,
                            "Incident-specific examples are useful because they show the actual warning signs that appeared in the organization’s own workflow.")
                    ],
                    [
                        "Check sender trust signals and link destinations before entering credentials.",
                        "Use a trusted login route instead of signing in through unexpected email prompts."
                    ]),
                ["Potential credential exposure depends on whether the user interacted with the phishing destination."],
                ["The package is based on currently available evidence and does not confirm compromise beyond the investigator findings."]);

            return new LlmResponse("fake-response-model", JsonSerializer.Serialize(package, SerializerOptions));
        }

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