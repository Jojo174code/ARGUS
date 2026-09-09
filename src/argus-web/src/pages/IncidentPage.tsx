import { useEffect, useMemo, useState } from 'react';
import type { ChangeEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  analyzeIncident,
  generateCoordinatorPlan,
  generateResponseEducationPackage,
  getCoordinatorPlan,
  getIncident,
  getIncidentAnalysis,
  getInvestigatorReport,
  getResponseEducationPackage,
  getWorkflow,
  runFullWorkflow,
  runInvestigator,
  uploadEmailEvidence,
} from '../api/incidents';
import type {
  AnalysisResult,
  AgenticWorkflowResultResponse,
  CoordinatorPlanResponse,
  Incident,
  InvestigatorReportResponse,
  ResponseAction,
  ResponseEducationPackageResponse,
} from '../api/types';
import { EmailSummary } from '../components/EmailSummary';
import { ErrorBanner } from '../components/ErrorBanner';
import { IndicatorsList } from '../components/IndicatorsList';
import { LoadingBlock } from '../components/LoadingBlock';
import { MitreMappings } from '../components/MitreMappings';
import { RiskBadge } from '../components/RiskBadge';
import { StatusBadge } from '../components/StatusBadge';
import { UrlTable } from '../components/UrlTable';
import { IncidentOverviewCharts } from '../components/IncidentOverviewCharts';
import { WorkflowTimeline } from '../components/WorkflowTimeline';
import { IncidentWorkspaceNav } from '../components/IncidentWorkspaceNav';

const MAX_UPLOAD_SIZE_BYTES = 2_097_152;

function renderDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function renderCoordinatorStatus(
  status: CoordinatorPlanResponse['status'] | 'NotStarted',
  isGenerating: boolean,
): string {
  if (isGenerating) {
    return 'Generating';
  }

  switch (status) {
    case 'Completed':
      return 'Completed';
    case 'Failed':
      return 'Failed';
    case 'Running':
      return 'Generating';
    default:
      return 'Not started';
  }
}

function renderReadyForInvestigation(plan: CoordinatorPlanResponse['plan'] | null): string {
  if (!plan) {
    return 'Not available';
  }

  return plan.readyForInvestigation ? 'Yes' : 'No - additional information required';
}

function renderInvestigatorStatus(
  status: InvestigatorReportResponse['status'] | 'NotStarted',
  isRunning: boolean,
): string {
  if (isRunning) {
    return 'Running';
  }

  switch (status) {
    case 'Completed':
      return 'Completed';
    case 'Failed':
      return 'Failed';
    case 'Running':
      return 'Running';
    default:
      return 'Not started';
  }
}

function renderResponseStatus(
  status: ResponseEducationPackageResponse['status'] | 'NotStarted',
  isGenerating: boolean,
): string {
  if (isGenerating) {
    return 'Generating';
  }

  switch (status) {
    case 'Completed':
      return 'Completed';
    case 'Failed':
      return 'Failed';
    case 'Running':
      return 'Generating';
    default:
      return 'Not started';
  }
}

function renderActionType(actionType: ResponseAction['actionType']): string {
  switch (actionType) {
    case 'UserAction':
      return 'User Action';
    case 'AdministratorAction':
      return 'Administrator Action';
    case 'ProfessionalEscalation':
      return 'Professional Escalation';
    default:
      return 'Informational';
  }
}

function isTerminalWorkflowStatus(status: AgenticWorkflowResultResponse['status']): boolean {
  return status === 'Completed' || status === 'Failed' || status === 'AwaitingInformation';
}

export function IncidentPage() {
  const { id } = useParams<{ id: string }>();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [analysis, setAnalysis] = useState<AnalysisResult | null>(null);
  const [coordinatorPlan, setCoordinatorPlan] = useState<CoordinatorPlanResponse | null>(null);
  const [investigatorReport, setInvestigatorReport] = useState<InvestigatorReportResponse | null>(null);
  const [responsePackage, setResponsePackage] = useState<ResponseEducationPackageResponse | null>(null);
  const [workflow, setWorkflow] = useState<AgenticWorkflowResultResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [coordinatorError, setCoordinatorError] = useState<string | null>(null);
  const [investigatorError, setInvestigatorError] = useState<string | null>(null);
  const [responseError, setResponseError] = useState<string | null>(null);
  const [workflowError, setWorkflowError] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [analyzing, setAnalyzing] = useState(false);
  const [generatingPlan, setGeneratingPlan] = useState(false);
  const [runningInvestigator, setRunningInvestigator] = useState(false);
  const [generatingResponsePackage, setGeneratingResponsePackage] = useState(false);
  const [runningWorkflow, setRunningWorkflow] = useState(false);
  const [isWorkflowPolling, setIsWorkflowPolling] = useState(false);
  const [selectedQuizAnswers, setSelectedQuizAnswers] = useState<Record<string, number>>({});
  const [submittedQuizAnswers, setSubmittedQuizAnswers] = useState<Record<string, boolean>>({});

  const hasEvidence = useMemo(() => (incident?.evidenceItems.length ?? 0) > 0, [incident]);
  const incidentRiskLabel = analysis?.riskLevel ?? 'Unknown';
  const findingsCount = investigatorReport?.report?.findings.length ?? 0;
  const responseActionsCount = responsePackage?.package
    ? responsePackage.package.immediateActions.length
      + responsePackage.package.recoveryActions.length
      + responsePackage.package.preventionActions.length
    : 0;

  const workflowCompletion = useMemo(() => {
    if (!workflow) {
      return 0;
    }

    const completedCount = workflow.stages.filter((stage) => stage.status === 'Completed').length;
    return Math.round((completedCount / workflow.stages.length) * 100);
  }, [workflow]);

  const stageSnapshot = useMemo(() => {
    return [
      {
        label: 'Deterministic Analysis',
        status: workflow?.stages.find((stage) => stage.stage === 'DeterministicAnalysis')?.status ?? 'Pending',
      },
      {
        label: 'Coordinator',
        status: workflow?.stages.find((stage) => stage.stage === 'Coordinator')?.status ?? 'Pending',
      },
      {
        label: 'Investigator',
        status: workflow?.stages.find((stage) => stage.stage === 'Investigator')?.status ?? 'Pending',
      },
      {
        label: 'Response & Learning',
        status: workflow?.stages.find((stage) => stage.stage === 'ResponseEducation')?.status ?? 'Pending',
      },
    ];
  }, [workflow]);

  const nextActionText = useMemo(() => {
    if (!hasEvidence) {
      return 'Upload at least one .eml evidence file to unlock analysis and workflow actions.';
    }

    if (!analysis) {
      return 'Run deterministic analysis to extract phishing indicators and risk signals.';
    }

    if (!coordinatorPlan?.plan || coordinatorPlan.status !== 'Completed') {
      return 'Generate an investigation plan to sequence the next deterministic tasks.';
    }

    if (!coordinatorPlan.plan.readyForInvestigation) {
      return 'Coordinator is waiting for required missing information before investigation can continue.';
    }

    if (!investigatorReport?.report || investigatorReport.status !== 'Completed') {
      return 'Run Investigator to synthesize evidence-backed findings.';
    }

    if (!responsePackage?.package || responsePackage.status !== 'Completed') {
      return 'Generate Response & Learning to produce action steps and staff guidance.';
    }

    return 'Workflow is complete. Review findings, action lists, and learning questions below.';
  }, [analysis, coordinatorPlan, hasEvidence, investigatorReport, responsePackage]);

  useEffect(() => {
    if (!id) {
      setError('Missing incident identifier.');
      setLoading(false);
      return;
    }
    const incidentId = id;

    let disposed = false;

    async function loadIncidentState(): Promise<void> {
      setLoading(true);
      setError(null);
      setCoordinatorError(null);
      setInvestigatorError(null);
      setResponseError(null);
      setWorkflowError(null);

      try {
        const loadedIncident = await getIncident(incidentId);
        if (disposed) {
          return;
        }

        setIncident(loadedIncident);

        if (loadedIncident.status === 'Completed') {
          const loadedAnalysis = await getIncidentAnalysis(incidentId);
          if (!disposed) {
            setAnalysis(loadedAnalysis);
          }
        }

        try {
          const loadedPlan = await getCoordinatorPlan(incidentId);
          if (!disposed) {
            setCoordinatorPlan(loadedPlan);
          }
        } catch (planError) {
          if (!disposed) {
            if (planError instanceof Error && 'status' in planError && (planError as { status?: number }).status === 404) {
              setCoordinatorPlan(null);
            } else {
              setCoordinatorError(planError instanceof Error ? planError.message : 'Failed to load coordinator plan.');
            }
          }
        }

        try {
          const loadedReport = await getInvestigatorReport(incidentId);
          if (!disposed) {
            setInvestigatorReport(loadedReport);
          }
        } catch (reportError) {
          if (!disposed) {
            if (reportError instanceof Error && 'status' in reportError && (reportError as { status?: number }).status === 404) {
              setInvestigatorReport(null);
            } else {
              setInvestigatorError(reportError instanceof Error ? reportError.message : 'Failed to load investigator report.');
            }
          }
        }

        try {
          const loadedPackage = await getResponseEducationPackage(incidentId);
          if (!disposed) {
            setResponsePackage(loadedPackage);
          }
        } catch (packageError) {
          if (!disposed) {
            if (packageError instanceof Error && 'status' in packageError && (packageError as { status?: number }).status === 404) {
              setResponsePackage(null);
            } else {
              setResponseError(packageError instanceof Error ? packageError.message : 'Failed to load response and learning package.');
            }
          }
        }

        try {
          const loadedWorkflow = await getWorkflow(incidentId);
          if (!disposed) {
            setWorkflow(loadedWorkflow);
          }
        } catch (workflowLoadError) {
          if (!disposed) {
            if (workflowLoadError instanceof Error && 'status' in workflowLoadError && (workflowLoadError as { status?: number }).status === 404) {
              setWorkflow(null);
            } else {
              setWorkflowError(workflowLoadError instanceof Error ? workflowLoadError.message : 'Failed to load workflow status.');
            }
          }
        }
      } catch (loadError) {
        if (!disposed) {
          setError(loadError instanceof Error ? loadError.message : 'Failed to load incident.');
        }
      } finally {
        if (!disposed) {
          setLoading(false);
        }
      }
    }

    void loadIncidentState();

    return () => {
      disposed = true;
    };
  }, [id]);

  useEffect(() => {
    if (!id || !isWorkflowPolling) {
      return;
    }

    const incidentId = id;
    let disposed = false;
    let requestInFlight = false;

    async function pollWorkflow(): Promise<void> {
      if (requestInFlight) {
        return;
      }

      requestInFlight = true;
      try {
        const nextWorkflow = await getWorkflow(incidentId);
        if (disposed) {
          return;
        }

        setWorkflow(nextWorkflow);
        if (isTerminalWorkflowStatus(nextWorkflow.status)) {
          setIsWorkflowPolling(false);
        }
      } catch (pollError) {
        if (disposed) {
          return;
        }

        if (pollError instanceof Error && 'status' in pollError && (pollError as { status?: number }).status === 404) {
          return;
        }

        setWorkflowError(pollError instanceof Error ? pollError.message : 'Failed to refresh workflow status.');
      } finally {
        requestInFlight = false;
      }
    }

    void pollWorkflow();
    const intervalId = window.setInterval(() => void pollWorkflow(), 1500);

    return () => {
      disposed = true;
      window.clearInterval(intervalId);
    };
  }, [id, isWorkflowPolling]);

  async function handleFileSelection(event: ChangeEvent<HTMLInputElement>): Promise<void> {
    const nextFile = event.target.files?.[0] ?? null;
    if (!nextFile) {
      setSelectedFile(null);
      return;
    }

    if (!nextFile.name.toLowerCase().endsWith('.eml')) {
      setError('Only .eml files are accepted.');
      setSelectedFile(null);
      return;
    }

    if (nextFile.size > MAX_UPLOAD_SIZE_BYTES) {
      setError('The selected file exceeds the 2 MB upload limit.');
      setSelectedFile(null);
      return;
    }

    setError(null);
    setSelectedFile(nextFile);
  }

  async function handleUpload(): Promise<void> {
    if (!id || !selectedFile) {
      return;
    }

    setUploading(true);
    setError(null);

    try {
      await uploadEmailEvidence(id, selectedFile);
      const refreshedIncident = await getIncident(id);
      setIncident(refreshedIncident);
      setSelectedFile(null);
      setAnalysis(null);
      setCoordinatorPlan(null);
      setInvestigatorReport(null);
      setResponsePackage(null);
      setWorkflow(null);
      setCoordinatorError(null);
      setInvestigatorError(null);
      setResponseError(null);
      setWorkflowError(null);
      setSelectedQuizAnswers({});
      setSubmittedQuizAnswers({});
    } catch (uploadError) {
      setError(uploadError instanceof Error ? uploadError.message : 'Upload failed.');
    } finally {
      setUploading(false);
    }
  }

  async function handleAnalyze(): Promise<void> {
    if (!id) {
      return;
    }

    setAnalyzing(true);
    setError(null);

    try {
      const nextAnalysis = await analyzeIncident(id);
      const refreshedIncident = await getIncident(id);
      setIncident(refreshedIncident);
      setAnalysis(nextAnalysis);
      setCoordinatorPlan(null);
      setInvestigatorReport(null);
      setResponsePackage(null);
      setWorkflow(null);
      setCoordinatorError(null);
      setInvestigatorError(null);
      setResponseError(null);
      setWorkflowError(null);
      setSelectedQuizAnswers({});
      setSubmittedQuizAnswers({});
    } catch (analyzeError) {
      setError(analyzeError instanceof Error ? analyzeError.message : 'Analysis failed.');
    } finally {
      setAnalyzing(false);
    }
  }

  async function handleGeneratePlan(): Promise<void> {
    if (!id) {
      return;
    }

    setGeneratingPlan(true);
    setCoordinatorError(null);

    try {
      const nextPlan = await generateCoordinatorPlan(id);
      setCoordinatorPlan(nextPlan);
      setInvestigatorReport(null);
      setResponsePackage(null);
      setWorkflow(null);
      setInvestigatorError(null);
      setResponseError(null);
      setWorkflowError(null);
      setSelectedQuizAnswers({});
      setSubmittedQuizAnswers({});
    } catch (planError) {
      setCoordinatorError(planError instanceof Error ? planError.message : 'Plan generation failed.');
    } finally {
      setGeneratingPlan(false);
    }
  }

  async function handleRunInvestigator(): Promise<void> {
    if (!id) {
      return;
    }

    setRunningInvestigator(true);
    setInvestigatorError(null);

    try {
      const nextReport = await runInvestigator(id);
      setInvestigatorReport(nextReport);
      setResponsePackage(null);
      setWorkflow(null);
      setResponseError(null);
      setWorkflowError(null);
      setSelectedQuizAnswers({});
      setSubmittedQuizAnswers({});
    } catch (runError) {
      setInvestigatorError(runError instanceof Error ? runError.message : 'Investigator run failed.');
    } finally {
      setRunningInvestigator(false);
    }
  }

  async function handleGenerateResponsePackage(): Promise<void> {
    if (!id) {
      return;
    }

    setGeneratingResponsePackage(true);
    setResponseError(null);

    try {
      const nextPackage = await generateResponseEducationPackage(id);
      setResponsePackage(nextPackage);
      setWorkflow(null);
      setSelectedQuizAnswers({});
      setSubmittedQuizAnswers({});
    } catch (packageError) {
      setResponseError(packageError instanceof Error ? packageError.message : 'Response and learning generation failed.');
    } finally {
      setGeneratingResponsePackage(false);
    }
  }

  function handleQuizSelection(questionId: string, optionIndex: number): void {
    setSelectedQuizAnswers((current) => ({
      ...current,
      [questionId]: optionIndex,
    }));
  }

  function handleQuizSubmit(questionId: string): void {
    setSubmittedQuizAnswers((current) => ({
      ...current,
      [questionId]: true,
    }));
  }

  async function handleRunWorkflow(): Promise<void> {
    if (!id) {
      return;
    }

    setRunningWorkflow(true);
    setIsWorkflowPolling(true);
    setWorkflow(null);
    setWorkflowError(null);

    try {
      const nextWorkflow = await runFullWorkflow(id);
      setWorkflow(nextWorkflow);
      if (isTerminalWorkflowStatus(nextWorkflow.status)) {
        setIsWorkflowPolling(false);
      }

      const refreshedIncident = await getIncident(id);
      setIncident(refreshedIncident);

      try {
        setAnalysis(await getIncidentAnalysis(id));
      } catch {
        setAnalysis(null);
      }

      try {
        setCoordinatorPlan(await getCoordinatorPlan(id));
      } catch {
        setCoordinatorPlan(null);
      }

      try {
        setInvestigatorReport(await getInvestigatorReport(id));
      } catch {
        setInvestigatorReport(null);
      }

      try {
        setResponsePackage(await getResponseEducationPackage(id));
      } catch {
        setResponsePackage(null);
      }

      setSelectedQuizAnswers({});
      setSubmittedQuizAnswers({});
    } catch (workflowRunError) {
      setWorkflowError(workflowRunError instanceof Error ? workflowRunError.message : 'Workflow run failed.');
    } finally {
      setRunningWorkflow(false);
    }
  }

  if (loading) {
    return <LoadingBlock message="Loading incident details..." />;
  }

  if (error && !incident) {
    return (
      <section className="card">
        <ErrorBanner message={error} />
        <Link to="/new-incident" className="button-primary">
          Create Incident
        </Link>
      </section>
    );
  }

  if (!incident) {
    return (
      <section className="card">
        <h1>Incident Unavailable</h1>
        <p>ARGUS could not load this incident.</p>
      </section>
    );
  }

  return (
    <section className="space-stack">
      <IncidentWorkspaceNav incidentId={incident.id} />

      <article className="card">
        <h1>Incident {incident.id}</h1>
        <div className="row-between wrap-gap">
          <StatusBadge status={incident.status} />
          <span>Created {renderDate(incident.createdAt)}</span>
        </div>
        <dl className="summary-grid">
          <div>
            <dt>Organization</dt>
            <dd>{incident.organizationName}</dd>
          </div>
          <div>
            <dt>Type</dt>
            <dd>{incident.organizationType}</dd>
          </div>
          <div>
            <dt>Reported By</dt>
            <dd>{incident.reportedBy}</dd>
          </div>
          <div>
            <dt>Technical Skill</dt>
            <dd>{incident.technicalSkillLevel}</dd>
          </div>
          <div className="full-row">
            <dt>Description</dt>
            <dd>{incident.description}</dd>
          </div>
        </dl>
      </article>

      <article className="card mission-strip">
        <div className="mission-strip-header">
          <h2>What Happens Next</h2>
          <p>{nextActionText}</p>
        </div>
        <div className="mission-metrics">
          <div className="metric-tile">
            <span className="metric-label">Evidence Files</span>
            <strong className="metric-value">{incident.evidenceItems.length}</strong>
          </div>
          <div className="metric-tile">
            <span className="metric-label">Current Risk</span>
            <strong className="metric-value">{incidentRiskLabel}</strong>
          </div>
          <div className="metric-tile">
            <span className="metric-label">Workflow Completion</span>
            <strong className="metric-value">{workflowCompletion}%</strong>
          </div>
          <div className="metric-tile">
            <span className="metric-label">Investigator Findings</span>
            <strong className="metric-value">{findingsCount}</strong>
          </div>
          <div className="metric-tile">
            <span className="metric-label">Response Actions</span>
            <strong className="metric-value">{responseActionsCount}</strong>
          </div>
        </div>
      </article>

      <article className="card pulse-board">
        <div className="row-between wrap-gap">
          <h2>Case Pulse</h2>
          <span className="soft-label">Fast status scan</span>
        </div>
        <div className="pulse-grid">
          {stageSnapshot.map((stage) => (
            <div key={stage.label} className={`pulse-tile pulse-${stage.status.toLowerCase()}`}>
              <span className="pulse-label">{stage.label}</span>
              <strong className="pulse-value">{stage.status}</strong>
            </div>
          ))}
        </div>
      </article>

      <IncidentOverviewCharts
        analysis={analysis}
        workflow={workflow}
        investigatorReport={investigatorReport}
        responsePackage={responsePackage}
      />

      <article className="card space-stack">
        <h2>Run Full ARGUS Workflow</h2>
        <p>
          This orchestration layer reuses deterministic analysis, Coordinator, Investigator, and Response &amp; Learning in a controlled workflow.
        </p>
        <button
          type="button"
          className="button-primary"
          disabled={runningWorkflow || !hasEvidence}
          onClick={() => void handleRunWorkflow()}
        >
          {runningWorkflow ? 'Running Full ARGUS Workflow...' : 'Run Full ARGUS Workflow'}
        </button>
        {!hasEvidence ? <p>Upload at least one .eml file before running the full workflow.</p> : null}
        {workflowError ? <ErrorBanner message={workflowError} /> : null}
      </article>

      <WorkflowTimeline workflow={workflow} isActive={isWorkflowPolling} />

      <article className="card space-stack">
        <h2>Email Evidence Upload</h2>
        <p>Upload one or more .eml files. ARGUS stores sanitized file names and secure hashes.</p>
        <input type="file" accept=".eml" onChange={(event) => void handleFileSelection(event)} />
        {selectedFile ? (
          <p>
            Ready to upload: <strong>{selectedFile.name}</strong> ({selectedFile.size} bytes)
          </p>
        ) : null}
        <button
          type="button"
          className="button-primary"
          disabled={!selectedFile || uploading}
          onClick={() => void handleUpload()}
        >
          {uploading ? 'Uploading...' : 'Upload Evidence'}
        </button>

        <h3>Uploaded Evidence</h3>
        {incident.evidenceItems.length === 0 ? (
          <p>No evidence has been uploaded yet.</p>
        ) : (
          <ul className="evidence-list">
            {incident.evidenceItems.map((item) => (
              <li key={item.id}>
                <div className="row-between wrap-gap">
                  <strong>{item.fileName}</strong>
                  <span>{item.fileSize} bytes</span>
                </div>
                <p>
                  Uploaded: {renderDate(item.uploadedAt)}
                </p>
                <p className="mono-text">SHA-256: {item.sha256}</p>
              </li>
            ))}
          </ul>
        )}
      </article>

      <article className="card space-stack">
        <h2>Run Deterministic Analysis</h2>
        <button
          type="button"
          className="button-primary"
          disabled={!hasEvidence || analyzing}
          onClick={() => void handleAnalyze()}
        >
          {analyzing ? 'Analyzing...' : 'Analyze Incident'}
        </button>
        {!hasEvidence ? <p>Upload at least one .eml file before analysis.</p> : null}
      </article>

      {error ? <ErrorBanner message={error} /> : null}

      {analysis ? (
        <>
          <article className="card">
            <h2>Analysis Result</h2>
            <div className="row-between wrap-gap">
              <RiskBadge riskLevel={analysis.riskLevel} />
              <span>Score: {analysis.riskScore}</span>
              <span>Analyzed {renderDate(analysis.analyzedAt)}</span>
            </div>
            <p>{analysis.summary}</p>
          </article>

          <EmailSummary email={analysis.email} />
          <IndicatorsList indicators={analysis.indicators} />
          <UrlTable urls={analysis.email.urls} />
          <MitreMappings techniques={analysis.mitreAttackMappings} />
        </>
      ) : null}

      <article className="card space-stack">
        <h2>Investigation Plan</h2>
        <p>Coordinator builds a prioritized plan from deterministic evidence.</p>
        <div className="row-between wrap-gap">
          <span>Coordinator status: {renderCoordinatorStatus(coordinatorPlan?.status ?? 'NotStarted', generatingPlan)}</span>
          {coordinatorPlan?.completedAt ? <span>Plan generated at {renderDate(coordinatorPlan.completedAt)}</span> : null}
        </div>
        <div className="row-between wrap-gap">
          <span>Incident classification: {coordinatorPlan?.plan?.incidentType ?? 'Not started'}</span>
          <span>Priority: {coordinatorPlan?.plan?.priority?.toUpperCase() ?? 'Not started'}</span>
        </div>
        <div>
          <strong>Ready for Investigation:</strong> {renderReadyForInvestigation(coordinatorPlan?.plan ?? null)}
        </div>
        <button
          type="button"
          className="button-primary"
          disabled={generatingPlan || !analysis}
          onClick={() => void handleGeneratePlan()}
        >
          {generatingPlan ? 'Generating Investigation Plan...' : 'Generate Investigation Plan'}
        </button>
        {!analysis ? <p>Run deterministic analysis before generating a plan.</p> : null}
        {coordinatorError ? <ErrorBanner message={coordinatorError} /> : null}

        {coordinatorPlan?.plan ? (
          <>
            <section className="card-inner">
              <h3>Investigation Tasks</h3>
              <ol className="visual-list">
                {[...coordinatorPlan.plan.tasks]
                  .sort((left, right) => left.priority - right.priority)
                  .map((task) => (
                    <li key={task.id} className="visual-item">
                      <strong>{task.title}</strong>
                      <p>{task.description}</p>
                      <div className="meta-chip-row">
                        <span className="meta-chip">Priority {task.priority}</span>
                        <span className="meta-chip">Evidence: {task.requiredEvidence.length > 0 ? task.requiredEvidence.join(', ') : 'None'}</span>
                      </div>
                      <p><strong>Done when:</strong> {task.completionCondition}</p>
                    </li>
                  ))}
              </ol>
            </section>

            <section className="card-inner">
              <h3>Missing Information</h3>
              {coordinatorPlan.plan.missingInformation.length === 0 ? (
                <p>No missing information identified.</p>
              ) : (
                <ul className="missing-info-list">
                  {coordinatorPlan.plan.missingInformation.map((item) => (
                    <li key={item.question}>
                      <strong>{item.question}</strong>
                      <p>{item.reason}</p>
                      <p>{item.required ? 'Required' : 'Optional'}</p>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            {coordinatorPlan.plan.assumptions.length > 0 ? (
              <div className="card-inner">
                <h3>Assumptions</h3>
                <ul>
                  {coordinatorPlan.plan.assumptions.map((assumption) => (
                    <li key={assumption}>{assumption}</li>
                  ))}
                </ul>
              </div>
            ) : null}

            {coordinatorPlan.plan.safetyNotes.length > 0 ? (
              <div className="card-inner">
                <h3>Safety Notes</h3>
                <ul>
                  {coordinatorPlan.plan.safetyNotes.map((note) => (
                    <li key={note}>{note}</li>
                  ))}
                </ul>
              </div>
            ) : null}
          </>
        ) : null}
      </article>

      <article className="card space-stack">
        <h2>Investigation Findings</h2>
        <p>Evidence-grounded synthesis from deterministic outputs.</p>
        <div className="row-between wrap-gap">
          <span>
            Investigator status: {renderInvestigatorStatus(investigatorReport?.status ?? 'NotStarted', runningInvestigator)}
          </span>
          {investigatorReport?.completedAt ? <span>Completed at {renderDate(investigatorReport.completedAt)}</span> : null}
        </div>
        <button
          type="button"
          className="button-primary"
          disabled={
            runningInvestigator
            || !coordinatorPlan?.plan
            || coordinatorPlan.status !== 'Completed'
            || !coordinatorPlan.plan.readyForInvestigation
          }
          onClick={() => void handleRunInvestigator()}
        >
          {runningInvestigator ? 'Running Investigation...' : 'Run Investigation'}
        </button>
        {!coordinatorPlan?.plan ? <p>Generate a coordinator plan before running investigator.</p> : null}
        {coordinatorPlan?.plan && !coordinatorPlan.plan.readyForInvestigation ? (
          <p>Investigator run is disabled until required missing information is resolved.</p>
        ) : null}
        {investigatorError ? <ErrorBanner message={investigatorError} /> : null}

        {investigatorReport?.report ? (
          <>
            <div className="card-inner">
              <h3>Report Summary</h3>
              <div className="meta-chip-row">
                <span className="meta-chip">{investigatorReport.report.classification}</span>
                <span className="meta-chip">Severity: {investigatorReport.report.severity}</span>
                <span className="meta-chip">Confidence: {Math.round(investigatorReport.report.confidence * 100)}%</span>
              </div>
            </div>

            <section className="card-inner">
              <h3>Findings</h3>
              {investigatorReport.report.findings.length === 0 ? (
                <p>No findings reported.</p>
              ) : (
                <ol className="visual-list">
                  {investigatorReport.report.findings.map((finding) => (
                    <li key={finding.id} className="visual-item">
                      <strong>{finding.title}</strong>
                      <p>{finding.description}</p>
                      <div className="meta-chip-row">
                        <span className="meta-chip">{finding.severity}</span>
                        <span className="meta-chip">Confidence {Math.round(finding.confidence * 100)}%</span>
                        <span className="meta-chip">Source: {finding.evidenceSource}</span>
                      </div>
                      <p className="mono-text">
                        <strong>Evidence Ref:</strong> {finding.evidenceReference}
                      </p>
                      <p>{finding.evidence}</p>
                      {finding.taskId ? (
                        <p>
                          <strong>Task:</strong> {finding.taskId}
                        </p>
                      ) : null}
                    </li>
                  ))}
                </ol>
              )}
            </section>

            <section className="card-inner">
              <h3>Task Execution Results</h3>
              {investigatorReport.report.taskResults.length === 0 ? (
                <p>No task execution results were recorded.</p>
              ) : (
                <ul className="visual-list">
                  {investigatorReport.report.taskResults.map((result) => (
                    <li key={result.taskId} className="visual-item">
                      <strong>{result.taskId}</strong>
                      <div className="meta-chip-row">
                        <span className="meta-chip">{result.taskType}</span>
                        <span className="meta-chip">{result.status}</span>
                        <span className="meta-chip">Tools: {result.toolsUsed.length > 0 ? result.toolsUsed.join(', ') : 'None'}</span>
                      </div>
                      <p>{result.summary}</p>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            {investigatorReport.report.attackTechniques.length > 0 ? (
              <div className="card-inner">
                <h3>Mapped ATT&CK Techniques</h3>
                <ul className="missing-info-list">
                  {investigatorReport.report.attackTechniques.map((technique) => (
                    <li key={technique.techniqueId}>
                      <strong>{technique.techniqueId} - {technique.name}</strong>
                      <p>{technique.basis}</p>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}

            {investigatorReport.report.possibleImpact.length > 0 ? (
              <div className="card-inner">
                <h3>Possible Impact</h3>
                <ul>
                  {investigatorReport.report.possibleImpact.map((impact) => (
                    <li key={impact}>{impact}</li>
                  ))}
                </ul>
              </div>
            ) : null}

            {investigatorReport.report.uncertainties.length > 0 ? (
              <div className="card-inner">
                <h3>Uncertainties</h3>
                <ul>
                  {investigatorReport.report.uncertainties.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </div>
            ) : null}
          </>
        ) : null}
      </article>

      <article className="card space-stack">
        <h2>Response &amp; Learning</h2>
        <p>Action plan and training generated from validated findings.</p>
        <div className="row-between wrap-gap">
          <span>
            Response &amp; Learning status: {renderResponseStatus(responsePackage?.status ?? 'NotStarted', generatingResponsePackage)}
          </span>
          {responsePackage?.completedAt ? <span>Completed at {renderDate(responsePackage.completedAt)}</span> : null}
        </div>
        <button
          type="button"
          className="button-primary"
          disabled={generatingResponsePackage || !investigatorReport?.report || investigatorReport.status !== 'Completed'}
          onClick={() => void handleGenerateResponsePackage()}
        >
          {generatingResponsePackage ? 'Generating Response & Learning Plan...' : 'Generate Response & Learning Plan'}
        </button>
        {!investigatorReport?.report ? <p>Run investigator before generating response and learning guidance.</p> : null}
        {responseError ? <ErrorBanner message={responseError} /> : null}

        {responsePackage?.package ? (
          <>
            <div className="card-inner">
              <h3>What ARGUS Found</h3>
              <p>{responsePackage.package.plainLanguageSummary}</p>
              <p>
                <strong>Classification:</strong> {responsePackage.package.incidentClassification}
              </p>
              <p>
                <strong>Overall Priority:</strong> {responsePackage.package.overallPriority}
              </p>
            </div>

            <section className="card-inner">
              <h3>Immediate Actions</h3>
              {responsePackage.package.immediateActions.length === 0 ? (
                <p>No immediate actions were recommended.</p>
              ) : (
                <ol className="visual-list">
                  {responsePackage.package.immediateActions
                    .slice()
                    .sort((left, right) => left.priority - right.priority)
                    .map((action) => (
                      <li key={action.id} className="visual-item">
                        <strong>{action.title}</strong>
                        <p>{action.description}</p>
                        <div className="meta-chip-row">
                          <span className="meta-chip">Priority {action.priority}</span>
                          <span className="meta-chip">{renderActionType(action.actionType)}</span>
                          <span className="meta-chip">Approval: {action.requiresHumanApproval ? 'Yes' : 'No'}</span>
                        </div>
                        <p>
                          <strong>Why:</strong> {action.reason}
                        </p>
                        {action.responsibleRole ? (
                          <p>
                            <strong>Responsible Role:</strong> {action.responsibleRole}
                          </p>
                        ) : null}
                      </li>
                    ))}
                </ol>
              )}
            </section>

            <section className="card-inner">
              <h3>Recovery Actions</h3>
              {responsePackage.package.recoveryActions.length === 0 ? (
                <p>No recovery actions were recommended.</p>
              ) : (
                <ol className="visual-list">
                  {responsePackage.package.recoveryActions
                    .slice()
                    .sort((left, right) => left.priority - right.priority)
                    .map((action) => (
                      <li key={action.id} className="visual-item">
                        <strong>{action.title}</strong>
                        <p>{action.description}</p>
                        <div className="meta-chip-row">
                          <span className="meta-chip">Priority {action.priority}</span>
                          <span className="meta-chip">{renderActionType(action.actionType)}</span>
                          <span className="meta-chip">Approval: {action.requiresHumanApproval ? 'Yes' : 'No'}</span>
                        </div>
                        <p>
                          <strong>Why:</strong> {action.reason}
                        </p>
                      </li>
                    ))}
                </ol>
              )}
            </section>

            <section className="card-inner">
              <h3>Prevention Actions</h3>
              {responsePackage.package.preventionActions.length === 0 ? (
                <p>No prevention actions were recommended.</p>
              ) : (
                <ol className="visual-list">
                  {responsePackage.package.preventionActions
                    .slice()
                    .sort((left, right) => left.priority - right.priority)
                    .map((action) => (
                      <li key={action.id} className="visual-item">
                        <strong>{action.title}</strong>
                        <p>{action.description}</p>
                        <div className="meta-chip-row">
                          <span className="meta-chip">Priority {action.priority}</span>
                          <span className="meta-chip">{renderActionType(action.actionType)}</span>
                          <span className="meta-chip">Approval: {action.requiresHumanApproval ? 'Yes' : 'No'}</span>
                        </div>
                        <p>
                          <strong>Why:</strong> {action.reason}
                        </p>
                      </li>
                    ))}
                </ol>
              )}
            </section>

            <div className="card-inner">
              <h3>When to get additional help</h3>
              {responsePackage.package.escalationRecommendations.length === 0 ? (
                <p>No escalation guidance was recommended.</p>
              ) : (
                <ul className="missing-info-list">
                  {responsePackage.package.escalationRecommendations.map((recommendation) => (
                    <li key={`${recommendation.level}-${recommendation.recommendedContact}`}>
                      <strong>{recommendation.level}</strong>
                      <p>{recommendation.reason}</p>
                      <p>
                        <strong>Recommended Contact:</strong> {recommendation.recommendedContact}
                      </p>
                      <p>
                        <strong>Urgent:</strong> {recommendation.urgent ? 'Yes' : 'No'}
                      </p>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <div className="card-inner">
              <h3>Learn From This Incident</h3>
              <p>
                <strong>{responsePackage.package.education.title}</strong>
              </p>
              <p>
                <strong>Learning Objective:</strong> {responsePackage.package.education.learningObjective}
              </p>
              <p>
                <strong>Audience Level:</strong> {responsePackage.package.education.audienceLevel}
              </p>
              <p>
                <strong>Estimated Time:</strong> {responsePackage.package.education.estimatedMinutes} minutes
              </p>
              <p>{responsePackage.package.education.explanation}</p>

              <h4>Warning Signs</h4>
              <ul className="missing-info-list">
                {responsePackage.package.education.warningSigns.map((warningSign) => (
                  <li key={warningSign.title}>
                    <strong>{warningSign.title}</strong>
                    <p>{warningSign.explanation}</p>
                  </li>
                ))}
              </ul>

              <h4>Questions</h4>
              <div className="space-stack">
                {responsePackage.package.education.questions.map((question) => {
                  const selectedAnswer = selectedQuizAnswers[question.id];
                  const wasSubmitted = submittedQuizAnswers[question.id] ?? false;
                  const isCorrect = wasSubmitted && selectedAnswer === question.correctOptionIndex;

                  return (
                    <fieldset key={question.id} className="card-inner">
                      <legend>
                        <strong>{question.question}</strong>
                      </legend>
                      <div className="space-stack">
                        {question.options.map((option, optionIndex) => (
                          <label key={`${question.id}-${optionIndex}`}>
                            <input
                              type="radio"
                              name={question.id}
                              value={optionIndex}
                              checked={selectedAnswer === optionIndex}
                              onChange={() => handleQuizSelection(question.id, optionIndex)}
                            />{' '}
                            {option}
                          </label>
                        ))}
                      </div>
                      <button
                        type="button"
                        className="button-primary"
                        disabled={selectedAnswer === undefined}
                        onClick={() => handleQuizSubmit(question.id)}
                      >
                        Check Answer
                      </button>
                      {wasSubmitted ? (
                        <p>
                          <strong>{isCorrect ? 'Correct.' : 'Incorrect.'}</strong> {question.explanation}
                        </p>
                      ) : null}
                    </fieldset>
                  );
                })}
              </div>

              {responsePackage.package.education.takeaways.length > 0 ? (
                <>
                  <h4>Key Takeaways</h4>
                  <ul>
                    {responsePackage.package.education.takeaways.map((takeaway) => (
                      <li key={takeaway}>{takeaway}</li>
                    ))}
                  </ul>
                </>
              ) : null}
            </div>

            {responsePackage.package.assumptions.length > 0 ? (
              <div className="card-inner">
                <h3>Assumptions</h3>
                <ul>
                  {responsePackage.package.assumptions.map((assumption) => (
                    <li key={assumption}>{assumption}</li>
                  ))}
                </ul>
              </div>
            ) : null}

            {responsePackage.package.limitations.length > 0 ? (
              <div className="card-inner">
                <h3>Limitations</h3>
                <ul>
                  {responsePackage.package.limitations.map((limitation) => (
                    <li key={limitation}>{limitation}</li>
                  ))}
                </ul>
              </div>
            ) : null}
          </>
        ) : null}
      </article>
    </section>
  );
}
