import type { AnalysisResult, InvestigatorReportResponse, ResponseEducationPackageResponse } from '../api/types';

interface IncidentExecutiveSummaryProps {
  analysis: AnalysisResult | null;
  report: InvestigatorReportResponse | null;
  responsePack: ResponseEducationPackageResponse | null;
}

export function IncidentExecutiveSummary({ analysis, report, responsePack }: IncidentExecutiveSummaryProps) {
  const packageData = responsePack?.package;
  const classification = packageData?.incidentClassification ?? report?.report?.classification ?? 'Investigation in progress';
  const risk = packageData?.overallPriority ?? report?.report?.severity ?? analysis?.riskLevel ?? 'Not assessed';
  const riskTone = risk.toLowerCase().replace(/\s/g, '-');
  const confidence = report?.report ? `${Math.round(report.report.confidence * 100)}%` : 'Not available';
  const findingCount = report?.report?.findings.length ?? 0;
  const summary = packageData?.plainLanguageSummary ?? analysis?.summary ?? 'ARGUS has not completed an investigation summary yet. Run the available Guide steps to continue.';
  const immediateActions = packageData?.immediateActions.slice(0, 3) ?? [];

  return (
    <article className="card executive-summary" aria-labelledby="argus-found-title">
      <p className="workflow-progress-kicker">What ARGUS Found</p>
      <h2 id="argus-found-title">{classification}</h2>
      <p className="executive-summary-copy">{summary}</p>
      <div className="executive-metrics">
        <div className={`executive-risk executive-risk-${riskTone}`}><span>Risk</span><strong>{risk}</strong><i aria-hidden="true"><b /></i></div>
        <div><span>Confidence</span><strong>{confidence}</strong></div>
        <div><span>Important Findings</span><strong>{findingCount}</strong></div>
      </div>
      {immediateActions.length > 0 ? (
        <div className="executive-actions">
          <h3>What You Should Do</h3>
          <ol>
            {immediateActions.map((action) => <li key={action.id}>{action.title}</li>)}
          </ol>
        </div>
      ) : null}
      {findingCount > 0 ? <a className="text-button executive-findings-link" href="#top-findings">View Detailed Findings</a> : null}
      {!packageData && analysis ? <p className="soft-label">This is an early assessment. Complete the investigation for tailored action steps.</p> : null}
    </article>
  );
}