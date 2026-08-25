import type { PhishingRuleResult } from '../api/types';

export function IndicatorsList({ indicators }: { indicators: PhishingRuleResult[] }) {
  return (
    <section className="card">
      <h2>Triggered Indicators</h2>
      {indicators.length === 0 ? (
        <p>No phishing indicators were triggered.</p>
      ) : (
        <ul className="indicator-list">
          {indicators.map((indicator) => (
            <li key={indicator.ruleId}>
              <div className="row-between">
                <strong>{indicator.name}</strong>
                <span className={`pill pill-risk-${indicator.severity.toLowerCase()}`}>
                  {indicator.severity}
                </span>
              </div>
              <p>{indicator.description}</p>
              <p>
                <strong>Score:</strong> +{indicator.scoreContribution}
              </p>
              {indicator.evidence ? (
                <p>
                  <strong>Evidence:</strong> {indicator.evidence}
                </p>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
