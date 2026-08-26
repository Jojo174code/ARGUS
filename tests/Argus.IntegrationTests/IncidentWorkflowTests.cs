using Argus.Application.DTOs;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Argus.IntegrationTests;

public sealed class IncidentWorkflowTests
{
    [Fact]
    public async Task IncidentWorkflow_CreateUploadAnalyzeAndFetchAnalysis_Succeeds()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();

        var createResponse = await httpClient.PostAsJsonAsync("/api/incidents", new CreateIncidentRequest(
            "Grace Community Church",
            OrganizationType.Church,
            "Suspicious Microsoft sign-in notice received by finance staff.",
            "Avery",
            TechnicalSkillLevel.Beginner));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var incident = await createResponse.Content.ReadArgusJsonAsync<IncidentDto>();
        Assert.NotNull(incident);

        await using var sampleStream = File.OpenRead(TestFileHelper.GetSampleEmailPath("phishing-microsoft-login.eml"));
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(sampleStream), "file", "phishing-microsoft-login.eml");

        var uploadResponse = await httpClient.PostAsync($"/api/incidents/{incident!.Id}/evidence/email", content);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadedEvidence = await uploadResponse.Content.ReadArgusJsonAsync<EvidenceItemDto>();
        Assert.NotNull(uploadedEvidence);
        Assert.NotEqual(Guid.Empty, uploadedEvidence!.Id);
        Assert.Equal(incident.Id, uploadedEvidence.IncidentId);
        Assert.Equal(EvidenceType.EmailFile, uploadedEvidence.EvidenceType);
        Assert.Equal("phishing-microsoft-login.eml", uploadedEvidence.FileName);
        Assert.False(string.IsNullOrWhiteSpace(uploadedEvidence.Sha256));

        var analyzeResponse = await httpClient.PostAsync($"/api/incidents/{incident.Id}/analyze", content: null);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);

        var analysis = await analyzeResponse.Content.ReadArgusJsonAsync<PhishingAnalysisResult>();
        Assert.NotNull(analysis);
        Assert.Equal(incident.Id, analysis!.IncidentId);
        Assert.InRange(analysis.RiskScore, 0, 100);
        Assert.NotEqual(default, analysis.RiskLevel);
        Assert.NotEmpty(analysis.Indicators);
        Assert.Contains(analysis.Indicators, indicator => indicator.RuleId == "EMAIL-AUTH-001");
        Assert.Contains(analysis.MitreAttackMappings, mapping => mapping.TechniqueId == "T1566.002");

        var getAnalysisResponse = await httpClient.GetAsync($"/api/incidents/{incident.Id}/analysis");
        Assert.Equal(HttpStatusCode.OK, getAnalysisResponse.StatusCode);

        var storedAnalysis = await getAnalysisResponse.Content.ReadArgusJsonAsync<PhishingAnalysisResult>();
        Assert.NotNull(storedAnalysis);
        Assert.Equal(incident.Id, storedAnalysis!.IncidentId);
        Assert.Equal(analysis.RiskScore, storedAnalysis.RiskScore);
        Assert.Equal(analysis.RiskLevel, storedAnalysis.RiskLevel);
        Assert.Equal(analysis.Indicators.Select(x => x.RuleId).OrderBy(x => x), storedAnalysis.Indicators.Select(x => x.RuleId).OrderBy(x => x));
    }

    [Fact]
    public async Task UploadEmail_WithInvalidExtension_ReturnsBadRequestAndDoesNotPersistEvidence()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();
        var incident = await CreateIncidentAsync(httpClient);

        var fileCountBefore = CountStoredFiles(testHost.UploadDirectory);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("safe-text"u8.ToArray()), "file", "malicious.txt");

        var response = await httpClient.PostAsync($"/api/incidents/{incident.Id}/evidence/email", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await GetIncidentEvidenceCountAsync(testHost, incident.Id));
        Assert.Equal(fileCountBefore, CountStoredFiles(testHost.UploadDirectory));
    }

    [Fact]
    public async Task UploadEmail_WithEmptyFile_ReturnsBadRequestAndDoesNotPersistEvidence()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();
        var incident = await CreateIncidentAsync(httpClient);

        var fileCountBefore = CountStoredFiles(testHost.UploadDirectory);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Array.Empty<byte>()), "file", "empty.eml");

        var response = await httpClient.PostAsync($"/api/incidents/{incident.Id}/evidence/email", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await GetIncidentEvidenceCountAsync(testHost, incident.Id));
        Assert.Equal(fileCountBefore, CountStoredFiles(testHost.UploadDirectory));
    }

    [Fact]
    public async Task UploadEmail_WithOversizedFile_ReturnsBadRequestAndDoesNotPersistEvidence()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();
        var incident = await CreateIncidentAsync(httpClient);

        var oversizedPayload = new byte[testHost.MaxUploadSizeBytes + 1];
        var fileCountBefore = CountStoredFiles(testHost.UploadDirectory);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(oversizedPayload), "file", "oversized.eml");

        var response = await httpClient.PostAsync($"/api/incidents/{incident.Id}/evidence/email", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await GetIncidentEvidenceCountAsync(testHost, incident.Id));
        Assert.Equal(fileCountBefore, CountStoredFiles(testHost.UploadDirectory));
    }

    [Fact]
    public async Task AnalyzeIncident_WithoutEvidence_ReturnsBadRequestAndDoesNotPersistAnalysis()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();
        var incident = await CreateIncidentAsync(httpClient);

        var response = await httpClient.PostAsync($"/api/incidents/{incident.Id}/analyze", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var validation = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(validation);
        Assert.Contains(validation!.Errors.SelectMany(pair => pair.Value), message => message.Contains(".eml evidence file", StringComparison.OrdinalIgnoreCase));

        await using var scope = testHost.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();

        var persistedIncident = await dbContext.Incidents.SingleAsync(x => x.Id == incident.Id);
        Assert.NotEqual(IncidentStatus.Completed, persistedIncident.Status);

        var hasAnalysis = await dbContext.IncidentAnalyses.AnyAsync(x => x.IncidentId == incident.Id);
        Assert.False(hasAnalysis);
    }

    [Fact]
    public async Task UploadEmail_ForMissingIncident_ReturnsNotFoundAndDoesNotPersistEvidence()
    {
        await using var testHost = new ArgusWebApplicationFactory();
        using var httpClient = testHost.CreateClient();

        var missingIncidentId = Guid.NewGuid();
        var fileCountBefore = CountStoredFiles(testHost.UploadDirectory);

        await using var sampleStream = File.OpenRead(TestFileHelper.GetSampleEmailPath("phishing-microsoft-login.eml"));
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(sampleStream), "file", "phishing-microsoft-login.eml");

        var response = await httpClient.PostAsync($"/api/incidents/{missingIncidentId}/evidence/email", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(fileCountBefore, CountStoredFiles(testHost.UploadDirectory));

        await using var scope = testHost.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
        Assert.False(await dbContext.EvidenceItems.AnyAsync(x => x.IncidentId == missingIncidentId));
        Assert.False(await dbContext.Incidents.AnyAsync(x => x.Id == missingIncidentId));
    }

    private static async Task<IncidentDto> CreateIncidentAsync(HttpClient httpClient)
    {
        var createResponse = await httpClient.PostAsJsonAsync("/api/incidents", new CreateIncidentRequest(
            "Grace Community Church",
            OrganizationType.Church,
            "Suspicious Microsoft sign-in notice received by finance staff.",
            "Avery",
            TechnicalSkillLevel.Beginner));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var incident = await createResponse.Content.ReadArgusJsonAsync<IncidentDto>();
        Assert.NotNull(incident);
        return incident!;
    }

    private static async Task<int> GetIncidentEvidenceCountAsync(ArgusWebApplicationFactory testHost, Guid incidentId)
    {
        await using var scope = testHost.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();
        return await dbContext.EvidenceItems.CountAsync(x => x.IncidentId == incidentId);
    }

    private static int CountStoredFiles(string uploadDirectory)
    {
        if (!Directory.Exists(uploadDirectory))
        {
            return 0;
        }

        return Directory.GetFiles(uploadDirectory, "*", SearchOption.TopDirectoryOnly).Length;
    }
}