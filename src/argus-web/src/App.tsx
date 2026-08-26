import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { HomePage } from './pages/HomePage';
import { IncidentEvidencePage } from './pages/IncidentEvidencePage';
import { IncidentEducationPage } from './pages/IncidentEducationPage';
import { IncidentGuidePage } from './pages/IncidentGuidePage';
import { IncidentPage } from './pages/IncidentPage';
import { IncidentResultsPage } from './pages/IncidentResultsPage';
import { NewIncidentEntryPage } from './pages/NewIncidentEntryPage';
import { NotFoundPage } from './pages/NotFoundPage';

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<AppShell />}>
        <Route index element={<HomePage />} />
        <Route path="new-incident" element={<NewIncidentEntryPage />} />
        <Route path="incidents/:id" element={<IncidentGuidePage />} />
        <Route path="incidents/:id/evidence" element={<IncidentEvidencePage />} />
        <Route path="incidents/:id/results" element={<IncidentResultsPage />} />
        <Route path="incidents/:id/education" element={<IncidentEducationPage />} />
        <Route path="incidents/:id/details" element={<IncidentPage />} />
        <Route path="404" element={<NotFoundPage />} />
        <Route path="*" element={<Navigate to="/404" replace />} />
      </Route>
    </Routes>
  );
}
