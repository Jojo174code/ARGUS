import type { ExtractedUrl } from '../api/types';

export function UrlTable({ urls }: { urls: ExtractedUrl[] }) {
  return (
    <section className="card">
      <h2>Extracted URLs</h2>
      {urls.length === 0 ? (
        <p>No URLs detected in the message body.</p>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>URL</th>
                <th>Source</th>
                <th>Display Text</th>
              </tr>
            </thead>
            <tbody>
              {urls.map((url, index) => (
                <tr key={`${url.url}-${index}`}>
                  <td className="mono-text">{url.url}</td>
                  <td>{url.source}</td>
                  <td>{url.displayText ?? 'None'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
