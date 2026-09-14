import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { IncidentExecutiveSummary } from '../components/IncidentExecutiveSummary';
import { IncidentEvidencePage } from '../pages/IncidentEvidencePage';

function jsonResponse(payload: unknown, status = 200): Response {
  return new Response(JSON.stringify(payload), { status, headers: { 'Content-Type': 'application/json' } });
}

const incident = {
  id: 'incident-ui', organizationName: 'ARGUS', organizationType: 'Church', description: 'Suspicious email', reportedBy: 'Test',
  createdAt: '2026-09-09T00:00:00Z', status: 'Submitted', technicalSkillLevel: 'Beginner', evidenceItems: [],
};

describe('incident workspace UI', () => {
  afterEach(() => vi.restoreAllMocks());

  it('derives the executive summary from persisted response data', () => {
    render(<IncidentExecutiveSummary analysis={null} report={{ status: 'Completed', report: { classification: 'Credential phishing', severity: 'High', confidence: 0.88, findings: [{ id: 'finding-1' }] } } as never} responsePack={{ package: { incidentClassification: 'Business email compromise', overallPriority: 'Critical', plainLanguageSummary: 'An impersonation attempt targeted staff payroll credentials.', immediateActions: [{ id: 'action-1', title: 'Reset affected passwords' }] } } as never} />);

    expect(screen.getByRole('heading', { name: 'Business email compromise' })).toBeInTheDocument();
    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText('Reset affected passwords')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'View Detailed Findings' })).toHaveAttribute('href', '#top-findings');
  });

  it('validates selections and shows an upload-ready evidence preview', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: string | URL) => String(input).endsWith('/api/incidents/incident-ui') ? jsonResponse(incident) : jsonResponse({}, 404)));
    const { container } = render(<MemoryRouter initialEntries={['/incidents/incident-ui/evidence']}><Routes><Route path="/incidents/:id/evidence" element={<IncidentEvidencePage />} /></Routes></MemoryRouter>);

    expect(await screen.findByText('No evidence uploaded yet')).toBeInTheDocument();
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [new File(['test'], 'message.txt', { type: 'text/plain' })] } });
    expect(await screen.findByText('Please choose an .eml file.')).toBeInTheDocument();

    fireEvent.change(input, { target: { files: [new File(['email'], 'suspicious.eml', { type: 'message/rfc822' })] } });
    expect(await screen.findByRole('heading', { name: 'suspicious.eml' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Upload Evidence' })).toBeEnabled();
  });
});