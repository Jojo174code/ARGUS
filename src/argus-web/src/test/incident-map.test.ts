import { describe, expect, it } from 'vitest';
import type { AnalysisResult, InvestigatorReportResponse, ResponseEducationPackageResponse } from '../api/types';
import { buildIncidentMap, getIncidentMapFocus } from '../components/incident-map-data';

const analysis: AnalysisResult = {
  incidentId: 'incident-1',
  riskScore: 82,
  riskLevel: 'High',
  summary: 'Risk signals detected.',
  analyzedAt: '2026-08-26T00:00:00Z',
  email: {
    displayName: null, fromAddress: null, replyToAddress: null, returnPath: null, subject: null, date: null, messageId: null,
    receivedHeaders: [], authentication: { spf: 'Fail', dkim: 'Pass', dmarc: 'Fail', authenticationResultsHeaders: [] },
    urls: [], attachments: [], plainTextBody: null, htmlBody: null,
  },
  ruleResults: [],
  indicators: [
    { ruleId: 'URL-001', name: 'Suspicious URL', description: 'A suspicious URL was detected.', severity: 'High', scoreContribution: 20, evidence: 'url', triggered: true },
  ],
  mitreAttackMappings: [],
};

const investigatorReport: InvestigatorReportResponse = {
  incidentId: 'incident-1', status: 'Completed', model: 'test', promptVersion: 'test', startedAt: '2026-08-26T00:00:00Z', completedAt: null, errorMessage: null,
  report: {
    incidentId: 'incident-1', classification: 'Credential Phishing', severity: 'High', confidence: 0.9,
    findings: [
      { id: 'finding-1', title: 'Credential Phishing', description: 'Credential collection was observed.', severity: 'High', confidence: 0.9, evidenceSource: 'UrlInspectionTool', evidenceReference: 'indicator:URL-001', evidence: 'url', findingType: 'Phishing', taskId: 'task-1' },
      { id: 'finding-2', title: 'Unrelated Finding', description: 'No linked action.', severity: 'Moderate', confidence: 0.7, evidenceSource: 'EmailMetadataTool', evidenceReference: 'email:subject', evidence: 'subject', findingType: 'Metadata', taskId: 'task-2' },
    ],
    attackTechniques: [{ techniqueId: 'T1566', name: 'Phishing', basis: 'Validated evidence.' }],
    possibleImpact: [], uncertainties: [], taskResults: [],
  },
};

const responsePackage: ResponseEducationPackageResponse = {
  incidentId: 'incident-1', status: 'Completed', model: 'test', promptVersion: 'test', startedAt: '2026-08-26T00:00:00Z', completedAt: null, errorMessage: null,
  package: {
    incidentId: 'incident-1', incidentClassification: 'Credential Phishing', overallPriority: 'High', plainLanguageSummary: 'Summary.',
    immediateActions: [{ id: 'action-1', title: 'Change Password', description: 'Change the password.', reason: 'Finding supports this action.', priority: 1, actionType: 'UserAction', requiresHumanApproval: true, responsibleRole: 'User', supportingFindingIds: ['finding-1'] }],
    recoveryActions: [], preventionActions: [], escalationRecommendations: [],
    education: { title: 'Lesson', audienceLevel: 'Beginner', estimatedMinutes: 5, learningObjective: 'Learn.', explanation: 'Learn.', warningSigns: [], questions: [], takeaways: [] },
    assumptions: [], limitations: [],
  },
};

describe('buildIncidentMap', () => {
  it('connects actions only to their declared supporting findings', () => {
    const graph = buildIncidentMap({ analysis, investigatorReport, responsePackage });

    expect(graph.edges).toContainEqual({ id: 'finding:finding-1->action:action-1', source: 'finding:finding-1', target: 'action:action-1' });
    expect(graph.edges).not.toContainEqual({ id: 'finding:finding-2->action:action-1', source: 'finding:finding-2', target: 'action:action-1' });
  });

  it('connects a finding to evidence only when its structured reference matches', () => {
    const graph = buildIncidentMap({ analysis, investigatorReport, responsePackage });

    expect(graph.edges).toContainEqual({ id: 'evidence:URL-001->finding:finding-1', source: 'evidence:URL-001', target: 'finding:finding-1' });
    expect(graph.edges.some((edge) => edge.target === 'finding:finding-2' && edge.source.startsWith('evidence:'))).toBe(false);
  });

  it('keeps the evidence to finding graph available before response generation', () => {
    const graph = buildIncidentMap({ analysis, investigatorReport, responsePackage: null });

    expect(graph.nodes.some((node) => node.kind === 'evidence')).toBe(true);
    expect(graph.nodes.some((node) => node.kind === 'finding')).toBe(true);
    expect(graph.nodes.some((node) => node.kind === 'action')).toBe(false);
  });

  it('focuses the selected finding and directly related evidence and action nodes', () => {
    const graph = buildIncidentMap({ analysis, investigatorReport, responsePackage });
    const focus = getIncidentMapFocus(graph, 'finding:finding-1');

    expect(focus).toEqual(new Set(['finding:finding-1', 'evidence:URL-001', 'action:action-1']));
    expect(focus?.has('finding:finding-2')).toBe(false);
  });

  it('preserves human approval requirements on action nodes', () => {
    const graph = buildIncidentMap({ analysis, investigatorReport, responsePackage });

    expect(graph.nodes.find((node) => node.id === 'action:action-1')?.action?.requiresHumanApproval).toBe(true);
  });
});