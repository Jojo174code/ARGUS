import type { AgenticWorkflowResultResponse, WorkflowRunStatus, WorkflowStageResult } from '../api/types';

const stageDefinitions = [
  {
    key: 'DeterministicAnalysis',
    label: 'Analyzing Email',
    description: 'ARGUS is checking headers, authentication results, URLs, and phishing indicators.',
  },
  {
    key: 'Coordinator',
    label: 'Planning Investigation',
    description: 'The Coordinator Agent is building an evidence-grounded investigation plan.',
  },
  {
    key: 'Investigator',
    label: 'Investigating Evidence',
    description: 'The Investigator Agent is reviewing available evidence and synthesizing findings.',
  },
  {
    key: 'ResponseEducation',
    label: 'Generating Response & Education',
    description: 'ARGUS is preparing recommended actions and an incident-specific learning module.',
  },
] as const;

type StageKey = (typeof stageDefinitions)[number]['key'];

function isTerminal(status: WorkflowRunStatus): boolean {
  return status === 'Completed' || status === 'Failed' || status === 'AwaitingInformation';
}

function getStage(workflow: AgenticWorkflowResultResponse | null, key: StageKey): WorkflowStageResult | undefined {
  return workflow?.stages.find((stage) => stage.stage === key);
}

function getStageLabel(key: string | null): string | null {
  return stageDefinitions.find((stage) => stage.key === key)?.label ?? key;
}

function getStageProgress(workflow: AgenticWorkflowResultResponse | null): number {
  if (!workflow) {
    return 0;
  }

  if (workflow.status === 'Completed') {
    return 100;
  }

  return stageDefinitions.filter((stage) => getStage(workflow, stage.key)?.status === 'Completed').length * 20;
}

function getActivity(workflow: AgenticWorkflowResultResponse | null, isActive: boolean): { heading: string; description: string; stage: StageKey | null } {
  if (!workflow) {
    return {
      heading: isActive ? 'Starting Investigation' : 'Workflow Not Started',
      description: isActive ? 'ARGUS is starting the investigation workflow.' : 'Run the full workflow to begin the investigation.',
      stage: null,
    };
  }

  if (workflow.status === 'Completed') {
    return {
      heading: 'Investigation Complete',
      description: 'ARGUS has completed the analysis, investigation, and response preparation stages.',
      stage: null,
    };
  }

  if (workflow.status === 'AwaitingInformation') {
    return {
      heading: 'Additional Information Needed',
      description: 'Additional information is needed before ARGUS can continue.',
      stage: null,
    };
  }

  if (workflow.status === 'Failed') {
    return {
      heading: 'Workflow Paused',
      description: workflow.failureMessage ?? 'ARGUS could not complete the current workflow stage.',
      stage: null,
    };
  }

  const activeDefinition = stageDefinitions.find((stage) => getStage(workflow, stage.key)?.status === 'Running');
  return activeDefinition
    ? { heading: activeDefinition.label, description: activeDefinition.description, stage: activeDefinition.key }
    : { heading: 'Starting Investigation', description: 'ARGUS is preparing the next workflow stage.', stage: null };
}

interface WorkflowTimelineProps {
  workflow: AgenticWorkflowResultResponse | null;
  isActive?: boolean;
}

export function WorkflowTimeline({ workflow, isActive = false }: WorkflowTimelineProps) {
  const progress = getStageProgress(workflow);
  const activity = getActivity(workflow, isActive);
  const isIndeterminate = isActive && !isTerminal(workflow?.status ?? 'Running');

  return (
    <article className="card workflow-progress" aria-labelledby="workflow-progress-title">
      <div className="workflow-progress-header">
        <div>
          <p className="workflow-progress-kicker">ARGUS AI Investigation</p>
          <h2 id="workflow-progress-title">{activity.heading}</h2>
          <p>{activity.description}</p>
        </div>
        <span className={`workflow-status workflow-status-${workflow?.status.toLowerCase() ?? 'pending'}`}>
          {workflow?.status === 'AwaitingInformation' ? 'Awaiting Information' : workflow?.status ?? 'Preparing'}
        </span>
      </div>

      <div
        className="workflow-progress-track"
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={progress}
        aria-valuetext={isIndeterminate ? `${activity.heading} is in progress. ${progress}% of workflow stages are complete.` : `${progress}% of workflow stages are complete.`}
      >
        <span className={isIndeterminate ? 'workflow-progress-fill is-indeterminate' : 'workflow-progress-fill'} style={{ width: `${progress}%` }} />
      </div>
      <p className="workflow-progress-measure">Stage-based progress: {progress}%</p>

      <ol className="workflow-stage-list" aria-label="Workflow stages">
        {stageDefinitions.map((definition) => {
          const stage = getStage(workflow, definition.key);
          const status = stage?.status ?? 'Pending';
          const isCurrent = activity.stage === definition.key && status === 'Running';
          const stageDescription = status === 'Running'
            ? definition.description
            : status === 'Completed'
              ? 'Completed'
              : status === 'Failed'
                ? stage?.error ?? 'This stage could not be completed.'
                : status === 'AwaitingInformation'
                  ? 'Additional information is required before continuing.'
                  : 'Waiting for the previous stage.';

          return (
            <li key={definition.key} className={`workflow-stage workflow-stage-${status.toLowerCase()}${isCurrent ? ' is-current' : ''}`}>
              <span className="workflow-stage-marker" aria-hidden="true" />
              <div>
                <strong>{definition.label}</strong>
                <p>{stageDescription}</p>
              </div>
              <span className="sr-only">{definition.label}: {status === 'AwaitingInformation' ? 'Awaiting Information' : status}</span>
            </li>
          );
        })}
      </ol>

      {workflow?.status === 'AwaitingInformation' && workflow.blockingMissingInformation.length > 0 ? (
        <div className="workflow-progress-detail">
          <h3>Additional information is needed before ARGUS can continue.</h3>
          <ul className="missing-info-list">
            {workflow.blockingMissingInformation.map((item) => (
              <li key={item.question}>
                <strong>{item.question}</strong>
                <p>{item.reason}</p>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {workflow?.status === 'Failed' ? (
        <div className="workflow-progress-detail workflow-progress-failure" role="alert">
          <h3>Workflow Failure</h3>
          {workflow.failureStage ? <p><strong>Stage:</strong> {getStageLabel(workflow.failureStage)}</p> : null}
          {workflow.failureMessage ? <p>{workflow.failureMessage}</p> : null}
        </div>
      ) : null}
    </article>
  );
}