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

function stageStatus(label: string, done: boolean): string {
  return done ? `${label}: done` : `${label}: next`;
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

  const hasEvidence = (incident?.evidenceItems.length ?? 0) > 0;

  const cards = useMemo(() => {
    return [
      {
        key: 'analysis',
        title: '1. Check The Email',
        description: 'Run a safe automated check to detect risk signals.',
        ready: hasEvidence,
        done: Boolean(analysis),
        actionLabel: analysis ? 'Re-run Check' : 'Run Check',
      },
      {
        key: 'plan',
        title: '2. Build Investigation Plan',
        description: 'Create a step-by-step plan in plain language.',
        ready: Boolean(analysis),
        done: plan?.status === 'Completed',
        actionLabel: plan?.status === 'Completed' ? 'Rebuild Plan' : 'Build Plan',
      },
      {
        key: 'investigator',
        title: '3. Generate Findings',
        description: 'Turn plan output into human-friendly findings.',
        ready: plan?.status === 'Completed' && Boolean(plan.plan?.readyForInvestigation),
        done: report?.status === 'Completed',
        actionLabel: report?.status === 'Completed' ? 'Refresh Findings' : 'Generate Findings',
      },
      {
        key: 'response',
        title: '4. Create Action Plan',
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

  async function runAction(action: 'analysis' | 'plan' | 'investigator' | 'response' | 'workflow') {
    if (!id) {
      return;
    }
    const incidentId = id;

    setBusyAction(action);
    setError(null);

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
        setWorkflow(await runFullWorkflow(incidentId));
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

      <article className="card mission-strip">
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
            <strong className="metric-value">{analysis?.riskLevel ?? 'Not checked yet'}</strong>
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

      <article className="card space-stack">
        <div className="row-between wrap-gap">
          <h2>Step-by-Step Controls</h2>
          <span className="soft-label">Plain language status</span>
        </div>
        <div className="step-card-grid">
          {cards.map((card) => (
            <section key={card.key} className="step-card">
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
