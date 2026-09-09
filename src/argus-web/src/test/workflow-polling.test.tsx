import { act, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { IncidentPage } from '../pages/IncidentPage';
import type { AgenticWorkflowResultResponse } from '../api/types';

function jsonResponse(payload: unknown, status = 200): Response {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function createWorkflow(status: AgenticWorkflowResultResponse['status'], activeStage = 'Coordinator'): AgenticWorkflowResultResponse {
  const stageNames = ['DeterministicAnalysis', 'Coordinator', 'Investigator', 'ResponseEducation'];
  return {
    incidentId: 'incident-polling',
    workflowRunId: 'workflow-polling',
    status,
    stages: stageNames.map((stage, index) => ({
      stage,
      status: status === 'Completed' ? 'Completed' : stage === activeStage ? 'Running' : index < stageNames.indexOf(activeStage) ? 'Completed' : 'Pending',
      startedAt: null,
      completedAt: null,
      summary: null,
      error: null,
    })),
    startedAt: '2026-09-09T00:00:00Z',
    completedAt: status === 'Completed' ? '2026-09-09T00:02:00Z' : null,
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

function renderIncidentPage(): ReturnType<typeof render> {
  return render(
    <MemoryRouter initialEntries={['/incidents/incident-polling']}>
      <Routes>
        <Route path="/incidents/:id" element={<IncidentPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

async function flushPromises(): Promise<void> {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

describe('IncidentPage workflow polling', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('stops polling after a persisted terminal workflow state', async () => {
    vi.useFakeTimers();
    let currentWorkflow = createWorkflow('Running');
    let workflowGets = 0;
    let resolveRun: (workflow: AgenticWorkflowResultResponse) => void = () => undefined;
    const runPromise = new Promise<AgenticWorkflowResultResponse>((resolve) => {
      resolveRun = resolve;
    });

    vi.stubGlobal('fetch', vi.fn(async (input: string | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';
      if (url.endsWith('/api/incidents/incident-polling') && method === 'GET') {
        return jsonResponse({
          id: 'incident-polling', organizationName: 'ARGUS', organizationType: 'Church', description: 'Polling test.', reportedBy: 'Test',
          createdAt: '2026-09-09T00:00:00Z', status: 'Completed', technicalSkillLevel: 'Beginner', evidenceItems: [{ id: 'evidence-1' }],
        });
      }
      if (url.endsWith('/api/incidents/incident-polling/workflow/run') && method === 'POST') {
        return runPromise;
      }
      if (url.endsWith('/api/incidents/incident-polling/workflow') && method === 'GET') {
        workflowGets++;
        return workflowGets === 1 ? jsonResponse({}, 404) : jsonResponse(currentWorkflow);
      }
      return jsonResponse({}, 404);
    }));

    renderIncidentPage();
    await flushPromises();
    fireEvent.click(screen.getByRole('button', { name: 'Run Full ARGUS Workflow' }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1500);
    });

    expect(screen.getByRole('heading', { name: 'Planning Investigation' })).toBeInTheDocument();
    currentWorkflow = createWorkflow('Completed');
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1500);
    });

    expect(screen.getByRole('heading', { name: 'Investigation Complete' })).toBeInTheDocument();
    const terminalPollCount = workflowGets;
    await act(async () => {
      await vi.advanceTimersByTimeAsync(4500);
    });
    expect(workflowGets).toBe(terminalPollCount);
    resolveRun(currentWorkflow);
  });

  it('clears polling on component unmount', async () => {
    vi.useFakeTimers();
    let workflowGets = 0;
    let resolveRun: (workflow: AgenticWorkflowResultResponse) => void = () => undefined;
    const runPromise = new Promise<AgenticWorkflowResultResponse>((resolve) => {
      resolveRun = resolve;
    });

    vi.stubGlobal('fetch', vi.fn(async (input: string | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';
      if (url.endsWith('/api/incidents/incident-polling') && method === 'GET') {
        return jsonResponse({
          id: 'incident-polling', organizationName: 'ARGUS', organizationType: 'Church', description: 'Polling test.', reportedBy: 'Test',
          createdAt: '2026-09-09T00:00:00Z', status: 'Completed', technicalSkillLevel: 'Beginner', evidenceItems: [{ id: 'evidence-1' }],
        });
      }
      if (url.endsWith('/api/incidents/incident-polling/workflow/run') && method === 'POST') {
        return runPromise;
      }
      if (url.endsWith('/api/incidents/incident-polling/workflow') && method === 'GET') {
        workflowGets++;
        return workflowGets === 1 ? jsonResponse({}, 404) : jsonResponse(createWorkflow('Running'));
      }
      return jsonResponse({}, 404);
    }));

    const { unmount } = renderIncidentPage();
    await flushPromises();
    fireEvent.click(screen.getByRole('button', { name: 'Run Full ARGUS Workflow' }));
    await flushPromises();
    unmount();

    const pollCountAtUnmount = workflowGets;
    await act(async () => {
      await vi.advanceTimersByTimeAsync(4500);
    });
    expect(workflowGets).toBe(pollCountAtUnmount);
    resolveRun(createWorkflow('Completed'));
  });
});