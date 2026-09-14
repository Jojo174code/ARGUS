import { NavLink } from 'react-router-dom';

interface IncidentWorkspaceNavProps {
  incidentId: string;
}

export function IncidentWorkspaceNav({ incidentId }: IncidentWorkspaceNavProps) {
  const basePath = `/incidents/${incidentId}`;

  return (
    <nav className="workspace-nav" aria-label="Incident workspace navigation">
      <NavLink to={basePath} end className={({ isActive }) => (isActive ? 'workspace-link active' : 'workspace-link')}>
        <span className="workspace-link-dot" aria-hidden="true" />Guide
      </NavLink>
      <NavLink to={`${basePath}/evidence`} className={({ isActive }) => (isActive ? 'workspace-link active' : 'workspace-link')}>
        <span className="workspace-link-dot" aria-hidden="true" />Evidence
      </NavLink>
      <NavLink to={`${basePath}/results`} className={({ isActive }) => (isActive ? 'workspace-link active' : 'workspace-link')}>
        <span className="workspace-link-dot" aria-hidden="true" />Results
      </NavLink>
      <NavLink to={`${basePath}/education`} className={({ isActive }) => (isActive ? 'workspace-link active' : 'workspace-link')}>
        <span className="workspace-link-dot" aria-hidden="true" />Education
      </NavLink>
      <NavLink to={`${basePath}/details`} className={({ isActive }) => (isActive ? 'workspace-link active' : 'workspace-link')}>
        <span className="workspace-link-dot" aria-hidden="true" />Detailed View
      </NavLink>
    </nav>
  );
}
