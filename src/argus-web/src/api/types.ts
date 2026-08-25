export type OrganizationType =
  | 'Nonprofit'
  | 'Church'
  | 'School'
  | 'SmallBusiness'
  | 'CommunityGroup'
  | 'Other';

export type TechnicalSkillLevel = 'Beginner' | 'Intermediate' | 'Advanced';

export type IncidentStatus =
  | 'Submitted'
  | 'Analyzing'
  | 'AwaitingInformation'
  | 'Completed'
  | 'Failed';

export type EvidenceType = 'EmailFile';

export type RiskLevel = 'Low' | 'Moderate' | 'High' | 'Critical';

export type RuleSeverity = 'Low' | 'Moderate' | 'High' | 'Critical';

export type CoordinatorRunStatus = 'NotStarted' | 'Running' | 'Completed' | 'Failed';

export type InvestigatorRunStatus = 'NotStarted' | 'Running' | 'Completed' | 'Failed';

export type AuthCheckVerdict =
  | 'Unknown'
  | 'Pass'
  | 'Fail'
  | 'SoftFail'
  | 'Neutral'
  | 'None'
  | 'Missing';

export interface CreateIncidentRequest {
  organizationName: string;
  organizationType: OrganizationType;
  description: string;
  reportedBy: string;
  technicalSkillLevel: TechnicalSkillLevel;
}

export interface EvidenceItem {
  id: string;
  incidentId: string;
  fileName: string;
  contentType: string;
  evidenceType: EvidenceType;
  fileSize: number;
  sha256: string;
  uploadedAt: string;
}

export interface Incident {
  id: string;
  organizationName: string;
  organizationType: OrganizationType;
  description: string;
  reportedBy: string;
  createdAt: string;
  status: IncidentStatus;
  technicalSkillLevel: TechnicalSkillLevel;
  evidenceItems: EvidenceItem[];
}

export interface ExtractedUrl {
  url: string;
  displayText: string | null;
  source: string;
}

export interface EmailAttachmentMetadata {
  fileName: string;
  contentType: string;
  size: number;
}

export interface EmailAuthenticationResults {
  spf: AuthCheckVerdict;
  dkim: AuthCheckVerdict;
  dmarc: AuthCheckVerdict;
  authenticationResultsHeaders: string[];
}

export interface ParsedEmail {
  displayName: string | null;
  fromAddress: string | null;
  replyToAddress: string | null;
  returnPath: string | null;
  subject: string | null;
  date: string | null;
  messageId: string | null;
  receivedHeaders: string[];
  authentication: EmailAuthenticationResults;
  urls: ExtractedUrl[];
  attachments: EmailAttachmentMetadata[];
  plainTextBody: string | null;
  htmlBody: string | null;
}

export interface PhishingRuleResult {
  ruleId: string;
  name: string;
  description: string;
  severity: RuleSeverity;
  scoreContribution: number;
  evidence: string | null;
  triggered: boolean;
}

export interface MitreAttackTechnique {
  techniqueId: string;
  name: string;
  description: string;
}

export interface AnalysisResult {
  incidentId: string;
  email: ParsedEmail;
  riskScore: number;
  riskLevel: RiskLevel;
  summary: string;
  ruleResults: PhishingRuleResult[];
  indicators: PhishingRuleResult[];
  mitreAttackMappings: MitreAttackTechnique[];
  analyzedAt: string;
}

export interface InvestigationTask {
  id: string;
  taskType: string;
  title: string;
  description: string;
  priority: number;
  requiredEvidence: string[];
  assignedCapability: string;
  completionCondition: string;
}

export interface MissingInformationItem {
  question: string;
  reason: string;
  required: boolean;
}

export interface InvestigationPlan {
  incidentId: string;
  incidentType: string;
  priority: string;
  readyForInvestigation: boolean;
  tasks: InvestigationTask[];
  missingInformation: MissingInformationItem[];
  assumptions: string[];
  safetyNotes: string[];
}

export interface CoordinatorPlanResponse {
  incidentId: string;
  status: CoordinatorRunStatus;
  model: string;
  promptVersion: string;
  startedAt: string;
  completedAt: string | null;
  errorMessage: string | null;
  plan: InvestigationPlan | null;
}

export interface InvestigationFinding {
  id: string;
  title: string;
  description: string;
  severity: RiskLevel;
  confidence: number;
  evidenceSource: string;
  evidenceReference: string;
  evidence: string;
  findingType: string;
  taskId: string | null;
}

export interface AttackTechniqueFinding {
  techniqueId: string;
  name: string;
  basis: string;
}

export interface TaskExecutionResult {
  taskId: string;
  taskType: string;
  status: string;
  toolsUsed: string[];
  evidenceReferences: string[];
  summary: string;
}

export interface InvestigationReport {
  incidentId: string;
  classification: string;
  severity: RiskLevel;
  confidence: number;
  findings: InvestigationFinding[];
  attackTechniques: AttackTechniqueFinding[];
  possibleImpact: string[];
  uncertainties: string[];
  taskResults: TaskExecutionResult[];
}

export interface InvestigatorReportResponse {
  incidentId: string;
  status: InvestigatorRunStatus;
  model: string;
  promptVersion: string;
  startedAt: string;
  completedAt: string | null;
  errorMessage: string | null;
  report: InvestigationReport | null;
}

export interface ValidationProblem {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}
