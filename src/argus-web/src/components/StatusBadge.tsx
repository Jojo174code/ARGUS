import type { IncidentStatus } from '../api/types';

const statusLabel: Record<IncidentStatus, string> = {
  Submitted: 'Submitted',
  Analyzing: 'Analyzing',
  AwaitingInformation: 'Awaiting Information',
  Completed: 'Completed',
  Failed: 'Failed',
};

export function StatusBadge({ status }: { status: IncidentStatus }) {
  return <span className={`pill pill-status-${status.toLowerCase()}`}>{statusLabel[status]}</span>;
}
