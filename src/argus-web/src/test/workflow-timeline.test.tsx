import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { WorkflowTimeline } from '../components/WorkflowTimeline';
import type { AgenticWorkflowResultResponse, WorkflowRunStatus } from '../api/types';

const stageNames = ['DeterministicAnalysis', 'Coordinator', 'Investigator', 'ResponseEducation'];

function createWorkflow(status: WorkflowRunStatus, activeStage?: string): AgenticWorkflowResultResponse {
  return {
    incidentId: 'incident-1',
    workflowRunId: 'workflow-1',
    status,
    stages: stageNames.map((stage, index) => ({
      stage,
      status: activeStage === stage ? 'Running' : activeStage && index < stageNames.indexOf(activeStage) ? 'Completed' : 'Pending',
      startedAt: null,
      completedAt: null,
      summary: null,
      error: null,
    })),
    startedAt: '2026-09-09T00:00:00Z',
    completedAt: status === 'Running' ? null : '2026-09-09T00:02:00Z',
    failureStage: null,
    failureMessage: null,
    blockingMissingInformation: [],
    metrics: {
      coordinatorTaskCount: 0,
      investigatorFindingCount: 0,
      unsupportedTaskCount: 0,
      responseActionCount: 0,
      quizQuestionCount: 0,
      durationMilliseconds: 0,
    },
  };
}

describe('WorkflowTimeline', () => {
  it('shows a truthful not-started state', () => {
    render(<WorkflowTimeline workflow={null} />);

    expect(screen.getByRole('heading', { name: 'Workflow Not Started' })).toBeInTheDocument();
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '0');
    expect(screen.getAllByText('Waiting for the previous stage.')).toHaveLength(4);
  });

  it.each([
    ['DeterministicAnalysis', 'Analyzing Email', '0'],
    ['Coordinator', 'Planning Investigation', '20'],
    ['Investigator', 'Investigating Evidence', '40'],
    ['ResponseEducation', 'Generating Response & Education', '60'],
  ])('shows %s as the active stage', (activeStage, heading, progress) => {
    render(<WorkflowTimeline workflow={createWorkflow('Running', activeStage)} isActive />);

    expect(screen.getByRole('heading', { name: heading })).toBeInTheDocument();
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', progress);
    expect(screen.getByText(`${heading}: Running`)).toBeInTheDocument();
  });

  it('shows completion as the final workflow state', () => {
    const workflow = createWorkflow('Completed');
    workflow.stages = workflow.stages.map((stage) => ({ ...stage, status: 'Completed' }));
    render(<WorkflowTimeline workflow={workflow} />);

    expect(screen.getByRole('heading', { name: 'Investigation Complete' })).toBeInTheDocument();
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '100');
  });

  it('shows failed stage details without presenting them as progress', () => {
    const workflow = createWorkflow('Failed');
    workflow.failureStage = 'Investigator';
    workflow.failureMessage = 'The provider did not complete this stage.';
    workflow.stages[2] = { ...workflow.stages[2], status: 'Failed', error: workflow.failureMessage };
    render(<WorkflowTimeline workflow={workflow} />);

    expect(screen.getByRole('heading', { name: 'Workflow Paused' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Investigating Evidence');
    expect(screen.getByRole('alert')).toHaveTextContent(workflow.failureMessage);
  });

  it('shows awaiting information as a distinct non-crash state', () => {
    const workflow = createWorkflow('AwaitingInformation');
    workflow.stages[0] = { ...workflow.stages[0], status: 'Completed' };
    workflow.stages[1] = { ...workflow.stages[1], status: 'AwaitingInformation' };
    workflow.blockingMissingInformation = [{
      question: 'Did the user enter credentials?',
      reason: 'This is required before the investigation can continue.',
      required: true,
      blocksInvestigation: true,
    }];
    render(<WorkflowTimeline workflow={workflow} />);

    expect(screen.getByRole('heading', { name: 'Additional Information Needed' })).toBeInTheDocument();
    expect(screen.getAllByText('Additional information is needed before ARGUS can continue.')).toHaveLength(2);
    expect(screen.getByText('Did the user enter credentials?')).toBeInTheDocument();
  });
});