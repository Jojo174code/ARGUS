import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { createIncident } from '../api/incidents';
import type { OrganizationType, TechnicalSkillLevel } from '../api/types';
import { ErrorBanner } from '../components/ErrorBanner';

const organizationTypes: OrganizationType[] = [
  'Nonprofit',
  'Church',
  'School',
  'SmallBusiness',
  'CommunityGroup',
  'Other',
];

const technicalLevels: TechnicalSkillLevel[] = ['Beginner', 'Intermediate', 'Advanced'];

export function NewIncidentPage() {
  const [organizationName, setOrganizationName] = useState('');
  const [organizationType, setOrganizationType] = useState<OrganizationType>('Nonprofit');
  const [description, setDescription] = useState('');
  const [reportedBy, setReportedBy] = useState('');
  const [technicalSkillLevel, setTechnicalSkillLevel] = useState<TechnicalSkillLevel>('Beginner');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const incident = await createIncident({
        organizationName,
        organizationType,
        description,
        reportedBy,
        technicalSkillLevel,
      });
      navigate(`/incidents/${incident.id}`);
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'Failed to create incident.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="card new-incident-card">
      <header className="new-incident-heading">
        <p className="workflow-progress-kicker">Incident intake</p>
        <h1>New Incident</h1>
        <p>Create an incident record before uploading an email evidence file.</p>
      </header>
      {error ? <ErrorBanner message={error} /> : null}
      <form onSubmit={handleSubmit} className="form-grid new-incident-form">
        <label>
          Organization Name
          <input
            value={organizationName}
            onChange={(event) => setOrganizationName(event.target.value)}
            required
          />
        </label>

        <label>
          Organization Type
          <select
            value={organizationType}
            onChange={(event) => setOrganizationType(event.target.value as OrganizationType)}
          >
            {organizationTypes.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </label>

        <label>
          Reported By
          <input
            value={reportedBy}
            onChange={(event) => setReportedBy(event.target.value)}
            required
          />
        </label>

        <label>
          Technical Skill Level
          <select
            value={technicalSkillLevel}
            onChange={(event) => setTechnicalSkillLevel(event.target.value as TechnicalSkillLevel)}
          >
            {technicalLevels.map((level) => (
              <option key={level} value={level}>
                {level}
              </option>
            ))}
          </select>
        </label>

        <label className="full-row">
          Description
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            rows={4}
            required
          />
        </label>

        <button type="submit" className="button-primary" disabled={submitting}>
          {submitting ? 'Creating...' : 'Create Incident'}
        </button>
      </form>
    </section>
  );
}
