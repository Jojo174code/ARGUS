using Argus.Application.DTOs;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Argus.IntegrationTests;

public sealed class ResponseEducationWorkflowTests
{
    [Fact]
    public async Task ResponseEducationWorkflow_CreateUploadAnalyzeInvestigateGenerateAndReload_Succeeds()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();

        var incident = await CreateIncidentAsync(httpClient);
        await UploadSampleEmailAsync(httpClient, incident.Id);
        await AnalyzeAsync(httpClient, incident.Id);

        Assert.Equal(HttpStatusCode.OK, (await httpClient.PostAsync($"/api/incidents/{incident.Id}/coordinator/plan", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await httpClient.PostAsync($"/api/incidents/{incident.Id}/investigator/run", content: null)).StatusCode);

        var generateResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/response/generate", content: null);
        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);

        var generatedPackage = await generateResponse.Content.ReadArgusJsonAsync<ResponseEducationPackageDto>();
        Assert.NotNull(generatedPackage);
        Assert.Equal(incident.Id, generatedPackage!.IncidentId);
        Assert.Equal("Completed", generatedPackage.Status);
        Assert.NotNull(generatedPackage.Package);
        Assert.NotEmpty(generatedPackage.Package!.ImmediateActions);
        Assert.NotEmpty(generatedPackage.Package.Education.Questions);

        var getResponse = await httpClient.GetAsync($"/api/incidents/{incident.Id}/response");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var storedPackage = await getResponse.Content.ReadArgusJsonAsync<ResponseEducationPackageDto>();
        Assert.NotNull(storedPackage);
        Assert.Equal(generatedPackage.Package.PlainLanguageSummary, storedPackage!.Package!.PlainLanguageSummary);
        Assert.Equal(generatedPackage.Package.ImmediateActions.Select(action => action.Id).OrderBy(id => id), storedPackage.Package.ImmediateActions.Select(action => action.Id).OrderBy(id => id));

        await using var scope = testHost.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
        var persistedRun = await dbContext.ResponseEducationRuns.SingleAsync(run => run.IncidentId == incident.Id);

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