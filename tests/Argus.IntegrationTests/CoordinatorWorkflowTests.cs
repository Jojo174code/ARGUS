using Argus.Application.DTOs;
using Argus.Domain.Enums;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Argus.IntegrationTests;

public sealed class CoordinatorWorkflowTests
{
    [Fact]
    public async Task CoordinatorWorkflow_CreateUploadAnalyzeGeneratePlanAndReload_Succeeds()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();

        var incident = await CreateIncidentAsync(httpClient);
        await UploadSampleEmailAsync(httpClient, incident.Id);
        await AnalyzeAsync(httpClient, incident.Id);

        var generateResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/coordinator/plan", content: null);
        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);

        var generatedPlan = await generateResponse.Content.ReadFromJsonAsync<CoordinatorPlanDto>();
        Assert.NotNull(generatedPlan);
        Assert.Equal(incident.Id, generatedPlan!.IncidentId);
        Assert.Equal("Completed", generatedPlan.Status);
        Assert.NotNull(generatedPlan.Plan);
        Assert.Equal("Credential Phishing", generatedPlan.Plan!.IncidentType);
        Assert.NotEmpty(generatedPlan.Plan.Tasks);
        Assert.NotEmpty(generatedPlan.Plan.MissingInformation);

        var getResponse = await httpClient.GetAsync($"/api/incidents/{incident.Id}/coordinator/plan");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var storedPlan = await getResponse.Content.ReadFromJsonAsync<CoordinatorPlanDto>();
        Assert.NotNull(storedPlan);
        Assert.Equal(generatedPlan.IncidentId, storedPlan!.IncidentId);
        Assert.Equal(generatedPlan.Status, storedPlan.Status);
        Assert.Equal(generatedPlan.Plan!.IncidentType, storedPlan.Plan!.IncidentType);
        Assert.Equal(generatedPlan.Plan.Tasks.Select(task => task.Id).OrderBy(id => id), storedPlan.Plan.Tasks.Select(task => task.Id).OrderBy(id => id));

        await using var scope = testHost.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
        var storedRun = await dbContext.CoordinatorRuns.SingleAsync(run => run.IncidentId == incident.Id);
        Assert.Equal(CoordinatorRunStatus.Completed, storedRun.Status);
        Assert.False(string.IsNullOrWhiteSpace(storedRun.OutputJson));
    }

    private static async Task<IncidentDto> CreateIncidentAsync(HttpClient httpClient)
    {
        var response = await httpClient.PostAsJsonAsync("/api/incidents", new CreateIncidentRequest(
            "Grace Community Church",
            OrganizationType.Church,
            "Our treasurer entered credentials after clicking a Microsoft email.",
            "Avery",
            TechnicalSkillLevel.Beginner));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var incident = await response.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.NotNull(incident);
        return incident!;
    }

    private static async Task UploadSampleEmailAsync(HttpClient httpClient, Guid incidentId)
    {
        await using var sampleStream = File.OpenRead(TestFileHelper.GetSampleEmailPath("phishing-microsoft-login.eml"));
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(sampleStream), "file", "phishing-microsoft-login.eml");

        var response = await httpClient.PostAsync($"/api/incidents/{incidentId}/evidence/email", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task AnalyzeAsync(HttpClient httpClient, Guid incidentId)
    {
        var response = await httpClient.PostAsync($"/api/incidents/{incidentId}/analyze", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}