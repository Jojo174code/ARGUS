import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
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
} from '../api/incidents';
import type {
  AgenticWorkflowResultResponse,
  AnalysisResult,
  CoordinatorPlanResponse,
  Incident,
  InvestigatorReportResponse,
  ResponseEducationPackageResponse,
} from '../api/types';
import { ErrorBanner } from '../components/ErrorBanner';
import { IncidentOverviewCharts } from '../components/IncidentOverviewCharts';
import { IncidentWorkspaceNav } from '../components/IncidentWorkspaceNav';
import { LoadingBlock } from '../components/LoadingBlock';
import { WorkflowActionProgress } from '../components/WorkflowActionProgress';
import { WorkflowTimeline } from '../components/WorkflowTimeline';

function stageStatus(label: string, done: boolean): string {
  return done ? `${label}: done` : `${label}: next`;
}

function isTerminalWorkflowStatus(status: AgenticWorkflowResultResponse['status']): boolean {
  return status === 'Completed' || status === 'Failed' || status === 'AwaitingInformation';
}

export function IncidentGuidePage() {
  const { id } = useParams<{ id: string }>();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [analysis, setAnalysis] = useState<AnalysisResult | null>(null);
  const [plan, setPlan] = useState<CoordinatorPlanResponse | null>(null);
  const [report, setReport] = useState<InvestigatorReportResponse | null>(null);
  const [responsePack, setResponsePack] = useState<ResponseEducationPackageResponse | null>(null);
  const [workflow, setWorkflow] = useState<AgenticWorkflowResultResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [isWorkflowPolling, setIsWorkflowPolling] = useState(false);

  const hasEvidence = (incident?.evidenceItems.length ?? 0) > 0;

  const cards = useMemo(() => {
    return [
      {
        key: 'analysis',
        title: '1. Check The Email',
        agent: 'ARGUS Analysis',
        tone: 'analysis',
        description: 'Run a safe automated check to detect risk signals.',
        ready: hasEvidence,
        done: Boolean(analysis),
        actionLabel: analysis ? 'Re-run Check' : 'Run Check',
      },
      {
        key: 'plan',
        title: '2. Build Investigation Plan',
        agent: 'Coordinator Agent',
        tone: 'coordinator',
        description: 'Create a step-by-step plan in plain language.',
        ready: Boolean(analysis),
        done: plan?.status === 'Completed',
        actionLabel: plan?.status === 'Completed' ? 'Rebuild Plan' : 'Build Plan',
      },
      {
        key: 'investigator',
        title: '3. Generate Findings',
        agent: 'Investigator Agent',
        tone: 'investigator',
        description: 'Turn plan output into human-friendly findings.',
        ready: plan?.status === 'Completed' && Boolean(plan.plan?.readyForInvestigation),
        done: report?.status === 'Completed',
        actionLabel: report?.status === 'Completed' ? 'Refresh Findings' : 'Generate Findings',
      },
      {
        key: 'response',
        title: '4. Create Action Plan',
        agent: 'Response & Education',
        tone: 'response',
        description: 'Produce immediate actions and staff guidance.',
        ready: report?.status === 'Completed',
        done: responsePack?.status === 'Completed',
        actionLabel: responsePack?.status === 'Completed' ? 'Refresh Action Plan' : 'Create Action Plan',
      },
    ];
  }, [analysis, hasEvidence, plan, report, responsePack]);

  useEffect(() => {
    if (!id) {
      setError('Missing incident id.');
      setLoading(false);
      return;
    }
    const incidentId = id;

    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);
      try {
        const nextIncident = await getIncident(incidentId);
        if (cancelled) {
          return;
        }
        setIncident(nextIncident);

        try {
          setAnalysis(await getIncidentAnalysis(incidentId));
        } catch {
          setAnalysis(null);
        }

        try {
          setPlan(await getCoordinatorPlan(incidentId));
        } catch {
          setPlan(null);
        }

        try {
          setReport(await getInvestigatorReport(incidentId));
        } catch {
          setReport(null);
        }

        try {
          setResponsePack(await getResponseEducationPackage(incidentId));
        } catch {
          setResponsePack(null);
        }

        try {
          setWorkflow(await getWorkflow(incidentId));
        } catch {
          setWorkflow(null);
        }
      } catch (loadError) {
        if (!cancelled) {
          setError(loadError instanceof Error ? loadError.message : 'Failed to load incident workspace.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [id]);

  useEffect(() => {
    if (!id || !isWorkflowPolling) {
      return;
    }

    const incidentId = id;
    let cancelled = false;
    let requestInFlight = false;

    async function pollWorkflow(): Promise<void> {
      if (requestInFlight) {
        return;
      }

      requestInFlight = true;
      try {
        const nextWorkflow = await getWorkflow(incidentId);
        if (cancelled) {
          return;
        }

        setWorkflow(nextWorkflow);
        if (isTerminalWorkflowStatus(nextWorkflow.status)) {
          setIsWorkflowPolling(false);
        }
      } catch (pollError) {
        if (cancelled) {
          return;
        }

        if (pollError instanceof Error && 'status' in pollError && (pollError as { status?: number }).status === 404) {
          return;
        }

        setError(pollError instanceof Error ? pollError.message : 'Failed to refresh workflow status.');
      } finally {
        requestInFlight = false;
      }
    }

    void pollWorkflow();
    const intervalId = window.setInterval(() => void pollWorkflow(), 1500);
    return () => {
      cancelled = true;
      window.clearInterval(intervalId);
    };
  }, [id, isWorkflowPolling]);

  async function runAction(action: 'analysis' | 'plan' | 'investigator' | 'response' | 'workflow') {
    if (!id) {
      return;
    }
    const incidentId = id;

    setBusyAction(action);
    setError(null);

    if (action === 'workflow') {
      setWorkflow(null);
      setIsWorkflowPolling(true);
    }

    try {
      if (action === 'analysis') {
        setAnalysis(await analyzeIncident(incidentId));
      }
      if (action === 'plan') {
        setPlan(await generateCoordinatorPlan(incidentId));
      }
      if (action === 'investigator') {
        setReport(await runInvestigator(incidentId));
      }
      if (action === 'response') {
        setResponsePack(await generateResponseEducationPackage(incidentId));
      }
      if (action === 'workflow') {
        const nextWorkflow = await runFullWorkflow(incidentId);
        setWorkflow(nextWorkflow);
        if (isTerminalWorkflowStatus(nextWorkflow.status)) {
          setIsWorkflowPolling(false);
        }
        try {
          setAnalysis(await getIncidentAnalysis(incidentId));
        } catch {
          setAnalysis(null);
        }
        try {
          setPlan(await getCoordinatorPlan(incidentId));
        } catch {
          setPlan(null);
        }
        try {
          setReport(await getInvestigatorReport(incidentId));
        } catch {
          setReport(null);
        }
        try {
          setResponsePack(await getResponseEducationPackage(incidentId));
        } catch {
          setResponsePack(null);
        }
      }

      setIncident(await getIncident(incidentId));
      try {
        setWorkflow(await getWorkflow(incidentId));
      } catch {
        setWorkflow(null);
      }
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'Action failed.');
      if (action === 'workflow') {
        setIsWorkflowPolling(false);
      }
    } finally {
      setBusyAction(null);
    }
  }

  if (loading) {
    return <LoadingBlock message="Loading your guided incident workspace..." />;
  }

  if (!incident) {
    return (
      <section className="card">
        <h1>Incident Not Found</h1>
        <p>We could not find this incident.</p>
      </section>
    );
  }

  const completionCount = cards.filter((card) => card.done).length;

  return (
    <section className="space-stack">
      <IncidentWorkspaceNav incidentId={incident.id} />

      <article className="card mission-strip guide-command-center">
        <div className="row-between wrap-gap">
          <div>
            <h1>Incident Guide</h1>
            <p className="soft-label">Follow these 4 steps. Each button runs one safe part of the workflow.</p>
          </div>
          <div className="metric-tile">
            <span className="metric-label">Progress</span>
            <strong className="metric-value">{completionCount}/4</strong>
          </div>
        </div>

        <div className="mission-metrics">
          <div className="metric-tile">
            <span className="metric-label">Evidence Files</span>
            <strong className="metric-value">{incident.evidenceItems.length}</strong>
          </div>
          <div className="metric-tile">
            <span className="metric-label">Risk Level</span>
            <strong className={`metric-value risk-value risk-value-${analysis?.riskLevel?.toLowerCase() ?? 'unknown'}`}>{analysis?.riskLevel ?? 'Not checked yet'}</strong>
          </div>
          <div className="metric-tile">
            <span className="metric-label">Findings</span>
            <strong className="metric-value">{report?.report?.findings.length ?? 0}</strong>
          </div>
          <div className="metric-tile">
            <span className="metric-label">Action Items</span>
            <strong className="metric-value">
              {responsePack?.package
                ? responsePack.package.immediateActions.length + responsePack.package.recoveryActions.length + responsePack.package.preventionActions.length
                : 0}
            </strong>
          </div>
        </div>
      </article>

      {error ? <ErrorBanner message={error} /> : null}

      <WorkflowTimeline workflow={workflow} isActive={isWorkflowPolling} />

      <article className="card space-stack">
        <div className="row-between wrap-gap">
          <h2>Step-by-Step Controls</h2>
          <span className="soft-label">Plain language status</span>
        </div>
        <div className="step-card-grid">
          {cards.map((card) => (
            <section key={card.key} className={`step-card agent-card agent-card-${card.tone}${card.done ? ' is-complete' : ''}${busyAction === card.key ? ' is-active' : ''}`}>
              <div className="agent-card-heading">
                <span className="agent-mark" aria-hidden="true" />
                <p className="agent-label">{card.agent}</p>
              </div>
              <h3>{card.title}</h3>
              <p>{card.description}</p>
              <p className="soft-label">{stageStatus(card.title, card.done)}</p>
              <button
                type="button"
                className="button-primary"
                disabled={!card.ready || busyAction !== null}
                onClick={() => void runAction(card.key as 'analysis' | 'plan' | 'investigator' | 'response')}
              >
                {busyAction === card.key ? 'Working...' : card.actionLabel}
              </button>
              {!card.ready ? <p className="soft-label">Complete the previous step first.</p> : null}
            </section>
          ))}
        </div>
        {busyAction && busyAction !== 'workflow' ? <WorkflowActionProgress action={busyAction as 'analysis' | 'plan' | 'investigator' | 'response'} /> : null}
      </article>

      <article className="card space-stack">
        <div className="row-between wrap-gap">
          <h2>One-Click Full Run</h2>
          <span className="soft-label">Runs all available steps in order</span>
        </div>
        <button
          type="button"
          className="button-primary"
          disabled={!hasEvidence || busyAction !== null}
          onClick={() => void runAction('workflow')}
        >
          {busyAction === 'workflow' ? 'Running Full Workflow...' : 'Run Full Workflow'}
        </button>
        {!hasEvidence ? <p className="soft-label">Upload at least one email file first on the Evidence page.</p> : null}
      </article>

      <IncidentOverviewCharts
        analysis={analysis}
        workflow={workflow}
        investigatorReport={report}
        responsePackage={responsePack}
      />
    </section>
  );
}
