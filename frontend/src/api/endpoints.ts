import { api } from './client';
import type {
  ActAttachment,
  ActDetail,
  ActListItem,
  ActType,
  ActStatus,
  AgendaItemStatus,
  AuthResponse,
  DocumentDetail,
  DocumentItem,
  DocumentSummary,
  ElectoralProgram,
  Invitation,
  InvitationPublic,
  PartyManifest,
  PartySummary,
  PoliticalProfile,
  ProvidersDto,
  SittingDetail,
  SittingListItem,
  SuggestedRef,
  User,
  UserPick,
  AgendaItem,
  Decisione,
} from './types';

// --- AUTH ---
export const authApi = {
  providers: () => api.get<ProvidersDto>('/api/auth/providers').then((r) => r.data),
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/api/auth/login', { email, password }).then((r) => r.data),
  register: (dto: {
    email: string;
    password: string;
    fullName: string;
    comune?: string;
    partito?: string;
    gruppo?: string;
    invitationToken?: string;
  }) => api.post<AuthResponse>('/api/auth/register', dto).then((r) => r.data),
  me: () => api.get<User>('/api/auth/me').then((r) => r.data),
  refresh: (refreshToken: string) =>
    api.post<AuthResponse>('/api/auth/refresh', { refreshToken }).then((r) => r.data),
  logout: (refreshToken?: string | null) =>
    api.post('/api/auth/logout', refreshToken ? { refreshToken } : {}).then((r) => r.data),
};

// --- PROFILE ---
export const profileApi = {
  getPolitical: () => api.get<PoliticalProfile>('/api/profile/political').then((r) => r.data),
  updatePolitical: (dto: Partial<PoliticalProfile>) =>
    api.put<PoliticalProfile>('/api/profile/political', dto).then((r) => r.data),
  /** @deprecated use updatePolitical */
  putPolitical: (dto: Partial<PoliticalProfile>) =>
    api.put<PoliticalProfile>('/api/profile/political', dto).then((r) => r.data),
  resetLineaPolitica: () =>
    api.post<PoliticalProfile>('/api/profile/political/reset-linea').then((r) => r.data),
  listPrograms: () => api.get<ElectoralProgram[]>('/api/profile/programs').then((r) => r.data),
  uploadProgram: (file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api
      .post<ElectoralProgram>('/api/profile/programs', fd, { headers: { 'Content-Type': 'multipart/form-data' } })
      .then((r) => r.data);
  },
  deleteProgram: (id: string) => api.delete(`/api/profile/programs/${id}`).then((r) => r.data),
};

// --- PARTY MANIFESTS ---
export const partyManifestsApi = {
  list: () => api.get<PartySummary[]>('/api/party-manifests').then((r) => r.data),
  get: (key: string) => api.get<PartyManifest>(`/api/party-manifests/${key}`).then((r) => r.data),
};

// --- DOCUMENTS ---
export const documentsApi = {
  list: (params?: { q?: string; type?: string }) =>
    api.get<DocumentItem[]>('/api/documents', { params }).then((r) => r.data),
  get: (id: string) => api.get<DocumentDetail>(`/api/documents/${id}`).then((r) => r.data),
  upload: (file: File, type = 'Documento') => {
    const fd = new FormData();
    fd.append('file', file);
    return api
      .post<DocumentItem>(`/api/documents?type=${type}`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data);
  },
  summarize: (id: string) => api.post<DocumentSummary>(`/api/documents/${id}/summarize`).then((r) => r.data),
  delete: (id: string) => api.delete(`/api/documents/${id}`).then((r) => r.data),
};

// --- ACTS ---
export const actsApi = {
  list: (params?: { tipo?: ActType; status?: ActStatus; q?: string }) =>
    api.get<ActListItem[]>('/api/acts', { params }).then((r) => r.data),
  get: (id: string) => api.get<ActDetail>(`/api/acts/${id}`).then((r) => r.data),
  create: (dto: {
    tipo: ActType;
    titolo: string;
    oggetto: string;
    contextNotes?: string;
    bodyMd?: string;
    parentActId?: string;
    referenceUrls?: string[];
    referenceNotesMd?: string;
  }) => api.post<ActDetail>('/api/acts', dto).then((r) => r.data),
  update: (id: string, dto: {
    titolo: string;
    oggetto: string;
    contextNotes?: string;
    bodyMd: string;
    status: ActStatus;
    referenceUrls?: string[];
    referenceNotesMd?: string | null;
  }) => api.put<ActDetail>(`/api/acts/${id}`, dto).then((r) => r.data),
  remove: (id: string) => api.delete(`/api/acts/${id}`).then((r) => r.data),
  aiDraft: (id: string, additionalInstructions?: string) =>
    api
      .post<{ text: string }>(`/api/acts/${id}/ai-draft`, { additionalInstructions })
      .then((r) => r.data),
  suggestLegalRefs: (id: string, text?: string) =>
    api
      .post<{ references: SuggestedRef[] }>(`/api/acts/${id}/legal-refs/suggest`, { text })
      .then((r) => r.data),
  insertLegalRefs: (id: string, referenceIds: string[], mode: 'append' | 'placeholder' = 'append') =>
    api
      .post<ActDetail>(`/api/acts/${id}/legal-refs/insert`, { referenceIds, mode })
      .then((r) => r.data),
  // Allegati
  listAttachments: (actId: string) =>
    api.get<ActAttachment[]>(`/api/acts/${actId}/attachments`).then((r) => r.data),
  uploadAttachment: (actId: string, file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api
      .post<ActAttachment>(`/api/acts/${actId}/attachments`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data);
  },
  downloadAttachment: (actId: string, attId: string) =>
    api
      .get(`/api/acts/${actId}/attachments/${attId}`, { responseType: 'blob' })
      .then((r) => r.data as Blob),
  deleteAttachment: (actId: string, attId: string) =>
    api.delete(`/api/acts/${actId}/attachments/${attId}`).then((r) => r.data),
  // Export PDF
  exportPdf: (actId: string) =>
    api
      .get(`/api/acts/${actId}/pdf`, { responseType: 'blob' })
      .then((r) => r.data as Blob),
};

// --- SITTINGS ---
export const sittingsApi = {
  list: () => api.get<SittingListItem[]>('/api/sittings').then((r) => r.data),
  get: (id: string) => api.get<SittingDetail>(`/api/sittings/${id}`).then((r) => r.data),
  create: (dto: { data: string; luogo: string; titolo: string }) =>
    api.post<SittingDetail>('/api/sittings', dto).then((r) => r.data),
  remove: (id: string) => api.delete(`/api/sittings/${id}`).then((r) => r.data),
  addAgenda: (
    sittingId: string,
    dto: { ordine: number; descrizione: string; decisione: Decisione; motivazione?: string; actId?: string; assignedUserIds?: string[] }
  ) => api.post<AgendaItem>(`/api/sittings/${sittingId}/agenda`, dto).then((r) => r.data),
  updateAgenda: (
    itemId: string,
    dto: { ordine: number; descrizione: string; decisione: Decisione; motivazione?: string; actId?: string; assignedUserIds?: string[] }
  ) => api.put<AgendaItem>(`/api/sittings/agenda/${itemId}`, dto).then((r) => r.data),
  removeAgenda: (itemId: string) => api.delete(`/api/sittings/agenda/${itemId}`).then((r) => r.data),
  uploadAgendaDocument: (itemId: string, file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api
      .post<AgendaItem>(`/api/sittings/agenda/${itemId}/document`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data);
  },
  updateAgendaStatus: (itemId: string, status: AgendaItemStatus) =>
    api.put<AgendaItem>(`/api/sittings/agenda/${itemId}/status`, { status }).then((r) => r.data),
};

// --- INVITATIONS ---
export const invitationsApi = {
  list: () => api.get<Invitation[]>('/api/invitations').then((r) => r.data),
  create: (req: { nome: string; cognome: string; email: string; gruppo?: string; comune?: string }) =>
    api.post<{ token: string; url: string }>('/api/invitations', req).then((r) => r.data),
  getPublic: (token: string) =>
    api.get<InvitationPublic>(`/api/invitations/${token}`).then((r) => r.data),
  remove: (id: string) => api.delete(`/api/invitations/${id}`).then((r) => r.data),
};

// --- USERS ---
export const usersApi = {
  list: () => api.get<UserPick[]>('/api/users').then((r) => r.data),
};
