import type { AgenticWorkflowResultResponse, WorkflowRunStatus, WorkflowStageResult } from '../api/types';

function renderDate(value: string | null): string {
  if (!value) {
    return 'Not recorded';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function renderDuration(stage: WorkflowStageResult): string | null {
  if (!stage.startedAt || !stage.completedAt) {
    return null;
  }

  const startedAt = new Date(stage.startedAt).getTime();
  const completedAt = new Date(stage.completedAt).getTime();
  if (Number.isNaN(startedAt) || Number.isNaN(completedAt) || completedAt < startedAt) {
    return null;
  }

  return `${(completedAt - startedAt).toFixed(0)} ms`;
}

function renderStageLabel(stage: string): string {
  switch (stage) {
    case 'DeterministicAnalysis':
      return 'Deterministic Analysis';
    case 'ResponseEducation':
      return 'Response & Learning';
    default:
      return stage;
  }
}

function renderStatus(status: WorkflowRunStatus): string {
  return status === 'AwaitingInformation' ? 'Awaiting Information' : status;
}

interface WorkflowTimelineProps {
  workflow: AgenticWorkflowResultResponse;
}

export function WorkflowTimeline({ workflow }: WorkflowTimelineProps) {
  return (
    <article className="card space-stack">
      <h2>Workflow Timeline</h2>
      <div className="row-between wrap-gap">
        <span>Workflow status: {renderStatus(workflow.status)}</span>
        <span>Started {renderDate(workflow.startedAt)}</span>
      </div>
      {workflow.completedAt ? <p>Completed {renderDate(workflow.completedAt)}</p> : null}
      <ul className="missing-info-list">
        {workflow.stages.map((stage) => (
          <li key={stage.stage}>
            <strong>{renderStageLabel(stage.stage)}</strong>
            <p>
              <strong>Status:</strong> {renderStatus(stage.status)}
            </p>
            {renderDuration(stage) ? (
              <p>
                <strong>Duration:</strong> {renderDuration(stage)}
              </p>
            ) : null}
            {stage.summary ? <p>{stage.summary}</p> : null}
            {stage.error ? (
              <p>
                <strong>Error:</strong> {stage.error}
              </p>
            ) : null}
          </li>
        ))}
      </ul>

      <div className="card-inner">
        <h3>Workflow Summary</h3>
        <p>Workflow completed in {(workflow.metrics.durationMilliseconds / 1000).toFixed(1)}s</p>
        <p>{workflow.stages.filter((stage) => stage.status === 'Completed').length} stages completed</p>
        <p>{workflow.metrics.coordinatorTaskCount} investigation tasks planned</p>
        <p>{workflow.metrics.investigatorFindingCount} findings produced</p>
        <p>{workflow.metrics.responseActionCount} response actions generated</p>
        <p>{workflow.metrics.quizQuestionCount} learning questions created</p>
      </div>

      {workflow.status === 'AwaitingInformation' && workflow.blockingMissingInformation.length > 0 ? (
        <div className="card-inner">
          <h3>ARGUS needs more information before continuing.</h3>
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

      {workflow.status === 'Failed' && workflow.failureStage ? (
        <div className="card-inner">
          <h3>Workflow Failure</h3>
          <p>
            <strong>Stage:</strong> {renderStageLabel(workflow.failureStage)}
          </p>
          {workflow.failureMessage ? <p>{workflow.failureMessage}</p> : null}
        </div>
      ) : null}
    </article>
  );
}