import type { RiskLevel } from '../api/types';

export function RiskBadge({ riskLevel }: { riskLevel: RiskLevel }) {
  return (
    <span className={`pill pill-risk-${riskLevel.toLowerCase()}`}>
      Risk: {riskLevel}
    </span>
  );
}
