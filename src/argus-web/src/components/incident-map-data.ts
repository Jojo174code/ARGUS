import type {
  AnalysisResult,
  InvestigationFinding,
  InvestigatorReportResponse,
  ResponseAction,
  ResponseEducationPackageResponse,
} from '../api/types';

export type IncidentMapNodeKind = 'evidence' | 'finding' | 'action';

export interface IncidentMapNode {
  id: string;
  kind: IncidentMapNodeKind;
  label: string;
  description: string;
  reference?: string;
  scoreContribution?: number;
  severity?: string;
  reportMitreTechniqueIds?: string[];
  action?: ResponseAction;
  finding?: InvestigationFinding;
}

export interface IncidentMapEdge {
  id: string;
  source: string;
  target: string;
}

export interface IncidentMapData {
  nodes: IncidentMapNode[];
  edges: IncidentMapEdge[];
}

interface BuildIncidentMapInput {
  analysis: AnalysisResult | null;
  investigatorReport: InvestigatorReportResponse | null;
  responsePackage: ResponseEducationPackageResponse | null;
}

export function buildIncidentMap({ analysis, investigatorReport, responsePackage }: BuildIncidentMapInput): IncidentMapData {
  const evidenceNodes = (analysis?.indicators ?? [])
    .filter((indicator) => indicator.triggered)
    .map<IncidentMapNode>((indicator) => ({
      id: `evidence:${indicator.ruleId}`,
      kind: 'evidence',
      label: indicator.name,
      description: indicator.description,
      reference: `indicator:${indicator.ruleId}`,
      scoreContribution: indicator.scoreContribution,
      severity: indicator.severity,
    }));

  const attackTechniqueIds = investigatorReport?.report?.attackTechniques.map((technique) => technique.techniqueId) ?? [];
  const findings = investigatorReport?.report?.findings ?? [];
  const findingNodes = findings.map<IncidentMapNode>((finding) => ({
    id: `finding:${finding.id}`,
    kind: 'finding',
    label: finding.title,
    description: finding.description,
    reference: finding.evidenceReference,
    severity: finding.severity,
    reportMitreTechniqueIds: attackTechniqueIds,
    finding,
  }));

  const actions = responsePackage?.package
    ? [
        ...responsePackage.package.immediateActions,
        ...responsePackage.package.recoveryActions,
        ...responsePackage.package.preventionActions,
      ]
    : [];
  const actionNodes = actions.map<IncidentMapNode>((action) => ({
    id: `action:${action.id}`,
    kind: 'action',
    label: action.title,
    description: action.description,
    action,
  }));

  const knownEvidenceReferences = new Map(evidenceNodes.map((node) => [node.reference, node.id]));
  const knownFindingIds = new Set(findings.map((finding) => finding.id));
  const evidenceEdges = findings.flatMap<IncidentMapEdge>((finding) => {
    const source = knownEvidenceReferences.get(finding.evidenceReference);
    return source ? [{ id: `${source}->finding:${finding.id}`, source, target: `finding:${finding.id}` }] : [];
  });
  const actionEdges = actions.flatMap<IncidentMapEdge>((action) =>
    action.supportingFindingIds
      .filter((findingId) => knownFindingIds.has(findingId))
      .map((findingId) => ({
        id: `finding:${findingId}->action:${action.id}`,
        source: `finding:${findingId}`,
        target: `action:${action.id}`,
      })),
  );

  return { nodes: [...evidenceNodes, ...findingNodes, ...actionNodes], edges: [...evidenceEdges, ...actionEdges] };
}

export function getIncidentMapFocus(data: IncidentMapData, selectedNodeId: string | null): Set<string> | null {
  if (!selectedNodeId) {
    return null;
  }

  const focus = new Set([selectedNodeId]);
  for (const edge of data.edges) {
    if (edge.source === selectedNodeId) {
      focus.add(edge.target);
    }
    if (edge.target === selectedNodeId) {
      focus.add(edge.source);
    }
  }
  return focus;
}