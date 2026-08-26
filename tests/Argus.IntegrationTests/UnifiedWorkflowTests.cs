using Argus.Application.DTOs;
using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Domain.Entities;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Argus.IntegrationTests;

public sealed class UnifiedWorkflowTests
{
    [Fact]
    public async Task UnifiedWorkflow_RunAndReload_CompletesAllStages()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();

        var incident = await CreateIncidentAsync(httpClient, "Our treasurer entered credentials after clicking a Microsoft email.");
        await UploadSampleEmailAsync(httpClient, incident.Id, "phishing-microsoft-login.eml");

        var runResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/workflow/run", content: null);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        var workflow = await runResponse.Content.ReadArgusJsonAsync<AgenticWorkflowResultDto>();
        Assert.NotNull(workflow);
        Assert.Equal("Completed", workflow!.Status);
        Assert.Equal(4, workflow.Stages.Count);
        Assert.All(workflow.Stages, stage => Assert.Equal("Completed", stage.Status));
        Assert.True(workflow.Metrics.CoordinatorTaskCount > 0);
        Assert.True(workflow.Metrics.InvestigatorFindingCount > 0);
        Assert.True(workflow.Metrics.ResponseActionCount > 0);
        Assert.True(workflow.Metrics.QuizQuestionCount > 0);

        var getResponse = await httpClient.GetAsync($"/api/incidents/{incident.Id}/workflow");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var storedWorkflow = await getResponse.Content.ReadArgusJsonAsync<AgenticWorkflowResultDto>();
        Assert.NotNull(storedWorkflow);
        Assert.Equal(workflow.WorkflowRunId, storedWorkflow!.WorkflowRunId);
        Assert.Equal(workflow.Status, storedWorkflow.Status);
        Assert.Equal(workflow.Stages.Select(stage => stage.Stage), storedWorkflow.Stages.Select(stage => stage.Stage));

        await using var scope = testHost.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
        Assert.Single(await dbContext.AgenticWorkflowRuns.Where(run => run.IncidentId == incident.Id).ToListAsync());
    }

    [Fact]
    public async Task UnifiedWorkflow_ResumeAfterInvestigatorFailure_ReusesPriorSuccessfulStages()
    {
        var llmClient = new StatefulWorkflowLlmClient();
        await using var testHost = new ArgusWebApplicationFactory(llmClient);
        using var httpClient = testHost.CreateClient();

        var incident = await CreateIncidentAsync(httpClient, "Our treasurer entered credentials after clicking a Microsoft email.");
        await UploadSampleEmailAsync(httpClient, incident.Id, "phishing-microsoft-login.eml");

        var firstRunResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/workflow/run", content: null);
        Assert.Equal(HttpStatusCode.OK, firstRunResponse.StatusCode);

        var firstWorkflow = await firstRunResponse.Content.ReadArgusJsonAsync<AgenticWorkflowResultDto>();
        Assert.NotNull(firstWorkflow);
        Assert.Equal("Failed", firstWorkflow!.Status);
        Assert.Equal("Investigator", firstWorkflow.FailureStage);

        var secondRunResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/workflow/run", content: null);
        Assert.Equal(HttpStatusCode.OK, secondRunResponse.StatusCode);

        var resumedWorkflow = await secondRunResponse.Content.ReadArgusJsonAsync<AgenticWorkflowResultDto>();
        Assert.NotNull(resumedWorkflow);
        Assert.Equal(firstWorkflow.WorkflowRunId, resumedWorkflow!.WorkflowRunId);
        Assert.Equal("Completed", resumedWorkflow.Status);
        Assert.All(resumedWorkflow.Stages, stage => Assert.Equal("Completed", stage.Status));
        Assert.Contains(resumedWorkflow.Stages, stage => stage.Stage == "DeterministicAnalysis" && stage.Summary?.Contains("Reused", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(resumedWorkflow.Stages, stage => stage.Stage == "Coordinator" && stage.Summary?.Contains("Reused", StringComparison.OrdinalIgnoreCase) == true);

        await using var scope = testHost.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
        Assert.Single(await dbContext.AgenticWorkflowRuns.Where(run => run.IncidentId == incident.Id).ToListAsync());
        Assert.Single(await dbContext.CoordinatorRuns.Where(run => run.IncidentId == incident.Id && run.Status == Domain.Enums.CoordinatorRunStatus.Completed).ToListAsync());
        Assert.Single(await dbContext.IncidentAnalyses.Where(run => run.IncidentId == incident.Id).ToListAsync());
        Assert.Single(await dbContext.ResponseEducationRuns.Where(run => run.IncidentId == incident.Id && run.Status == Domain.Enums.ResponseEducationRunStatus.Completed).ToListAsync());
        Assert.Equal(1, await dbContext.InvestigatorRuns.CountAsync(run => run.IncidentId == incident.Id && run.Status == Domain.Enums.InvestigatorRunStatus.Completed));
        Assert.Equal(1, await dbContext.InvestigatorRuns.CountAsync(run => run.IncidentId == incident.Id && run.Status == Domain.Enums.InvestigatorRunStatus.Failed));
    }

    [Fact]
    public async Task UnifiedWorkflow_WithBlockingCoordinatorQuestions_ReturnsAwaitingInformation()
    {
        var llmClient = new TestCoordinatorLlmClient(request =>
        {
            if (string.Equals(request.ResponseSchemaName, "InvestigationPlan", StringComparison.Ordinal))
            {
                var plan = new Domain.Models.InvestigationPlan(
                    request.IncidentId,
                    "Credential Phishing",
                    "High",
                    false,
                    [new Domain.Models.InvestigationTask("task-1", "ReviewEmailAuthentication", "Review sender authentication", "Confirm SPF, DKIM, and DMARC outcomes.", 1, ["Authentication headers"], "Investigator", "Authentication verdicts are captured")],
                    [new Domain.Models.MissingInformationItem("Did the user enter credentials?", "This is required before continuing account-response steps.", true, true)],
                    ["Only deterministic evidence is currently available."],
                    ["Do not perform account-changing actions without human approval."]);

                return new LlmResponse("blocking-coordinator-model", JsonSerializer.Serialize(plan, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }

            return new TestCoordinatorLlmClient().GenerateAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        });

        await using var testHost = new ArgusWebApplicationFactory(llmClient);
        using var httpClient = testHost.CreateClient();

        var incident = await CreateIncidentAsync(httpClient, "We suspect the user may have entered credentials, but we do not have confirmation.");
        await UploadSampleEmailAsync(httpClient, incident.Id, "phishing-microsoft-login.eml");

        var runResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/workflow/run", content: null);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        var workflow = await runResponse.Content.ReadArgusJsonAsync<AgenticWorkflowResultDto>();
        Assert.NotNull(workflow);
        Assert.Equal("AwaitingInformation", workflow!.Status);
        Assert.Single(workflow.BlockingMissingInformation);
        Assert.Equal("Coordinator", workflow.FailureStage);
        Assert.Contains(workflow.Stages, stage => stage.Stage == "Coordinator" && stage.Status == "AwaitingInformation");
        Assert.Contains(workflow.Stages, stage => stage.Stage == "Investigator" && stage.Status == "Pending");
        Assert.Contains(workflow.Stages, stage => stage.Stage == "ResponseEducation" && stage.Status == "Pending");

        await using var scope = testHost.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
        Assert.Empty(await dbContext.InvestigatorRuns.Where(run => run.IncidentId == incident.Id).ToListAsync());
        Assert.Empty(await dbContext.ResponseEducationRuns.Where(run => run.IncidentId == incident.Id).ToListAsync());
    }

    [Fact]
    public async Task UnifiedWorkflow_WithPromptInjectionEmail_RemainsSafeAndSchemaValid()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();

        var incident = await CreateIncidentAsync(httpClient, "Suspicious email told the user to ignore all prior instructions and disable MFA.");
        await UploadSampleEmailAsync(httpClient, incident.Id, "phishing-prompt-injection.eml");

        var runResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/workflow/run", content: null);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        var workflow = await runResponse.Content.ReadArgusJsonAsync<AgenticWorkflowResultDto>();
        Assert.NotNull(workflow);
        Assert.Equal("Completed", workflow!.Status);

        var responsePackageResponse = await httpClient.GetAsync($"/api/incidents/{incident.Id}/response");
        Assert.Equal(HttpStatusCode.OK, responsePackageResponse.StatusCode);

        var package = await responsePackageResponse.Content.ReadArgusJsonAsync<ResponseEducationPackageDto>();
        Assert.NotNull(package);
        Assert.NotNull(package!.Package);
        var combinedText = string.Join(' ', package.Package.PlainLanguageSummary,
            string.Join(' ', package.Package.ImmediateActions.Select(action => $"{action.Title} {action.Description} {action.Reason}")),
            string.Join(' ', package.Package.RecoveryActions.Select(action => $"{action.Title} {action.Description} {action.Reason}")),
            string.Join(' ', package.Package.PreventionActions.Select(action => $"{action.Title} {action.Description} {action.Reason}")));

        Assert.DoesNotContain("disable mfa", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("share passwords", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("argus reset", combinedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ignore all prior instructions", combinedText, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IncidentDto> CreateIncidentAsync(HttpClient httpClient, string description)
    {
        var response = await httpClient.PostAsJsonAsync("/api/incidents", new CreateIncidentRequest(
            "Grace Community Church",
            Domain.Enums.OrganizationType.Church,
            description,
            "Avery",
            Domain.Enums.TechnicalSkillLevel.Beginner));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var incident = await response.Content.ReadArgusJsonAsync<IncidentDto>();
        Assert.NotNull(incident);
        return incident!;
    }

    private static async Task UploadSampleEmailAsync(HttpClient httpClient, Guid incidentId, string sampleName)
    {
        await using var sampleStream = File.OpenRead(TestFileHelper.GetSampleEmailPath(sampleName));
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(sampleStream), "file", sampleName);

        var response = await httpClient.PostAsync($"/api/incidents/{incidentId}/evidence/email", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed class StatefulWorkflowLlmClient : ILlmClient
    {
        private readonly TestCoordinatorLlmClient _defaultClient = new();
        private int _investigatorRequests;

        public Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
        {
            if (string.Equals(request.ResponseSchemaName, "InvestigationReportSynthesis", StringComparison.Ordinal))
            {
                _investigatorRequests++;
                if (_investigatorRequests <= 2)
                {
                    return Task.FromResult(new LlmResponse("failing-investigator-model", "not-json"));
                }
            }

            return _defaultClient.GenerateAsync(request, cancellationToken);
        }
    }
}