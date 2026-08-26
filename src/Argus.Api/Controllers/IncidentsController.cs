using Argus.Application.DTOs;
using Argus.Application.Interfaces;
using Argus.Application.Validation;
using Argus.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Argus.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;
    private readonly IIncidentInvestigationService _incidentInvestigationService;
    private readonly ICoordinatorService _coordinatorService;
    private readonly IInvestigatorService _investigatorService;
    private readonly IResponseEducationService _responseEducationService;
    private readonly IEducationChatService _educationChatService;
    private readonly IAgenticWorkflowService _agenticWorkflowService;

    public IncidentsController(
        IIncidentService incidentService,
        IIncidentInvestigationService incidentInvestigationService,
        ICoordinatorService coordinatorService,
        IInvestigatorService investigatorService,
        IResponseEducationService responseEducationService,
        IEducationChatService educationChatService,
        IAgenticWorkflowService agenticWorkflowService)
    {
        _incidentService = incidentService;
        _incidentInvestigationService = incidentInvestigationService;
        _coordinatorService = coordinatorService;
        _investigatorService = investigatorService;
        _responseEducationService = responseEducationService;
        _educationChatService = educationChatService;
        _agenticWorkflowService = agenticWorkflowService;
    }

    [HttpPost]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentDto>> CreateAsync(
        [FromBody] CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var incident = await _incidentService.CreateAsync(request, cancellationToken);
            return Created($"/api/incidents/{incident.Id}", incident);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("request", ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _incidentService.GetByIdAsync(id, cancellationToken);
        return incident is null ? NotFound() : Ok(incident);
    }

    [HttpPost("{id:guid}/evidence/email")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<EvidenceItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvidenceItemDto>> UploadEmailEvidenceAsync(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            ModelState.AddModelError("file", "An .eml file is required.");
            return ValidationProblem(ModelState);
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var evidence = await _incidentInvestigationService.UploadEmailEvidenceAsync(
                id,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);

            return Created($"/api/incidents/{id}/evidence/email", evidence);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("file", ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpPost("{id:guid}/analyze")]
    [EnableRateLimiting("analysis")]
    [ProducesResponseType<PhishingAnalysisResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhishingAnalysisResult>> AnalyzeAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _incidentInvestigationService.AnalyzeAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("incident", ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpGet("{id:guid}/analysis")]
    [ProducesResponseType<PhishingAnalysisResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhishingAnalysisResult>> GetAnalysisAsync(Guid id, CancellationToken cancellationToken)
    {
        var analysis = await _incidentInvestigationService.GetAnalysisAsync(id, cancellationToken);
        return analysis is null ? NotFound() : Ok(analysis);
    }

    [HttpPost("{id:guid}/coordinator/plan")]
    [EnableRateLimiting("coordinator")]
    [ProducesResponseType<CoordinatorPlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CoordinatorPlanDto>> GenerateCoordinatorPlanAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _coordinatorService.GeneratePlanAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (CoordinatorPrerequisiteException ex)
        {
            ModelState.AddModelError("analysis", ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (CoordinatorPlanValidationException ex)
        {
            ModelState.AddModelError("plan", string.Join(" ", ex.ValidationErrors));
            return ValidationProblem(ModelState);
        }
        catch (CoordinatorUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Coordinator plan generation is temporarily unavailable.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{id:guid}/coordinator/plan")]
    [ProducesResponseType<CoordinatorPlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoordinatorPlanDto>> GetCoordinatorPlanAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await _coordinatorService.GetLatestPlanAsync(id, cancellationToken);
        return plan is null ? NotFound() : Ok(plan);
    }

    [HttpPost("{id:guid}/investigator/run")]
    [EnableRateLimiting("investigator")]
    [ProducesResponseType<InvestigatorReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InvestigatorReportDto>> RunInvestigatorAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _investigatorService.RunAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvestigatorPrerequisiteException ex)
        {
            ModelState.AddModelError("investigator", ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (InvestigatorValidationException ex)
        {
            ModelState.AddModelError("report", string.Join(" ", ex.ValidationErrors));
            return ValidationProblem(ModelState);
        }
        catch (InvestigatorUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Investigator run is temporarily unavailable.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{id:guid}/investigator/report")]
    [ProducesResponseType<InvestigatorReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvestigatorReportDto>> GetInvestigatorReportAsync(Guid id, CancellationToken cancellationToken)
    {
        var report = await _investigatorService.GetLatestSuccessfulReportAsync(id, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpPost("{id:guid}/response/generate")]
    [EnableRateLimiting("response")]
    [ProducesResponseType<ResponseEducationPackageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ResponseEducationPackageDto>> GenerateResponseEducationAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _responseEducationService.GenerateAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ResponseEducationPrerequisiteException ex)
        {
            ModelState.AddModelError("response", ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (ResponseEducationValidationException ex)
        {
            ModelState.AddModelError("package", string.Join(" ", ex.ValidationErrors));
            return ValidationProblem(ModelState);
        }
        catch (ResponseEducationUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Response and education generation is temporarily unavailable.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{id:guid}/response")]
    [ProducesResponseType<ResponseEducationPackageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResponseEducationPackageDto>> GetResponseEducationAsync(Guid id, CancellationToken cancellationToken)
    {
        var package = await _responseEducationService.GetLatestSuccessfulPackageAsync(id, cancellationToken);
        return package is null ? NotFound() : Ok(package);
    }

    [HttpPost("{id:guid}/education/chat")]
    [EnableRateLimiting("response")]
    [ProducesResponseType<EducationChatResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EducationChatResponseDto>> ChatEducationAsync(
        Guid id,
        [FromBody] EducationChatRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _educationChatService.ChatAsync(id, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("question", ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Education assistant is temporarily unavailable.",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("{id:guid}/workflow/run")]
    [EnableRateLimiting("workflow")]
    [ProducesResponseType<AgenticWorkflowResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgenticWorkflowResultDto>> RunWorkflowAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _agenticWorkflowService.RunAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/workflow")]
    [ProducesResponseType<AgenticWorkflowResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgenticWorkflowResultDto>> GetWorkflowAsync(Guid id, CancellationToken cancellationToken)
    {
        var workflow = await _agenticWorkflowService.GetLatestAsync(id, cancellationToken);
        return workflow is null ? NotFound() : Ok(workflow);
    }
}