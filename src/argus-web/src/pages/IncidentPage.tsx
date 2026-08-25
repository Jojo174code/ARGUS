import { useEffect, useMemo, useState } from 'react';
import type { ChangeEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  analyzeIncident,
  generateCoordinatorPlan,
  getCoordinatorPlan,
  getIncident,
  getIncidentAnalysis,
  getInvestigatorReport,
  runInvestigator,
  uploadEmailEvidence,
} from '../api/incidents';
import type { AnalysisResult, CoordinatorPlanResponse, Incident, InvestigatorReportResponse } from '../api/types';
import { EmailSummary } from '../components/EmailSummary';
import { ErrorBanner } from '../components/ErrorBanner';
import { IndicatorsList } from '../components/IndicatorsList';
import { LoadingBlock } from '../components/LoadingBlock';
import { MitreMappings } from '../components/MitreMappings';
import { RiskBadge } from '../components/RiskBadge';
import { StatusBadge } from '../components/StatusBadge';
import { UrlTable } from '../components/UrlTable';

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

export function IncidentPage() {
  const { id } = useParams<{ id: string }>();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [analysis, setAnalysis] = useState<AnalysisResult | null>(null);
  const [coordinatorPlan, setCoordinatorPlan] = useState<CoordinatorPlanResponse | null>(null);
  const [investigatorReport, setInvestigatorReport] = useState<InvestigatorReportResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [coordinatorError, setCoordinatorError] = useState<string | null>(null);
  const [investigatorError, setInvestigatorError] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [analyzing, setAnalyzing] = useState(false);
  const [generatingPlan, setGeneratingPlan] = useState(false);
  const [runningInvestigator, setRunningInvestigator] = useState(false);

  const hasEvidence = useMemo(() => (incident?.evidenceItems.length ?? 0) > 0, [incident]);

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
      setCoordinatorError(null);
      setInvestigatorError(null);
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
      setCoordinatorError(null);
      setInvestigatorError(null);
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
      setInvestigatorError(null);
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
    } catch (runError) {
      setInvestigatorError(runError instanceof Error ? runError.message : 'Investigator run failed.');
    } finally {
      setRunningInvestigator(false);
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
        <p>
          The coordinator uses deterministic findings and incident context to propose the smallest useful investigation plan.
        </p>
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
            <div className="card-inner">
              <h3>Investigation Tasks</h3>
              <ol className="investigation-list">
                {[...coordinatorPlan.plan.tasks]
                  .sort((left, right) => left.priority - right.priority)
                  .map((task) => (
                    <li key={task.id}>
                      <strong>{task.title}</strong>
                      <p>{task.description}</p>
                      <p>
                        <strong>Priority:</strong> {task.priority}
                      </p>
                      <p>
                        <strong>Required Evidence:</strong> {task.requiredEvidence.length > 0 ? task.requiredEvidence.join(', ') : 'None'}
                      </p>
                      <p>
                        <strong>Completion Condition:</strong> {task.completionCondition}
                      </p>
                    </li>
                  ))}
              </ol>
            </div>

            <div className="card-inner">
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
            </div>

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
        <p>
          The investigator executes only deterministic tools against coordinator tasks and synthesizes an evidence-grounded report.
        </p>
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
              <p>
                <strong>Classification:</strong> {investigatorReport.report.classification}
              </p>
              <p>
                <strong>Severity:</strong> {investigatorReport.report.severity}
              </p>
              <p>
                <strong>Confidence:</strong> {Math.round(investigatorReport.report.confidence * 100)}%
              </p>
            </div>

            <div className="card-inner">
              <h3>Findings</h3>
              {investigatorReport.report.findings.length === 0 ? (
                <p>No findings reported.</p>
              ) : (
                <ol className="investigation-list">
                  {investigatorReport.report.findings.map((finding) => (
                    <li key={finding.id}>
                      <strong>{finding.title}</strong>
                      <p>{finding.description}</p>
                      <p>
                        <strong>Severity:</strong> {finding.severity} | <strong>Confidence:</strong> {Math.round(finding.confidence * 100)}%
                      </p>
                      <p>
                        <strong>Evidence Source:</strong> {finding.evidenceSource}
                      </p>
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
            </div>

            <div className="card-inner">
              <h3>Task Execution Results</h3>
              {investigatorReport.report.taskResults.length === 0 ? (
                <p>No task execution results were recorded.</p>
              ) : (
                <ul className="missing-info-list">
                  {investigatorReport.report.taskResults.map((result) => (
                    <li key={result.taskId}>
                      <strong>{result.taskId}</strong>
                      <p>
                        <strong>Type:</strong> {result.taskType}
                      </p>
                      <p>
                        <strong>Status:</strong> {result.status}
                      </p>
                      <p>
                        <strong>Tools:</strong> {result.toolsUsed.length > 0 ? result.toolsUsed.join(', ') : 'None'}
                      </p>
                      <p>{result.summary}</p>
                    </li>
                  ))}
                </ul>
              )}
            </div>

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
    </section>
  );
}
