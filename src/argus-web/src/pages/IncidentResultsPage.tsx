import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  getIncident,
  getIncidentAnalysis,
  getInvestigatorReport,
  getResponseEducationPackage,
  getWorkflow,
} from '../api/incidents';
import type {
  AgenticWorkflowResultResponse,
  AnalysisResult,
  Incident,
  InvestigatorReportResponse,
  ResponseEducationPackageResponse,
} from '../api/types';
import { ErrorBanner } from '../components/ErrorBanner';
import { IncidentOverviewCharts } from '../components/IncidentOverviewCharts';
import { IncidentWorkspaceNav } from '../components/IncidentWorkspaceNav';
import { LoadingBlock } from '../components/LoadingBlock';
import { IncidentExecutiveSummary } from '../components/IncidentExecutiveSummary';

export function IncidentResultsPage() {
  const { id } = useParams<{ id: string }>();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [analysis, setAnalysis] = useState<AnalysisResult | null>(null);
  const [report, setReport] = useState<InvestigatorReportResponse | null>(null);
  const [responsePack, setResponsePack] = useState<ResponseEducationPackageResponse | null>(null);
  const [workflow, setWorkflow] = useState<AgenticWorkflowResultResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
        setIncident(await getIncident(incidentId));

        try {
          setAnalysis(await getIncidentAnalysis(incidentId));
        } catch {
          setAnalysis(null);
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
          setError(loadError instanceof Error ? loadError.message : 'Failed to load results.');
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

  if (loading) {
    return <LoadingBlock message="Loading visual results..." />;
  }

  if (!incident) {
    return (
      <section className="card">
        <h1>Incident Not Found</h1>
      </section>
    );
  }

  return (
    <section className="space-stack results-workspace">
      <IncidentWorkspaceNav incidentId={incident.id} />

      <article className="card mission-strip results-hero">
        <h1>Results Dashboard</h1>
        <p className="soft-label">Non-technical summary of risk, findings, and recommended actions.</p>
      </article>

      {error ? <ErrorBanner message={error} /> : null}

      <IncidentExecutiveSummary analysis={analysis} report={report} responsePack={responsePack} />

      <IncidentOverviewCharts
        analysis={analysis}
        workflow={workflow}
        investigatorReport={report}
        responsePackage={responsePack}
      />

      <article id="top-findings" className="card space-stack">
        <h2>Top Findings</h2>
        {report?.report?.findings?.length ? (
          <ul className="visual-list">
            {report.report.findings.slice(0, 5).map((finding) => (
              <li key={finding.id} className="visual-item">
                <strong>{finding.title}</strong>
                <p>{finding.description}</p>
                <div className="meta-chip-row">
                  <span className="meta-chip">{finding.severity}</span>
                  <span className="meta-chip">Confidence {Math.round(finding.confidence * 100)}%</span>
                </div>
              </li>
            ))}
          </ul>
        ) : (
          <p>No findings yet. Use the Guide page to run steps.</p>
        )}
      </article>

      <article className="card space-stack">
        <h2>Recommended Actions</h2>
        {responsePack?.package ? (
          <>
            <h3>Immediate</h3>
            {responsePack.package.immediateActions.length > 0 ? (
              <ul className="visual-list">
                {responsePack.package.immediateActions.slice(0, 4).map((action) => (
                  <li key={action.id} className="visual-item">
                    <strong>{action.title}</strong>
                    <p>{action.description}</p>
                    <div className="meta-chip-row">
                      <span className="meta-chip">Priority {action.priority}</span>
                      <span className="meta-chip">{action.actionType}</span>
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <p>No immediate actions listed.</p>
            )}
          </>
        ) : (
          <p>No action plan yet. Use the Guide page to create one.</p>
        )}
      </article>
    </section>
  );
}
