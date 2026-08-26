import { useCallback, useMemo, useState } from 'react';
import {
  Background,
  Controls,
  Handle,
  Position,
  ReactFlow,
  type Edge,
  type Node,
  type NodeProps,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import type { AnalysisResult, InvestigatorReportResponse, ResponseEducationPackageResponse } from '../api/types';
import { buildIncidentMap, getIncidentMapFocus, type IncidentMapNode, type IncidentMapNodeKind } from './incident-map-data';

interface IncidentMapProps {
  analysis: AnalysisResult | null;
  investigatorReport: InvestigatorReportResponse | null;
  responsePackage: ResponseEducationPackageResponse | null;
}

interface FlowNodeData extends Record<string, unknown> {
  mapNode: IncidentMapNode;
  focused: boolean;
  selected: boolean;
  onSelect: (id: string) => void;
}

const layerX: Record<IncidentMapNodeKind, number> = {
  evidence: 0,
  finding: 310,
  action: 620,
};

function IncidentMapNodeCard({ data }: NodeProps<Node<FlowNodeData>>) {
  const { mapNode, focused, selected, onSelect } = data;
  const action = mapNode.action;
  const reportMitre = mapNode.reportMitreTechniqueIds?.slice(0, 2).join(', ');

  return (
    <button
      type="button"
      className={`incident-map-node incident-map-node-${mapNode.kind}${focused ? '' : ' is-muted'}${selected ? ' is-selected' : ''}`}
      onClick={() => onSelect(mapNode.id)}
      aria-label={`Select ${mapNode.kind}: ${mapNode.label}`}
    >
      <Handle type="target" position={Position.Left} isConnectable={false} />
      <span className="incident-map-node-kind">{mapNode.kind}</span>
      <strong>{mapNode.label}</strong>
      {mapNode.kind === 'evidence' && mapNode.scoreContribution ? <small>+{mapNode.scoreContribution} risk points</small> : null}
      {mapNode.kind === 'finding' && reportMitre ? <small>Report MITRE {reportMitre}</small> : null}
      {action ? (
        <small>
          Priority {action.priority} · {action.actionType}
          {action.requiresHumanApproval ? ' · Approval required' : ''}
        </small>
      ) : null}
      <Handle type="source" position={Position.Right} isConnectable={false} />
    </button>
  );
}

const nodeTypes = { incidentMapNode: IncidentMapNodeCard };

export function IncidentMap({ analysis, investigatorReport, responsePackage }: IncidentMapProps) {
  const data = useMemo(
    () => buildIncidentMap({ analysis, investigatorReport, responsePackage }),
    [analysis, investigatorReport, responsePackage],
  );
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const focus = useMemo(() => getIncidentMapFocus(data, selectedNodeId), [data, selectedNodeId]);
  const selectedNode = data.nodes.find((node) => node.id === selectedNodeId) ?? null;
  const selectNode = useCallback((id: string) => setSelectedNodeId(id), []);

  const flowNodes = useMemo<Node<FlowNodeData>[]>(() => {
    const counters: Record<IncidentMapNodeKind, number> = { evidence: 0, finding: 0, action: 0 };
    return data.nodes.map((mapNode) => {
      const index = counters[mapNode.kind]++;
      return {
        id: mapNode.id,
        type: 'incidentMapNode',
        position: { x: layerX[mapNode.kind], y: index * 145 },
        data: {
          mapNode,
          focused: focus?.has(mapNode.id) ?? true,
          selected: selectedNodeId === mapNode.id,
          onSelect: selectNode,
        },
        focusable: true,
        ariaLabel: `${mapNode.kind}: ${mapNode.label}`,
      };
    });
  }, [data.nodes, focus, selectNode, selectedNodeId]);

  const flowEdges = useMemo<Edge[]>(() => data.edges.map((edge) => {
    const focused = !selectedNodeId || edge.source === selectedNodeId || edge.target === selectedNodeId;
    return {
      ...edge,
      type: 'smoothstep',
      animated: focused && Boolean(selectedNodeId),
      style: { stroke: focused ? '#bd1e2d' : '#b9aaac', strokeWidth: focused ? 2.5 : 1.2, opacity: focused ? 1 : 0.22 },
    };
  }), [data.edges, selectedNodeId]);

  const relationshipSummary = data.nodes
    .filter((node) => node.kind === 'finding')
    .map((finding) => {
      const evidence = data.edges
        .filter((edge) => edge.target === finding.id)
        .map((edge) => data.nodes.find((node) => node.id === edge.source)?.label)
        .filter(Boolean);
      const actions = data.edges
        .filter((edge) => edge.source === finding.id)
        .map((edge) => data.nodes.find((node) => node.id === edge.target)?.label)
        .filter(Boolean);
      return `${finding.label} is supported by ${evidence.length ? evidence.join(', ') : 'no graphable evidence reference'} and supports ${actions.length ? actions.join(', ') : 'no linked actions'}.`;
    });

  const hasEvidence = data.nodes.some((node) => node.kind === 'evidence');
  const hasFindings = data.nodes.some((node) => node.kind === 'finding');
  const hasActions = data.nodes.some((node) => node.kind === 'action');

  return (
    <section className="incident-map" aria-labelledby="incident-map-heading">
      <div className="row-between wrap-gap">
        <div>
          <h3 id="incident-map-heading">ARGUS Incident Map</h3>
          <p className="soft-label">Select any node to trace how ARGUS moved from evidence to action.</p>
        </div>
        {selectedNodeId ? <button type="button" className="incident-map-reset" onClick={() => setSelectedNodeId(null)}>Reset selection</button> : null}
      </div>

      {!hasEvidence ? <p className="incident-map-message">Evidence collected. Run deterministic analysis to map triggered signals.</p> : null}
      {hasEvidence && !hasFindings ? <p className="incident-map-message">Evidence collected. Run the Investigator to connect evidence to findings.</p> : null}
      {hasFindings && !hasActions ? <p className="incident-map-message">Evidence and findings are shown. Generate Response &amp; Learning to connect recommended actions.</p> : null}

      {data.nodes.length > 0 ? (
        <div className="incident-map-canvas" aria-label="Evidence, findings, and action relationship map">
          <div className="incident-map-columns" aria-hidden="true"><span>Evidence</span><span>Findings</span><span>Actions</span></div>
          <ReactFlow
            nodes={flowNodes}
            edges={flowEdges}
            nodeTypes={nodeTypes}
            fitView
            fitViewOptions={{ padding: 0.2 }}
            nodesDraggable={false}
            nodesConnectable={false}
            elementsSelectable={false}
            panOnDrag
            zoomOnDoubleClick={false}
            proOptions={{ hideAttribution: true }}
          >
            <Background color="#e5d7d8" gap={18} size={1} />
            <Controls showInteractive={false} />
          </ReactFlow>
        </div>
      ) : null}

      {selectedNode ? <IncidentMapDetail node={selectedNode} /> : null}
      <div className="sr-only" aria-live="polite">
        {relationshipSummary.map((summary) => <p key={summary}>{summary}</p>)}
      </div>
    </section>
  );
}

function IncidentMapDetail({ node }: { node: IncidentMapNode }) {
  const action = node.action;
  const finding = node.finding;
  return (
    <aside className="incident-map-detail" aria-live="polite">
      <span className="incident-map-node-kind">{node.kind}</span>
      <h4>{node.label}</h4>
      <p>{node.description}</p>
      {node.kind === 'evidence' && node.scoreContribution ? <p><strong>Risk contribution:</strong> {node.scoreContribution} points</p> : null}
      {finding ? (
        <>
          <p><strong>Evidence reference:</strong> {finding.evidenceReference}</p>
          <p><strong>Report-level MITRE ATT&amp;CK:</strong> {node.reportMitreTechniqueIds?.join(', ') || 'No mapping recorded'}</p>
        </>
      ) : null}
      {action ? (
        <>
          <p><strong>Priority:</strong> {action.priority}</p>
          <p><strong>Type:</strong> {action.actionType}</p>
          <p><strong>Human approval:</strong> {action.requiresHumanApproval ? 'Required' : 'Not required'}</p>
          <p><strong>Why:</strong> {action.reason}</p>
        </>
      ) : null}
    </aside>
  );
}