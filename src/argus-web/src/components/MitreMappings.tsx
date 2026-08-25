import type { MitreAttackTechnique } from '../api/types';

export function MitreMappings({ techniques }: { techniques: MitreAttackTechnique[] }) {
  return (
    <section className="card">
      <h2>MITRE ATT&CK Mappings</h2>
      {techniques.length === 0 ? (
        <p>No MITRE mappings generated for this analysis.</p>
      ) : (
        <ul className="mitre-list">
          {techniques.map((technique) => (
            <li key={technique.techniqueId}>
              <strong>{technique.techniqueId}</strong>
              <span>{technique.name}</span>
              <p>{technique.description}</p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
