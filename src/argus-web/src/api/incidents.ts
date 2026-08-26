import { apiFetch } from './client';
import type {
  AnalysisResult,
  AgenticWorkflowResultResponse,
  CoordinatorPlanResponse,
  CreateIncidentRequest,
  EvidenceItem,
  Incident,
  InvestigatorReportResponse,
  ResponseEducationPackageResponse,
  EducationChatRequest,
  EducationChatResponse,
} from './types';

export function createIncident(request: CreateIncidentRequest): Promise<Incident> {
  return apiFetch<Incident>('/api/incidents', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  });
}

export function getIncident(id: string): Promise<Incident> {
  return apiFetch<Incident>(`/api/incidents/${id}`);
}

export function uploadEmailEvidence(id: string, file: File): Promise<EvidenceItem> {
  const formData = new FormData();
  formData.append('file', file);

  return apiFetch<EvidenceItem>(`/api/incidents/${id}/evidence/email`, {
    method: 'POST',
    body: formData,
  });
}

export function analyzeIncident(id: string): Promise<AnalysisResult> {
  return apiFetch<AnalysisResult>(`/api/incidents/${id}/analyze`, {
    method: 'POST',
  });
}

export function getIncidentAnalysis(id: string): Promise<AnalysisResult> {
  return apiFetch<AnalysisResult>(`/api/incidents/${id}/analysis`);
}

export function generateCoordinatorPlan(id: string): Promise<CoordinatorPlanResponse> {
  return apiFetch<CoordinatorPlanResponse>(`/api/incidents/${id}/coordinator/plan`, {
    method: 'POST',
  });
}

export function getCoordinatorPlan(id: string): Promise<CoordinatorPlanResponse> {
  return apiFetch<CoordinatorPlanResponse>(`/api/incidents/${id}/coordinator/plan`);
}

export function runInvestigator(id: string): Promise<InvestigatorReportResponse> {
  return apiFetch<InvestigatorReportResponse>(`/api/incidents/${id}/investigator/run`, {
    method: 'POST',
  });
}

export function getInvestigatorReport(id: string): Promise<InvestigatorReportResponse> {
  return apiFetch<InvestigatorReportResponse>(`/api/incidents/${id}/investigator/report`);
}

export function generateResponseEducationPackage(id: string): Promise<ResponseEducationPackageResponse> {
  return apiFetch<ResponseEducationPackageResponse>(`/api/incidents/${id}/response/generate`, {
    method: 'POST',
  });
}

export function getResponseEducationPackage(id: string): Promise<ResponseEducationPackageResponse> {
  return apiFetch<ResponseEducationPackageResponse>(`/api/incidents/${id}/response`);
}

export function runFullWorkflow(id: string): Promise<AgenticWorkflowResultResponse> {
  return apiFetch<AgenticWorkflowResultResponse>(`/api/incidents/${id}/workflow/run`, {
    method: 'POST',
  });
}

export function getWorkflow(id: string): Promise<AgenticWorkflowResultResponse> {
  return apiFetch<AgenticWorkflowResultResponse>(`/api/incidents/${id}/workflow`);
}

export function chatEducationAssistant(id: string, request: EducationChatRequest): Promise<EducationChatResponse> {
  return apiFetch<EducationChatResponse>(`/api/incidents/${id}/education/chat`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  });
}
