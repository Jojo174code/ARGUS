import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <section className="card">
      <h1>Page Not Found</h1>
      <p>The page you requested does not exist in this ARGUS frontend.</p>
      <Link to="/" className="button-primary">
        Return Home
      </Link>
    </section>
  );
}
