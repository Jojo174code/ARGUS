import { Link } from 'react-router-dom';

export function HomePage() {
  return (
    <section className="hero">
      <div className="hero-glow" aria-hidden="true" />
      <h1>ARGUS Phase 1</h1>
      <p>
        Deterministic phishing triage for community organizations. Submit incidents, upload
        <span className="mono-text"> .eml </span>
        evidence, run explainable analysis, and review MITRE ATT&CK technique mappings.
      </p>
      <div className="hero-actions">
        <Link to="/new-incident" className="button-primary">
          Start New Incident
        </Link>
      </div>
    </section>
  );
}
