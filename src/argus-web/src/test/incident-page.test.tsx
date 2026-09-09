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
    let responsePackageSaved = false;
    let workflowSaved = false;

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

    const responsePackage = {
      incidentId: 'incident-abc',
      status: 'Completed',
      model: 'fake-response-model',
      promptVersion: 'response-education-v1',
      startedAt: '2026-02-02T00:11:00Z',
      completedAt: '2026-02-02T00:12:00Z',
      errorMessage: null,
      package: {
        incidentId: 'incident-abc',
        incidentClassification: 'Credential Phishing',
        overallPriority: 'High',
        plainLanguageSummary:
          'ARGUS found evidence consistent with a credential-phishing attempt targeting account credentials.',
        immediateActions: [
          {
            id: 'action-1',
            title: 'Preserve the suspicious email and related notes',
            description: 'Keep the suspicious message and related notes available for review.',
            reason: 'The validated findings may need to be referenced during internal follow-up.',
            priority: 1,
            actionType: 'Informational',
            requiresHumanApproval: false,
            responsibleRole: 'Incident Reporter',
            supportingFindingIds: ['finding-1'],
          },
        ],
        recoveryActions: [
          {
            id: 'action-2',
            title: 'Review recent account sign-ins',
            description: 'Review recent sign-in history for unfamiliar access after the phishing event.',
            reason: 'This helps determine whether suspicious access followed the phishing attempt.',
            priority: 1,
            actionType: 'AdministratorAction',
            requiresHumanApproval: true,
            responsibleRole: 'Administrator',
            supportingFindingIds: ['finding-1'],
          },
        ],
        preventionActions: [
          {
            id: 'action-3',
            title: 'Enable multi-factor authentication',
            description: 'Require MFA for the affected account if it is not already enabled.',
            reason: 'MFA reduces the impact of stolen credentials from phishing.',
            priority: 1,
            actionType: 'AdministratorAction',
            requiresHumanApproval: true,
            responsibleRole: 'Administrator',
            supportingFindingIds: ['finding-1'],
          },
        ],
        escalationRecommendations: [
          {
            level: 'InternalIT',
            reason: 'Internal review is appropriate based on the phishing findings.',
            recommendedContact: 'Internal IT administrator',
            urgent: false,
          },
        ],
        education: {
          title: 'Recognizing Credential Phishing in Email',
          audienceLevel: 'Intermediate',
          estimatedMinutes: 10,
          learningObjective: 'Learn how to identify warning signs from this incident before interacting with similar messages.',
          explanation: 'This lesson uses the actual warning signs found in the investigated email.',
          warningSigns: [
            {
              title: 'Failed authentication checks',
              explanation: 'The investigation found failed authentication results linked to this message.',
              supportingFindingIds: ['finding-1'],
            },
          ],
          questions: [
            {
              id: 'question-1',
              question: 'Which warning sign in this incident suggested the sender might not be legitimate?',
              options: ['Failed authentication checks', 'A short message', 'A weekday delivery time'],
              correctOptionIndex: 0,
              explanation: 'Failed authentication checks were one of the strongest validated warning signs.',
            },
            {
              id: 'question-2',
              question: 'What should a user verify before signing in from an email prompt?',
              options: ['The page colors', 'The trusted domain', 'The font used in the message'],
              correctOptionIndex: 1,
              explanation: 'The destination domain is a stronger signal than visual styling.',
            },
            {
              id: 'question-3',
              question: 'Why is this incident useful for staff awareness training?',
              options: ['It shows real warning signs from the incident', 'It guarantees future safety', 'It replaces investigation'],
              correctOptionIndex: 0,
              explanation: 'Incident-specific examples help staff recognize similar phishing attempts later.',
            },
          ],
          takeaways: ['Check trust signals before entering credentials.'],
        },
        assumptions: ['Potential credential exposure depends on whether the user interacted with the phishing destination.'],
        limitations: ['This package does not confirm compromise beyond the validated findings.'],
      },
    };

    const workflowResponse = {
      incidentId: 'incident-abc',
      workflowRunId: 'workflow-1',
      status: 'Completed',
      stages: [
        {
          stage: 'DeterministicAnalysis',
          status: 'Completed',
          startedAt: '2026-02-02T00:06:00Z',
          completedAt: '2026-02-02T00:06:01Z',
          summary: 'Generated deterministic analysis with risk High and score 87.',
          error: null,
        },
        {
          stage: 'Coordinator',
          status: 'Completed',
          startedAt: '2026-02-02T00:07:00Z',
          completedAt: '2026-02-02T00:07:01Z',
          summary: 'Generated coordinator plan with 2 tasks.',
          error: null,
        },
        {
          stage: 'Investigator',
          status: 'Completed',
          startedAt: '2026-02-02T00:08:00Z',
          completedAt: '2026-02-02T00:08:01Z',
          summary: 'Generated investigator report with 1 findings and 1 unsupported tasks.',
          error: null,
        },
        {
          stage: 'ResponseEducation',
          status: 'Completed',
          startedAt: '2026-02-02T00:09:00Z',
          completedAt: '2026-02-02T00:09:01Z',
          summary: 'Generated response package with 3 actions and 3 questions.',
          error: null,
        },
      ],
      startedAt: '2026-02-02T00:06:00Z',
      completedAt: '2026-02-02T00:09:01Z',
      failureStage: null,
      failureMessage: null,
      blockingMissingInformation: [],
      metrics: {
        coordinatorTaskCount: 2,
        investigatorFindingCount: 1,
        unsupportedTaskCount: 1,
        responseActionCount: 3,
        quizQuestionCount: 3,
        durationMilliseconds: 3000,
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

      if (url.endsWith('/api/incidents/incident-abc/response') && method === 'GET') {
        return responsePackageSaved ? jsonResponse(responsePackage) : jsonResponse({}, 404);
      }

      if (url.endsWith('/api/incidents/incident-abc/response/generate') && method === 'POST') {
        responsePackageSaved = true;
        return jsonResponse(responsePackage);
      }

      if (url.endsWith('/api/incidents/incident-abc/workflow') && method === 'GET') {
        return workflowSaved ? jsonResponse(workflowResponse) : jsonResponse({}, 404);
      }

      if (url.endsWith('/api/incidents/incident-abc/workflow/run') && method === 'POST') {
        workflowSaved = true;
        analysisCompleted = true;
        coordinatorPlanSaved = true;
        investigatorReportSaved = true;
        responsePackageSaved = true;
        evidenceUploaded = true;
        return jsonResponse(workflowResponse);
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
    expect(screen.getByRole('button', { name: 'Run Full ARGUS Workflow' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Run Full ARGUS Workflow' }));

    expect(await screen.findByRole('heading', { name: 'Investigation Complete' })).toBeInTheDocument();
    expect(screen.getByText('Stage-based progress: 100%')).toBeInTheDocument();
    expect(screen.getAllByText('Analyzing Email').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Generating Response & Education').length).toBeGreaterThan(0);

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
    expect(screen.getByText('Report Summary')).toBeInTheDocument();
    expect(screen.getByText('Credential Phishing')).toBeInTheDocument();
    expect(screen.getAllByText('Authentication failures observed').length).toBeGreaterThan(0);
    expect(screen.getByText('T1566.002 - Spearphishing Link')).toBeInTheDocument();
    expect(screen.getByText('Credential entry status remains unknown.')).toBeInTheDocument();

    expect(screen.getByText('Response & Learning status: Not started')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Generate Response & Learning Plan' }));

    expect(await screen.findByText('Response & Learning status: Completed')).toBeInTheDocument();
    expect(screen.getByText('What ARGUS Found')).toBeInTheDocument();
    expect(screen.getByText('ARGUS found evidence consistent with a credential-phishing attempt targeting account credentials.')).toBeInTheDocument();
    expect(screen.getByText('Immediate Actions')).toBeInTheDocument();
    expect(screen.getAllByText('Preserve the suspicious email and related notes').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Informational').length).toBeGreaterThan(0);
    expect(screen.getByText('Approval: No')).toBeInTheDocument();
    expect(screen.getByText('Prevention Actions')).toBeInTheDocument();
    expect(screen.getAllByText('Enable multi-factor authentication').length).toBeGreaterThan(0);
    expect(screen.getByText('Learn From This Incident')).toBeInTheDocument();
    expect(screen.getByText('Recognizing Credential Phishing in Email')).toBeInTheDocument();
    expect(screen.getAllByText('Failed authentication checks').length).toBeGreaterThan(0);
    expect(screen.getByText('Which warning sign in this incident suggested the sender might not be legitimate?')).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText('Failed authentication checks'));
    await userEvent.click(screen.getAllByRole('button', { name: 'Check Answer' })[0]);

    expect(await screen.findByText(/Correct\./)).toBeInTheDocument();
    expect(screen.getByText('Failed authentication checks were one of the strongest validated warning signs.')).toBeInTheDocument();

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
    expect(screen.getAllByText('Authentication failures observed').length).toBeGreaterThan(0);
    expect(screen.getByText('Response & Learning status: Completed')).toBeInTheDocument();
    expect(screen.getByText('Recognizing Credential Phishing in Email')).toBeInTheDocument();
  });

  it('renders awaiting-information workflow state with blocking questions', async () => {
    const fetchMock = vi.fn(async (input: string | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';

      if (url.endsWith('/api/incidents/incident-awaiting') && method === 'GET') {
        return jsonResponse({
          id: 'incident-awaiting',
          organizationName: 'Grace Community Church',
          organizationType: 'Church',
          description: 'Possible credential phishing email.',
          reportedBy: 'Avery',
          createdAt: '2026-02-02T00:00:00Z',
          status: 'Completed',
          technicalSkillLevel: 'Beginner',
          evidenceItems: [
            {
              id: 'evidence-1',
              incidentId: 'incident-awaiting',
              fileName: 'phishing-microsoft-login.eml',
              contentType: 'message/rfc822',
              evidenceType: 'EmailFile',
              fileSize: 512,
              sha256: 'hash',
              uploadedAt: '2026-02-02T00:05:00Z',
            },
          ],
        });
      }

      if (url.endsWith('/api/incidents/incident-awaiting/analysis') && method === 'GET') {
        return jsonResponse({
          incidentId: 'incident-awaiting',
          riskScore: 72,
          riskLevel: 'High',
          summary: 'Likely phishing indicators were detected.',
          analyzedAt: '2026-02-02T00:06:00Z',
          email: {
            displayName: 'Microsoft Support',
            fromAddress: 'support@example.net',
            replyToAddress: null,
            returnPath: null,
            subject: 'Review your login',
            date: '2026-02-02T00:01:00Z',
            messageId: '<abc@example.net>',
            receivedHeaders: [],
            authentication: {
              spf: 'Fail',
              dkim: 'Fail',
              dmarc: 'Fail',
              authenticationResultsHeaders: [],
            },
            urls: [],
            attachments: [],
            plainTextBody: 'Body',
            htmlBody: null,
          },
          ruleResults: [],
          indicators: [],
          mitreAttackMappings: [],
        });
      }

      if (url.endsWith('/api/incidents/incident-awaiting/coordinator/plan') && method === 'GET') {
        return jsonResponse({
          incidentId: 'incident-awaiting',
          status: 'Completed',
          model: 'fake-coordinator-model',
          promptVersion: 'coordinator-v1',
          startedAt: '2026-02-02T00:07:00Z',
          completedAt: '2026-02-02T00:08:00Z',
          errorMessage: null,
          plan: {
            incidentId: 'incident-awaiting',
            incidentType: 'Credential Phishing',
            priority: 'High',
            readyForInvestigation: false,
            tasks: [],
            missingInformation: [
              {
                question: 'Did the user enter credentials?',
                reason: 'This blocks downstream response decisions.',
                required: true,
                blocksInvestigation: true,
              },
            ],
            assumptions: [],
            safetyNotes: [],
          },
        });
      }

      if (url.endsWith('/api/incidents/incident-awaiting/investigator/report') && method === 'GET') {
        return jsonResponse({}, 404);
      }

      if (url.endsWith('/api/incidents/incident-awaiting/response') && method === 'GET') {
        return jsonResponse({}, 404);
      }

      if (url.endsWith('/api/incidents/incident-awaiting/workflow') && method === 'GET') {
        return jsonResponse({
          incidentId: 'incident-awaiting',
          workflowRunId: 'workflow-awaiting',
          status: 'AwaitingInformation',
          stages: [
            {
              stage: 'DeterministicAnalysis',
              status: 'Completed',
              startedAt: '2026-02-02T00:06:00Z',
              completedAt: '2026-02-02T00:06:01Z',
              summary: 'Reused deterministic analysis with risk High and score 72.',
              error: null,
            },
            {
              stage: 'Coordinator',
              status: 'AwaitingInformation',
              startedAt: '2026-02-02T00:07:00Z',
              completedAt: '2026-02-02T00:07:02Z',
              summary: 'Blocking information is still required before investigation can continue.',
              error: null,
            },
            {
              stage: 'Investigator',
              status: 'Pending',
              startedAt: null,
              completedAt: null,
              summary: null,
              error: null,
            },
            {
              stage: 'ResponseEducation',
              status: 'Pending',
              startedAt: null,
              completedAt: null,
              summary: null,
              error: null,
            },
          ],
          startedAt: '2026-02-02T00:06:00Z',
          completedAt: '2026-02-02T00:07:02Z',
          failureStage: 'Coordinator',
          failureMessage: 'ARGUS needs more information before continuing.',
          blockingMissingInformation: [
            {
              question: 'Did the user enter credentials?',
              reason: 'This blocks downstream response decisions.',
              required: true,
              blocksInvestigation: true,
            },
          ],
          metrics: {
            coordinatorTaskCount: 0,
            investigatorFindingCount: 0,
            unsupportedTaskCount: 0,
            responseActionCount: 0,
            quizQuestionCount: 0,
            durationMilliseconds: 1200,
          },
        });
      }

      return jsonResponse({}, 404);
    });

    vi.stubGlobal('fetch', fetchMock);

    render(
      <MemoryRouter initialEntries={['/incidents/incident-awaiting']}>
        <Routes>
          <Route path="/incidents/:id" element={<IncidentPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole('heading', { name: 'Additional Information Needed' })).toBeInTheDocument();
    expect(screen.getByText('Awaiting Information')).toBeInTheDocument();
    expect(screen.getAllByText('Additional information is needed before ARGUS can continue.').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Did the user enter credentials?').length).toBeGreaterThan(0);
    expect(screen.getAllByText('This blocks downstream response decisions.').length).toBeGreaterThan(0);
    expect(screen.getByText('Response & Learning status: Not started')).toBeInTheDocument();
  });
});
