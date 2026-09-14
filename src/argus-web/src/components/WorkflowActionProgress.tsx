const actionCopy = {
  analysis: {
    heading: 'Analyzing Email',
    description: 'ARGUS is checking headers, authentication results, URLs, and phishing indicators.',
  },
  plan: {
    heading: 'Planning Investigation',
    description: 'The Coordinator Agent is building an evidence-grounded investigation plan.',
  },
  investigator: {
    heading: 'Investigating Evidence',
    description: 'The Investigator Agent is reviewing available evidence and synthesizing findings.',
  },
  response: {
    heading: 'Generating Response & Education',
    description: 'ARGUS is preparing recommended actions and an incident-specific learning module.',
  },
} as const;

interface WorkflowActionProgressProps {
  action: keyof typeof actionCopy;
}

export function WorkflowActionProgress({ action }: WorkflowActionProgressProps) {
  const copy = actionCopy[action];

  return (
    <section className="workflow-action-progress" aria-live="polite" aria-label={`${copy.heading} in progress`}>
      <div>
        <p className="workflow-progress-kicker">ARGUS is working</p>
        <h3>{copy.heading}</h3>
        <p>{copy.description}</p>
      </div>
      <div className="workflow-progress-track" role="progressbar" aria-valuetext={`${copy.heading} is in progress`}>
        <span className="workflow-progress-fill is-indeterminate" />
      </div>
    </section>
  );
}