export type Role = 'Admin' | 'Consigliere' | 'Capogruppo' | 'Vice';

export interface User {
  id: string;
  email: string;
  fullName: string;
  comune?: string | null;
  partito?: string | null;
  gruppo?: string | null;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  user: User;
}

export interface ProvidersDto {
  email: boolean;
  google: boolean;
  microsoft: boolean;
}

export type ActType = 'Mozione' | 'OrdineDelGiorno' | 'Delibera' | 'Emendamento';
export type ActStatus = 'Bozza' | 'Depositato' | 'Approvato' | 'Respinto';

export interface ActListItem {
  id: string;
  tipo: ActType;
  titolo: string;
  oggetto: string;
  status: ActStatus;
  createdAt: string;
  updatedAt: string;
}

export interface ActRevision {
  id: string;
  bodyMd: string;
  createdAt: string;
  authorId: string;
}

export interface LegalRef {
  id: string;
  citation: string;
  description: string;
  inserted: boolean;
  confirmedAt?: string | null;
}

export interface ActDetail {
  id: string;
  tipo: ActType;
  titolo: string;
  oggetto: string;
  contextNotes?: string | null;
  bodyMd: string;
  status: ActStatus;
  parentActId?: string | null;
  createdAt: string;
  updatedAt: string;
  revisions: ActRevision[];
  legalReferences: LegalRef[];
  referenceUrls: string[];
  referenceNotesMd?: string | null;
}

export interface SuggestedRef {
  citation: string;
  description: string;
}

export type DocumentType = 'Delibera' | 'Verbale' | 'Documento' | 'Altro';

export interface DocumentItem {
  id: string;
  originalName: string;
  type: DocumentType;
  createdAt: string;
  hasSummary: boolean;
}

export interface DocumentSummary {
  summaryMd: string;
  keyPoints: string[];
  criticities: string[];
  generatedAt: string;
}

export interface DocumentDetail {
  id: string;
  originalName: string;
  type: DocumentType;
  createdAt: string;
  extractedText: string;
  summary?: DocumentSummary | null;
}

export interface SittingListItem {
  id: string;
  data: string;
  luogo: string;
  titolo: string;
}

export interface AssignedUser {
  userId: string;
  email: string;
  fullName: string;
}

export type Decisione = 'DaDecidere' | 'Approvare' | 'Respingere' | 'Astenersi';

export type AgendaItemStatus = 'DaAnalizzare' | 'Analizzata' | 'ApprovataPerSeduta';

export interface AgendaItem {
  id: string;
  ordine: number;
  descrizione: string;
  decisione: Decisione;
  motivazione: string;
  actId?: string | null;
  documentId?: string | null;
  status: AgendaItemStatus;
  assignedUsers: AssignedUser[];
}

export interface Invitation {
  id: string;
  token: string;
  email: string;
  nome: string;
  cognome: string;
  comune?: string | null;
  gruppo?: string | null;
  createdAt: string;
  expiresAt: string;
  consumedAt: string | null;
  revokedAt: string | null;
  status: 'Pending' | 'Consumed' | 'Expired' | 'Revoked';
}

export interface InvitationPublic {
  email: string;
  nome: string;
  cognome: string;
  comune?: string | null;
  gruppo?: string | null;
  status: 'Pending' | 'Consumed' | 'Expired' | 'Revoked';
}

export interface SittingDetail {
  id: string;
  data: string;
  luogo: string;
  titolo: string;
  items: AgendaItem[];
}

export type LineaPoliticaSource = 'Partito' | 'Manuale';

export interface PoliticalProfile {
  lineaPoliticaMd: string;
  argomentiForti: string[];
  temiInteresse: string[];
  lineaPoliticaSource: LineaPoliticaSource;
}

export interface PartySummary {
  key: string;
  fullName: string;
}

export interface PartyManifest {
  key: string;
  fullName: string;
  lineaPoliticaMd: string;
}

export interface ActAttachment {
  id: string;
  originalName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
}

export interface ElectoralProgram {
  id: string;
  originalName: string;
  uploadedAt: string;
}

export interface UserPick {
  id: string;
  displayName: string;
}

// --- DASHBOARD ---
export interface DocumentListItem {
  id: string;
  originalName: string;
  type: DocumentType;
  createdAt: string;
  hasSummary: boolean;
}

export interface NextSitting {
  id: string;
  data: string;
  luogo: string;
  titolo: string;
  agendaCount: number;
  daAnalizzareCount: number;
}

export interface DocToAnalyze {
  sittingId: string;
  sittingData: string;
  sittingTitolo: string;
  agendaItemId: string;
  descrizione: string;
  documentId: string | null;
}

export interface DashboardCounters {
  actsBozza: number;
  upcomingSittings: number;
  pendingInvitations?: number | null;
}

export interface DashboardPayload {
  recentDocuments: DocumentListItem[];
  nextSitting: NextSitting | null;
  documentsToAnalyze: DocToAnalyze[];
  counters: DashboardCounters;
}

// --- SITTINGS QUERY PARAMS ---
export interface SittingsQueryParams {
  from?: string;
  to?: string;
  q?: string;
  period?: 'All' | 'Past' | 'Upcoming';
  page?: number;
  pageSize?: number;
}

// --- DOCUMENT SUGGESTION ---
export interface DocumentSuggestion {
  agendaItemId: string;
  sittingId: string;
  sittingData: string;
  sittingTitolo: string;
  descrizione: string;
  documentId: string;
  documentName: string;
  score: number;
}

// --- PDF IMPORT ---
export interface ParsedAgendaItem {
  ordine: number;
  descrizione: string;
}

export interface SittingParsed {
  data: string | null;
  luogo: string;
  titolo: string;
  agendaItems: ParsedAgendaItem[];
}

// --- INVITATION WITH EMAIL STATUS ---
export interface InvitationCreated {
  token: string;
  url: string;
  emailSent: boolean;
}

// --- NOTIFICATION PREFERENCES ---
export interface NotificationPreferences {
  emailNotificationsEnabled: boolean;
}
