import { Link, NavLink, Outlet } from 'react-router-dom';

export function AppShell() {
  return (
    <div className="app-shell">
      <header className="topbar">
        <Link to="/" className="brand" aria-label="ARGUS home">
          <img className="brand-logo" src="/media/argus-agentic-logo-2026.png" alt="" />
          <span>
            <span className="brand-mark">ARGUS</span>
            <span className="brand-subtitle">Deterministic Email Triage</span>
          </span>
        </Link>
        <nav>
          <ul className="topnav">
            <li>
              <NavLink to="/" end>
                Home
              </NavLink>
            </li>
            <li>
              <NavLink to="/new-incident">New Incident</NavLink>
            </li>
          </ul>
        </nav>
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
