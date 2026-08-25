import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { IncidentPage } from '../pages/IncidentPage';

function jsonResponse(payload: unknown, status = 200): Response {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('IncidentPage', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('uploads evidence and renders analysis results', async () => {
    let evidenceUploaded = false;
    let analysisCompleted = false;
    let coordinatorPlanSaved = false;
    let investigatorReportSaved = false;

    const analysisResult = {
      incidentId: 'incident-abc',
      riskScore: 87,
      riskLevel: 'High',
      summary: 'Multiple phishing indicators suggest elevated risk.',
      analyzedAt: '2026-02-02T00:06:00Z',
      email: {
        displayName: 'Accounts Team',
        fromAddress: 'billing@alerts-payments.example',
        replyToAddress: 'urgent-payments@example.net',
        returnPath: 'mailer@example.net',
        subject: 'Payment required',
        date: '2026-02-02T00:01:00Z',
        messageId: '<abc@example.net>',
        receivedHeaders: [],
        authentication: {
          spf: 'Fail',
          dkim: 'Fail',
          dmarc: 'Fail',
          authenticationResultsHeaders: [],
        },
        urls: [
          {
            url: 'http://pay.example.net/verify',
            displayText: 'Verify payment',
            source: 'HtmlBody',
          },
        ],
        attachments: [],
        plainTextBody: 'Please verify payment today.',
        htmlBody: null,
      },
      ruleResults: [],
      indicators: [
        {
          ruleId: 'R1',
          name: 'SPF/DKIM failure',
          description: 'Authentication checks failed.',
          severity: 'High',
          scoreContribution: 40,
          evidence: 'spf=fail dkim=fail dmarc=fail',
          triggered: true,
        },
      ],
      mitreAttackMappings: [
        {
          techniqueId: 'T1566.001',
          name: 'Spearphishing Attachment',
          description: 'Targeted phishing content using deceptive messaging.',
        },
      ],
    };

    const coordinatorPlan = {
      incidentId: 'incident-abc',
      status: 'Completed',
      model: 'fake-coordinator-model',
      promptVersion: 'coordinator-v1',
      startedAt: '2026-02-02T00:07:00Z',
      completedAt: '2026-02-02T00:08:00Z',
      errorMessage: null,
      plan: {
        incidentId: 'incident-abc',
        incidentType: 'Credential Phishing',
        priority: 'High',
        readyForInvestigation: true,
        tasks: [
          {
            id: 'task-1',
            taskType: 'AccountExposureCheck',
            title: 'Determine whether credentials were entered',
            description: 'Confirm whether the user submitted credentials to the suspicious page.',
            priority: 1,
            requiredEvidence: ['User confirmation'],
            assignedCapability: 'Investigator',
            completionCondition: 'Credential submission status is known',
          },
          {
            id: 'task-2',
            taskType: 'SignInReview',
            title: 'Review account sign-in activity',
            description: 'Check for suspicious or unfamiliar sign-ins after the phishing event.',
            priority: 2,
            requiredEvidence: ['Relevant account sign-in logs'],
            assignedCapability: 'Investigator',
            completionCondition: 'Recent sign-ins are reviewed',
          },
        ],
        missingInformation: [
          {
            question: 'Did the user enter credentials on the page?',
            reason: 'This determines whether credential exposure should be assumed.',
            required: true,
          },
        ],
        assumptions: ['Only the suspicious email and deterministic analysis were submitted.'],
        safetyNotes: ['Do not perform account-changing actions without human approval.'],
      },
    };

    const investigatorReport = {
      incidentId: 'incident-abc',
      status: 'Completed',
      model: 'fake-investigator-model',
      promptVersion: 'investigator-v1',
      startedAt: '2026-02-02T00:09:00Z',
      completedAt: '2026-02-02T00:10:00Z',
      errorMessage: null,
      report: {
        incidentId: 'incident-abc',
        classification: 'Credential Phishing',
        severity: 'High',
        confidence: 0.88,
        findings: [
          {
            id: 'finding-1',
            title: 'Authentication failures observed',
            description: 'SPF and DMARC failed in deterministic evidence.',
            severity: 'High',
            confidence: 0.9,
            evidenceSource: 'EmailAuthenticationTool',
            evidenceReference: 'auth:spf',
            evidence: 'SPF failed in parsed authentication headers.',
            findingType: 'AuthenticationAnomaly',
            taskId: 'task-1',
          },
        ],
        attackTechniques: [
          {
            techniqueId: 'T1566.002',
            name: 'Spearphishing Link',
            basis: 'Deterministic phishing-linked indicators observed.',
          },
        ],
        possibleImpact: ['Potential credential exposure if recipient entered credentials.'],
        uncertainties: ['Credential entry status remains unknown.'],
        taskResults: [
          {
            taskId: 'task-1',
            taskType: 'ReviewEmailAuthentication',
            status: 'Completed',
            toolsUsed: ['EmailAuthenticationTool'],
            evidenceReferences: ['auth:spf'],
            summary: 'Task completed using deterministic tools.',
          },
          {
            taskId: 'task-2',
            taskType: 'SignInReview',
            status: 'Unsupported',
            toolsUsed: [],
            evidenceReferences: [],
            summary: 'No supported deterministic investigation tool is available for this task type.',
          },
        ],
      },
    };

    const fetchMock = vi.fn(async (input: string | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';

      if (url.endsWith('/api/incidents/incident-abc') && method === 'GET') {
        return jsonResponse({
          id: 'incident-abc',
          organizationName: 'Mercy Clinic',
          organizationType: 'Nonprofit',
          description: 'Potential invoice phishing email.',
          reportedBy: 'Jesse',
          createdAt: '2026-02-02T00:00:00Z',
          status: evidenceUploaded ? 'Completed' : 'Submitted',
          technicalSkillLevel: 'Intermediate',
          evidenceItems: evidenceUploaded
            ? [
                {
                  id: 'evidence-1',
                  incidentId: 'incident-abc',
                  fileName: 'invoice-alert.eml',
                  contentType: 'message/rfc822',
                  evidenceType: 'EmailFile',
                  fileSize: 512,
                  sha256: 'abc123',
                  uploadedAt: '2026-02-02T00:05:00Z',
                },
              ]
            : [],
        });
      }

      if (url.endsWith('/api/incidents/incident-abc/evidence/email') && method === 'POST') {
        evidenceUploaded = true;
        return jsonResponse({
          id: 'evidence-1',
          incidentId: 'incident-abc',
          fileName: 'invoice-alert.eml',
          contentType: 'message/rfc822',
          evidenceType: 'EmailFile',
          fileSize: 512,
          sha256: 'abc123',
          uploadedAt: '2026-02-02T00:05:00Z',
        });
      }

      if (url.endsWith('/api/incidents/incident-abc/analyze') && method === 'POST') {
        analysisCompleted = true;
        return jsonResponse(analysisResult);
      }

      if (url.endsWith('/api/incidents/incident-abc/analysis') && method === 'GET') {
        return analysisCompleted ? jsonResponse(analysisResult) : jsonResponse({}, 404);
      }

      if (url.endsWith('/api/incidents/incident-abc/coordinator/plan') && method === 'GET') {
        return coordinatorPlanSaved ? jsonResponse(coordinatorPlan) : jsonResponse({}, 404);
      }

      if (url.endsWith('/api/incidents/incident-abc/coordinator/plan') && method === 'POST') {
        coordinatorPlanSaved = true;
        return jsonResponse(coordinatorPlan);
      }

      if (url.endsWith('/api/incidents/incident-abc/investigator/report') && method === 'GET') {
        return investigatorReportSaved ? jsonResponse(investigatorReport) : jsonResponse({}, 404);
      }

      if (url.endsWith('/api/incidents/incident-abc/investigator/run') && method === 'POST') {
        investigatorReportSaved = true;
        return jsonResponse(investigatorReport);
      }

      return jsonResponse({}, 404);
    });

    vi.stubGlobal('fetch', fetchMock);

    const { container } = render(
      <MemoryRouter initialEntries={['/incidents/incident-abc']}>
        <Routes>
          <Route path="/incidents/:id" element={<IncidentPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByText('Incident incident-abc')).toBeInTheDocument();

    const fileInput = container.querySelector('input[type="file"]');
    expect(fileInput).not.toBeNull();
    const file = new File(['From: fake@example.com'], 'invoice-alert.eml', {
      type: 'message/rfc822',
    });
    await userEvent.upload(fileInput as HTMLInputElement, file);

    await userEvent.click(screen.getByRole('button', { name: 'Upload Evidence' }));
    expect(await screen.findByText('invoice-alert.eml')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Analyze Incident' }));

    expect(await screen.findByText('Risk: High')).toBeInTheDocument();
    expect(screen.getByText('Multiple phishing indicators suggest elevated risk.')).toBeInTheDocument();
    expect(screen.getByText('T1566.001')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /http:\/\/pay\.example\.net\/verify/i })).toBeNull();

    expect(screen.getByText('Coordinator status: Not started')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Generate Investigation Plan' }));

    expect(await screen.findByText('Coordinator status: Completed')).toBeInTheDocument();
    expect(screen.getByText('Incident classification: Credential Phishing')).toBeInTheDocument();
    expect(screen.getByText('Priority: HIGH')).toBeInTheDocument();
    expect(screen.getByText('Yes')).toBeInTheDocument();
    expect(screen.getByText('Determine whether credentials were entered')).toBeInTheDocument();
    expect(screen.getByText('Did the user enter credentials on the page?')).toBeInTheDocument();

    expect(screen.getByText('Investigator status: Not started')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Run Investigation' }));

    expect(await screen.findByText('Investigator status: Completed')).toBeInTheDocument();
    expect(screen.getByText('Classification:')).toBeInTheDocument();
    expect(screen.getByText('Credential Phishing')).toBeInTheDocument();
    expect(screen.getByText('Authentication failures observed')).toBeInTheDocument();
    expect(screen.getByText('T1566.002 - Spearphishing Link')).toBeInTheDocument();
    expect(screen.getByText('Credential entry status remains unknown.')).toBeInTheDocument();

    document.body.innerHTML = '';

    render(
      <MemoryRouter initialEntries={['/incidents/incident-abc']}>
        <Routes>
          <Route path="/incidents/:id" element={<IncidentPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByText('Coordinator status: Completed')).toBeInTheDocument();
    expect(screen.getByText('Incident classification: Credential Phishing')).toBeInTheDocument();
    expect(screen.getByText('Did the user enter credentials on the page?')).toBeInTheDocument();
    expect(screen.getByText('Investigator status: Completed')).toBeInTheDocument();
    expect(screen.getByText('Authentication failures observed')).toBeInTheDocument();
  });
});
