import { useEffect, useRef, useState } from 'react';
import type { ChangeEvent, DragEvent, KeyboardEvent } from 'react';
import { useParams } from 'react-router-dom';
import { getIncident, uploadEmailEvidence } from '../api/incidents';
import type { Incident } from '../api/types';
import { ErrorBanner } from '../components/ErrorBanner';
import { IncidentWorkspaceNav } from '../components/IncidentWorkspaceNav';
import { LoadingBlock } from '../components/LoadingBlock';

const MAX_UPLOAD_SIZE_BYTES = 2_097_152;

function renderDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} bytes`;
  }

  return `${(bytes / 1024).toFixed(bytes < 10 * 1024 ? 1 : 0)} KB`;
}

function compactFingerprint(value: string): string {
  return value.length <= 16 ? value : `${value.slice(0, 8)}...${value.slice(-6)}`;
}

export function IncidentEvidencePage() {
  const { id } = useParams<{ id: string }>();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const [expandedFingerprints, setExpandedFingerprints] = useState<Set<string>>(new Set());
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!id) {
      setError('Missing incident id.');
      setLoading(false);
      return;
    }

    void (async () => {
      try {
        const nextIncident = await getIncident(id);
        setIncident(nextIncident);
      } catch (loadError) {
        setError(loadError instanceof Error ? loadError.message : 'Failed to load incident.');
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  function selectFile(nextFile: File | null): void {
    if (!nextFile) {
      setSelectedFile(null);
      return;
    }

    if (!nextFile.name.toLowerCase().endsWith('.eml')) {
      setError('Please choose an .eml file.');
      setSelectedFile(null);
      return;
    }

    if (nextFile.size > MAX_UPLOAD_SIZE_BYTES) {
      setError('This file is too large. Maximum size is 2 MB.');
      setSelectedFile(null);
      return;
    }

    setError(null);
    setSelectedFile(nextFile);
  }

  async function handleFileSelection(event: ChangeEvent<HTMLInputElement>): Promise<void> {
    selectFile(event.target.files?.[0] ?? null);
  }

  function handleDrop(event: DragEvent<HTMLDivElement>): void {
    event.preventDefault();
    setIsDragging(false);
    selectFile(event.dataTransfer.files[0] ?? null);
  }

  function handleDropzoneKeyDown(event: KeyboardEvent<HTMLDivElement>): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      inputRef.current?.click();
    }
  }

  async function handleUpload(): Promise<void> {
    if (!id || !selectedFile) {
      return;
    }

    setUploading(true);
    setError(null);

    try {
      await uploadEmailEvidence(id, selectedFile);
      setIncident(await getIncident(id));
      setSelectedFile(null);
    } catch (uploadError) {
      setError(uploadError instanceof Error ? uploadError.message : 'Upload failed.');
    } finally {
      setUploading(false);
    }
  }

  if (loading) {
    return <LoadingBlock message="Loading evidence workspace..." />;
  }

  if (!incident) {
    return (
      <section className="card">
        <h1>Incident Not Found</h1>
      </section>
    );
  }

  return (
    <section className="space-stack evidence-workspace">
      <IncidentWorkspaceNav incidentId={incident.id} />

      <article className="card mission-strip evidence-hero">
        <h1>Evidence Upload</h1>
        <p className="soft-label">Submit suspicious email evidence for ARGUS to analyze. Your file is preserved and fingerprinted before analysis.</p>
        <div className="evidence-trust-grid" aria-label="Evidence upload requirements">
          <div><span>EMAIL EVIDENCE</span><strong>.eml only</strong></div>
          <div><span>MAX SIZE</span><strong>2 MB</strong></div>
          <div><span>INTEGRITY</span><strong>SHA-256 verified</strong></div>
        </div>
      </article>

      {error ? <ErrorBanner message={error} /> : null}

      <article className="card space-stack">
        <div className="row-between wrap-gap">
          <div>
            <h2>Email Evidence</h2>
            <p className="soft-label">Choose the original suspicious message in standard email format.</p>
          </div>
        </div>
        <input ref={inputRef} className="sr-only" type="file" accept=".eml" onChange={(event) => void handleFileSelection(event)} />
        <div
          className={`evidence-dropzone${isDragging ? ' is-dragging' : ''}`}
          role="button"
          tabIndex={0}
          aria-label="Choose an .eml email evidence file"
          onClick={() => inputRef.current?.click()}
          onKeyDown={handleDropzoneKeyDown}
          onDragOver={(event) => { event.preventDefault(); setIsDragging(true); }}
          onDragLeave={() => setIsDragging(false)}
          onDrop={handleDrop}
        >
          <span className="evidence-dropzone-icon" aria-hidden="true">@</span>
          <strong>Drop an .eml file here</strong>
          <span>or</span>
          <button type="button" className="evidence-browse-button" onClick={(event) => { event.stopPropagation(); inputRef.current?.click(); }}>Browse Files</button>
          <small>Maximum file size: 2 MB</small>
        </div>
        {selectedFile ? (
          <section className="selected-evidence-card" aria-label="Selected evidence file">
            <div>
              <p className="workflow-progress-kicker">Ready To Upload</p>
              <h3>{selectedFile.name}</h3>
              <div className="meta-chip-row">
                <span className="meta-chip">{formatFileSize(selectedFile.size)}</span>
                <span className="meta-chip">Email Evidence</span>
                <span className="meta-chip">.eml</span>
              </div>
            </div>
            <div className="selected-evidence-actions">
              <button type="button" className="text-button" onClick={() => { setSelectedFile(null); if (inputRef.current) inputRef.current.value = ''; }}>Remove</button>
              <button type="button" className="button-primary" disabled={uploading} onClick={() => void handleUpload()}>{uploading ? 'Uploading...' : 'Upload Evidence'}</button>
            </div>
          </section>
        ) : null}
      </article>

      <article className="card space-stack">
        <h2>Uploaded Files</h2>
        {incident.evidenceItems.length === 0 ? (
          <section className="evidence-empty-state">
            <h3>No evidence uploaded yet</h3>
            <p>Upload the suspicious email that started this incident. ARGUS will analyze sender details, links, and phishing indicators.</p>
          </section>
        ) : (
          <ul className="evidence-card-grid">
            {incident.evidenceItems.map((item) => (
              <li key={item.id} className="evidence-file-card">
                <div className="row-between wrap-gap">
                  <span className="workflow-progress-kicker">Email Evidence</span>
                  <span className="evidence-verified">Verified</span>
                </div>
                <h3>{item.fileName}</h3>
                <p>{formatFileSize(item.fileSize)} · Uploaded {renderDate(item.uploadedAt)}</p>
                <div className="fingerprint-block">
                  <span>Integrity Fingerprint</span>
                  <code>{expandedFingerprints.has(item.id) ? item.sha256 : compactFingerprint(item.sha256)}</code>
                  <button type="button" className="text-button" onClick={() => setExpandedFingerprints((current) => {
                    const next = new Set(current);
                    next.has(item.id) ? next.delete(item.id) : next.add(item.id);
                    return next;
                  })}>{expandedFingerprints.has(item.id) ? 'Hide full fingerprint' : 'Show full fingerprint'}</button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </article>
    </section>
  );
}
