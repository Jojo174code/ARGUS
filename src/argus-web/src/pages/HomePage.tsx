import { Link } from 'react-router-dom';

export function HomePage() {
  return (
    <section className="hero">
      <div className="hero-glow" aria-hidden="true" />
      <h1>ARGUS Incident Response</h1>
      <p>
        Visual phishing triage for community organizations. Submit incidents, upload
        <span className="mono-text"> .eml </span>
        evidence, and follow a step-by-step guide built for non-technical teams.
      </p>
      <div className="hero-actions">
        <Link to="/new-incident" className="button-primary">
          Start New Incident
        </Link>
      </div>
    </section>
  );
}
