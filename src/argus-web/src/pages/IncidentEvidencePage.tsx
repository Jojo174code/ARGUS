import { useEffect, useState } from 'react';
import type { ChangeEvent } from 'react';
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

export function IncidentEvidencePage() {
  const { id } = useParams<{ id: string }>();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);

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

  async function handleFileSelection(event: ChangeEvent<HTMLInputElement>): Promise<void> {
    const nextFile = event.target.files?.[0] ?? null;
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
    <section className="space-stack">
      <IncidentWorkspaceNav incidentId={incident.id} />

      <article className="card mission-strip">
        <h1>Evidence Upload</h1>
        <p className="soft-label">Step 1: Upload one or more .eml files. This is the only file type needed.</p>
      </article>

      {error ? <ErrorBanner message={error} /> : null}

      <article className="card space-stack">
        <h2>Add Email File</h2>
        <input type="file" accept=".eml" onChange={(event) => void handleFileSelection(event)} />
        {selectedFile ? (
          <p>
            Ready: <strong>{selectedFile.name}</strong> ({selectedFile.size} bytes)
          </p>
        ) : null}
        <button type="button" className="button-primary" disabled={!selectedFile || uploading} onClick={() => void handleUpload()}>
          {uploading ? 'Uploading...' : 'Upload Email Evidence'}
        </button>
      </article>

      <article className="card space-stack">
        <h2>Uploaded Files</h2>
        {incident.evidenceItems.length === 0 ? (
          <p>No files uploaded yet.</p>
        ) : (
          <ul className="visual-list">
            {incident.evidenceItems.map((item) => (
              <li key={item.id} className="visual-item">
                <strong>{item.fileName}</strong>
                <div className="meta-chip-row">
                  <span className="meta-chip">{item.fileSize} bytes</span>
                  <span className="meta-chip">Uploaded {renderDate(item.uploadedAt)}</span>
                </div>
                <p className="mono-text">SHA-256: {item.sha256}</p>
              </li>
            ))}
          </ul>
        )}
      </article>
    </section>
  );
}
