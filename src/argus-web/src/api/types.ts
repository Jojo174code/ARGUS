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

export type ResponseEducationRunStatus = 'NotStarted' | 'Running' | 'Completed' | 'Failed';

export type ResponseActionType = 'Informational' | 'UserAction' | 'AdministratorAction' | 'ProfessionalEscalation';

export type EscalationLevel = 'None' | 'InternalIT' | 'ManagedServiceProvider' | 'CybersecurityProfessional' | 'LegalOrCompliance' | 'LawEnforcement';

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
  blocksInvestigation: boolean;
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

export interface ResponseAction {
  id: string;
  title: string;
  description: string;
  reason: string;
  priority: number;
  actionType: ResponseActionType;
  requiresHumanApproval: boolean;
  responsibleRole: string | null;
  supportingFindingIds: string[];
}

export interface EscalationRecommendation {
  level: EscalationLevel;
  reason: string;
  recommendedContact: string;
  urgent: boolean;
}

export interface WarningSign {
  title: string;
  explanation: string;
  supportingFindingIds: string[];
}

export interface EducationQuestion {
  id: string;
  question: string;
  options: string[];
  correctOptionIndex: number;
  explanation: string;
}

export interface EducationModule {
  title: string;
  audienceLevel: string;
  estimatedMinutes: number;
  learningObjective: string;
  explanation: string;
  warningSigns: WarningSign[];
  questions: EducationQuestion[];
  takeaways: string[];
}

export interface ResponseEducationPackage {
  incidentId: string;
  incidentClassification: string;
  overallPriority: RiskLevel;
  plainLanguageSummary: string;
  immediateActions: ResponseAction[];
  recoveryActions: ResponseAction[];
  preventionActions: ResponseAction[];
  escalationRecommendations: EscalationRecommendation[];
  education: EducationModule;
  assumptions: string[];
  limitations: string[];
}

export interface ResponseEducationPackageResponse {
  incidentId: string;
  status: ResponseEducationRunStatus;
  model: string;
  promptVersion: string;
  startedAt: string;
  completedAt: string | null;
  errorMessage: string | null;
  package: ResponseEducationPackage | null;
}

export type WorkflowRunStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'AwaitingInformation';

export interface WorkflowStageResult {
  stage: string;
  status: WorkflowRunStatus;
  startedAt: string | null;
  completedAt: string | null;
  summary: string | null;
  error: string | null;
}

export interface WorkflowSummaryMetrics {
  coordinatorTaskCount: number;
  investigatorFindingCount: number;
  unsupportedTaskCount: number;
  responseActionCount: number;
  quizQuestionCount: number;
  durationMilliseconds: number;
}

export interface AgenticWorkflowResultResponse {
  incidentId: string;
  workflowRunId: string;
  status: WorkflowRunStatus;
  stages: WorkflowStageResult[];
  startedAt: string;
  completedAt: string | null;
  failureStage: string | null;
  failureMessage: string | null;
  blockingMissingInformation: MissingInformationItem[];
  metrics: WorkflowSummaryMetrics;
}

export interface ValidationProblem {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export interface EducationChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface EducationChatRequest {
  question: string;
  history: EducationChatMessage[];
}

export interface EducationChatResponse {
  answer: string;
  suggestedNextStep: string | null;
  model: string;
  generatedAt: string;
}
