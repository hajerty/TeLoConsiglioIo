export type Role = 'Admin' | 'Consigliere';

export interface User {
  id: string;
  email: string;
  fullName: string;
  comune?: string | null;
  partito?: string | null;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  user: User;
}

export interface ProvidersDto {
  password: boolean;
  google: boolean;
  microsoft: boolean;
}

export type ActType = 'Mozione' | 'OrdineDelGiorno' | 'Delibera' | 'Emendamento' | 'Comunicazione';
export type ActStatus = 'Bozza' | 'InRevisione' | 'Pronto' | 'Presentato' | 'Archiviato';

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
}

export interface SuggestedRef {
  citation: string;
  description: string;
}

export type DocumentType = 'Documento' | 'Delibera' | 'OrdineDelGiorno' | 'Verbale' | 'Allegato';

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

export type Decisione = 'Indecisa' | 'Favorevole' | 'Contraria' | 'Astenuto';

export interface AgendaItem {
  id: string;
  ordine: number;
  descrizione: string;
  decisione: Decisione;
  motivazione: string;
  actId?: string | null;
  assignedUsers: AssignedUser[];
}

export interface SittingDetail {
  id: string;
  data: string;
  luogo: string;
  titolo: string;
  items: AgendaItem[];
}

export interface PoliticalProfile {
  lineaPoliticaMd: string;
  puntiEvidenza: string[];
}

export interface ElectoralProgram {
  id: string;
  originalName: string;
  uploadedAt: string;
}
