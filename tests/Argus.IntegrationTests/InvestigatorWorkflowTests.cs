using Argus.Application.DTOs;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Argus.IntegrationTests;

public sealed class InvestigatorWorkflowTests
{
    [Fact]
    public async Task InvestigatorWorkflow_CreateUploadAnalyzePlanInvestigateAndReload_Succeeds()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();

        var incident = await CreateIncidentAsync(httpClient);
        await UploadSampleEmailAsync(httpClient, incident.Id);
        await AnalyzeAsync(httpClient, incident.Id);

        var planResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/coordinator/plan", content: null);
        Assert.Equal(HttpStatusCode.OK, planResponse.StatusCode);

        var runResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/investigator/run", content: null);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        var generatedReport = await runResponse.Content.ReadArgusJsonAsync<InvestigatorReportDto>();
        Assert.NotNull(generatedReport);
        Assert.Equal(incident.Id, generatedReport!.IncidentId);
        Assert.Equal("Completed", generatedReport.Status);
        Assert.NotNull(generatedReport.Report);
        Assert.NotEmpty(generatedReport.Report!.Findings);
        Assert.NotEmpty(generatedReport.Report.TaskResults);
        Assert.Contains(generatedReport.Report.TaskResults, result => string.Equals(result.Status, "Unsupported", StringComparison.OrdinalIgnoreCase));

        var getResponse = await httpClient.GetAsync($"/api/incidents/{incident.Id}/investigator/report");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var storedReport = await getResponse.Content.ReadArgusJsonAsync<InvestigatorReportDto>();
        Assert.NotNull(storedReport);
        Assert.Equal(generatedReport.Status, storedReport!.Status);
        Assert.Equal(generatedReport.Report.Classification, storedReport.Report!.Classification);
        Assert.Equal(generatedReport.Report.Findings.Select(finding => finding.Id).OrderBy(id => id), storedReport.Report.Findings.Select(finding => finding.Id).OrderBy(id => id));

        await using var scope = testHost.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
        var persistedRun = await dbContext.InvestigatorRuns.SingleAsync(run => run.IncidentId == incident.Id);

        Assert.Equal("Completed", persistedRun.Status.ToString());
        Assert.False(string.IsNullOrWhiteSpace(persistedRun.OutputJson));
    }

    private static async Task<IncidentDto> CreateIncidentAsync(HttpClient httpClient)
    {
        var response = await httpClient.PostAsJsonAsync("/api/incidents", new CreateIncidentRequest(
            "Grace Community Church",
            Domain.Enums.OrganizationType.Church,
            "Our treasurer entered credentials after clicking a Microsoft email.",
            "Avery",
            Domain.Enums.TechnicalSkillLevel.Beginner));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var incident = await response.Content.ReadArgusJsonAsync<IncidentDto>();
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
