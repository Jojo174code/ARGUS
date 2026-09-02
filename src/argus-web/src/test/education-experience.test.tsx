import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { EducationExperience } from '../components/EducationExperience';
import type { Incident, ResponseEducationPackage } from '../api/types';

const incident: Incident = {
  id: 'incident-1', organizationName: 'Grace Church', organizationType: 'Church', description: 'Suspicious sign-in notice.', reportedBy: 'Avery', createdAt: '2026-08-01T00:00:00Z', status: 'Completed', technicalSkillLevel: 'Beginner', evidenceItems: [],
};

const responsePackage: ResponseEducationPackage = {
  incidentId: 'incident-1', incidentClassification: 'Credential Phishing', overallPriority: 'High', plainLanguageSummary: 'A fake sign-in notice attempted to collect credentials.', immediateActions: [], recoveryActions: [], preventionActions: [], escalationRecommendations: [], assumptions: [], limitations: [],
  education: {
    title: 'Spotting fake sign-in notices', audienceLevel: 'Beginner', estimatedMinutes: 4, learningObjective: 'Verify a sender and link before responding.', explanation: 'Use the signs below.',
    warningSigns: [{ title: 'Sender mismatch', explanation: 'The actual sender was not Microsoft.', supportingFindingIds: ['finding-1'] }],
    questions: [
      { id: 'q1', question: 'What should you check first?', options: ['Sender domain', 'Reply immediately', 'Share a password'], correctOptionIndex: 0, explanation: 'Check the actual sender domain.' },
      { id: 'q2', question: 'Should the link be opened?', options: ['Yes', 'No', 'Only quickly'], correctOptionIndex: 1, explanation: 'Verify through a trusted channel instead.' },
    ],
    takeaways: ['Check the actual sender domain.', 'Verify links through trusted channels.', 'Pause when a message creates urgency.'],
  },
};

describe('EducationExperience', () => {
  it('guides evidence review through a sequential knowledge check and score', async () => {
    const user = userEvent.setup();
    render(<EducationExperience incident={incident} responsePackage={responsePackage} analysis={null} report={{ incidentId: 'incident-1', classification: 'Credential Phishing', severity: 'High', confidence: 0.9, attackTechniques: [], possibleImpact: [], uncertainties: [], taskResults: [], findings: [{ id: 'finding-1', title: 'Sender mismatch', description: 'The sender did not match the brand.', severity: 'High', confidence: 0.9, evidenceSource: 'EmailMetadataTool', evidenceReference: 'email:from', evidence: 'alerts@example-login.test', findingType: 'SenderMismatch', taskId: 'task-1' }] }} />);

    expect(screen.getByRole('heading', { name: 'Spotting fake sign-in notices' })).toBeInTheDocument();
    expect(screen.getByText('What Happened')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Show the evidence' }));
    expect(screen.getByText('alerts@example-login.test')).toBeInTheDocument();

    await user.click(screen.getByRole('radio', { name: 'Sender domain' }));
    await user.click(screen.getByRole('button', { name: 'Check Answer' }));
    expect(screen.getByText('Correct.', { exact: false })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Next Question' }));

    await user.click(screen.getByRole('radio', { name: 'No' }));
    await user.click(screen.getByRole('button', { name: 'Check Answer' }));
    await user.click(screen.getByRole('button', { name: 'See Results' }));
    expect(screen.getByText('Knowledge Check Complete')).toBeInTheDocument();
    expect(screen.getByText('2 / 2 correct')).toBeInTheDocument();
    expect(screen.getByText('Pause when a message creates urgency.')).toBeInTheDocument();
  });
});