import { useMemo } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type {
  AgenticWorkflowResultResponse,
  AnalysisResult,
  InvestigatorReportResponse,
  ResponseEducationPackageResponse,
  RiskLevel,
  WorkflowRunStatus,
} from '../api/types';
import { IncidentMap } from './IncidentMap';

interface IncidentOverviewChartsProps {
  analysis: AnalysisResult | null;
  workflow: AgenticWorkflowResultResponse | null;
  investigatorReport: InvestigatorReportResponse | null;
  responsePackage: ResponseEducationPackageResponse | null;
}

const statusColors: Record<WorkflowRunStatus, string> = {
  Pending: '#7d6c6e',
  Running: '#9f3140',
  Completed: '#bd1e2d',
  Failed: '#590b14',
  AwaitingInformation: '#9c5962',
};

const riskColors: Record<RiskLevel, string> = {
  Low: '#9c5962',
  Moderate: '#c54c5a',
  High: '#a31326',
  Critical: '#590b14',
};

const actionTypeColors: Record<string, string> = {
  Informational: '#7d6c6e',
  UserAction: '#bd1e2d',
  AdministratorAction: '#760d18',
  ProfessionalEscalation: '#3b2023',
};

function formatStatus(status: WorkflowRunStatus): string {
  if (status === 'AwaitingInformation') {
    return 'Awaiting Info';
  }

  return status;
}

export function IncidentOverviewCharts({
  analysis,
  workflow,
  investigatorReport,
  responsePackage,
}: IncidentOverviewChartsProps) {
  const stageStatusData = useMemo(() => {
    if (!workflow) {
      return [] as Array<{ name: WorkflowRunStatus; value: number }>;
    }

    const counts = workflow.stages.reduce<Record<WorkflowRunStatus, number>>((acc, stage) => {
      acc[stage.status] = (acc[stage.status] ?? 0) + 1;
      return acc;
    }, {
      Pending: 0,
      Running: 0,
      Completed: 0,
      Failed: 0,
      AwaitingInformation: 0,
    });

    return (Object.entries(counts) as Array<[WorkflowRunStatus, number]>)
      .filter(([, count]) => count > 0)
      .map(([status, count]) => ({ name: status, value: count }));
  }, [workflow]);

  const riskSignalData = useMemo(() => {
    if (!analysis) {
      return [] as Array<{ name: RiskLevel; count: number }>;
    }

    const counts = analysis.indicators.reduce<Record<RiskLevel, number>>((acc, indicator) => {
      acc[indicator.severity] = (acc[indicator.severity] ?? 0) + 1;
      return acc;
    }, {
      Low: 0,
      Moderate: 0,
      High: 0,
      Critical: 0,
    });

    return (Object.entries(counts) as Array<[RiskLevel, number]>)
      .filter(([, count]) => count > 0)
      .map(([severity, count]) => ({ name: severity, count }));
  }, [analysis]);

  const responseActionData = useMemo(() => {
    if (!responsePackage?.package) {
      return [] as Array<{ name: string; count: number }>;
    }

    const allActions = [
      ...responsePackage.package.immediateActions,
      ...responsePackage.package.recoveryActions,
      ...responsePackage.package.preventionActions,
    ];

    const counts = allActions.reduce<Record<string, number>>((acc, action) => {
      acc[action.actionType] = (acc[action.actionType] ?? 0) + 1;
      return acc;
    }, {});

    return Object.entries(counts).map(([name, count]) => ({ name, count }));
  }, [responsePackage]);

  return (
    <article className="card space-stack">
      <div className="row-between wrap-gap">
        <h2>Incident Visual Overview</h2>
        <span className="soft-label">Live summary of where this case stands</span>
      </div>

      <IncidentMap
        analysis={analysis}
        investigatorReport={investigatorReport}
        responsePackage={responsePackage}
      />

      <div className="overview-grid">
        <section className="chart-panel">
          <h3>Workflow Stage Status</h3>
          {stageStatusData.length === 0 ? (
            <p>No workflow run yet.</p>
          ) : (
            <div className="chart-box">
              <ResponsiveContainer width="100%" height={220}>
                <PieChart>
                  <Pie data={stageStatusData} dataKey="value" nameKey="name" innerRadius={52} outerRadius={86}>
                    {stageStatusData.map((entry) => (
                      <Cell key={entry.name} fill={statusColors[entry.name]} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
              <ul className="legend-list">
                {stageStatusData.map((entry) => (
                  <li key={entry.name}>
                    <span className="legend-dot" style={{ backgroundColor: statusColors[entry.name] }} />
                    {formatStatus(entry.name)}: {entry.value}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </section>

        <section className="chart-panel">
          <h3>Risk Signal Breakdown</h3>
          {riskSignalData.length === 0 ? (
            <p>Run deterministic analysis to see risk signal distribution.</p>
          ) : (
            <div className="chart-box">
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={riskSignalData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#eadbdd" />
                  <XAxis dataKey="name" tickLine={false} axisLine={false} />
                  <YAxis allowDecimals={false} tickLine={false} axisLine={false} />
                  <Tooltip />
                  <Bar dataKey="count" radius={[8, 8, 0, 0]}>
                    {riskSignalData.map((entry) => (
                      <Cell key={entry.name} fill={riskColors[entry.name]} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>

        <section className="chart-panel">
          <h3>Response Action Mix</h3>
          {responseActionData.length === 0 ? (
            <p>Generate Response & Learning to visualize recommended action types.</p>
          ) : (
            <div className="chart-box">
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={responseActionData} layout="vertical" margin={{ left: 12, right: 12 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#eadbdd" />
                  <XAxis type="number" allowDecimals={false} tickLine={false} axisLine={false} />
                  <YAxis dataKey="name" type="category" width={130} tickLine={false} axisLine={false} />
                  <Tooltip />
                  <Bar dataKey="count" radius={[0, 8, 8, 0]}>
                    {responseActionData.map((entry) => (
                      <Cell key={entry.name} fill={actionTypeColors[entry.name] ?? '#7d6c6e'} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>
      </div>
    </article>
  );
}
