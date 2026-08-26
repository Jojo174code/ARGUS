import { useState } from 'react';
import type { SyntheticEvent } from 'react';
import { NewIncidentPage } from './NewIncidentPage';

export function NewIncidentEntryPage() {
  const [showIntro, setShowIntro] = useState(true);
  const [progress, setProgress] = useState(0);

  function handleVideoProgress(event: SyntheticEvent<HTMLVideoElement>): void {
    const { currentTime, duration } = event.currentTarget;
    if (Number.isFinite(duration) && duration > 0) {
      setProgress(Math.min(100, Math.round((currentTime / duration) * 100)));
    }
  }

  if (!showIntro) {
    return <NewIncidentPage />;
  }

  return (
    <section className="incident-intro" aria-labelledby="incident-intro-heading">
      <div className="incident-intro-copy">
        <img className="incident-intro-logo" src="/media/argus-agentic-logo-2026.png" alt="ARGUS logo" />
        <p className="incident-intro-eyebrow">Incident response workspace</p>
        <h1 id="incident-intro-heading">Preparing a secure incident record</h1>
        <p>ARGUS is ready to help you document and investigate suspicious email safely.</p>
      </div>
      <video
        className="incident-intro-video"
        src="/media/argus-intro.mp4"
        autoPlay
        muted
        playsInline
        onTimeUpdate={handleVideoProgress}
        onEnded={() => {
          setProgress(100);
          setShowIntro(false);
        }}
        aria-label="ARGUS loading animation"
      />
      <div className="incident-intro-actions">
        <div className="incident-intro-progress" aria-live="polite">
          <div className="incident-intro-progress-label">
            <span>Loading the new incident workspace...</span>
            <strong>{progress}%</strong>
          </div>
          <div className="incident-intro-progress-track" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={progress} aria-label="Loading progress">
            <span style={{ width: `${progress}%` }} />
          </div>
        </div>
        <button type="button" className="incident-intro-skip" onClick={() => setShowIntro(false)}>
          Skip intro
        </button>
      </div>
    </section>
  );
}