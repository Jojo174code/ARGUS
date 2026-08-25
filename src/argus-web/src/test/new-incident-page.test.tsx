import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NewIncidentPage } from '../pages/NewIncidentPage';

function jsonResponse(payload: unknown, status = 200): Response {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('NewIncidentPage', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('creates an incident and navigates to the incident page', async () => {
    const fetchMock = vi.fn(async (input: string | URL, init?: RequestInit) => {
      if (String(input).endsWith('/api/incidents') && init?.method === 'POST') {
        return jsonResponse({
          id: 'incident-123',
          organizationName: 'Hope Center',
          organizationType: 'Nonprofit',
          description: 'Suspicious payroll email',
          reportedBy: 'Maya',
          createdAt: '2026-01-01T00:00:00Z',
          status: 'Submitted',
          technicalSkillLevel: 'Beginner',
          evidenceItems: [],
        });
      }

      return jsonResponse({}, 404);
    });

    vi.stubGlobal('fetch', fetchMock);

    render(
      <MemoryRouter initialEntries={['/new-incident']}>
        <Routes>
          <Route path="/new-incident" element={<NewIncidentPage />} />
          <Route path="/incidents/:id" element={<div>Incident detail page</div>} />
        </Routes>
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText('Organization Name'), 'Hope Center');
    await userEvent.type(screen.getByLabelText('Reported By'), 'Maya');
    await userEvent.type(screen.getByLabelText('Description'), 'Suspicious payroll email');

    await userEvent.click(screen.getByRole('button', { name: 'Create Incident' }));

    expect(await screen.findByText('Incident detail page')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
