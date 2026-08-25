import type { ParsedEmail } from '../api/types';

function renderTimestamp(value: string | null): string {
  if (!value) {
    return 'Unknown';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

export function EmailSummary({ email }: { email: ParsedEmail }) {
  return (
    <section className="card">
      <h2>Email Summary</h2>
      <dl className="summary-grid">
        <div>
          <dt>From</dt>
          <dd>{email.fromAddress ?? 'Unknown'}</dd>
        </div>
        <div>
          <dt>Display Name</dt>
          <dd>{email.displayName ?? 'None'}</dd>
        </div>
        <div>
          <dt>Reply-To</dt>
          <dd>{email.replyToAddress ?? 'None'}</dd>
        </div>
        <div>
          <dt>Subject</dt>
          <dd>{email.subject ?? 'No subject'}</dd>
        </div>
        <div>
          <dt>Date</dt>
          <dd>{renderTimestamp(email.date)}</dd>
        </div>
        <div>
          <dt>Message-ID</dt>
          <dd className="mono-text">{email.messageId ?? 'Unknown'}</dd>
        </div>
      </dl>
    </section>
  );
}
