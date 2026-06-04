import { api } from './client';
import type {
  ActDetail,
  ActListItem,
  ActType,
  ActStatus,
  AuthResponse,
  DocumentDetail,
  DocumentItem,
  DocumentSummary,
  ElectoralProgram,
  PoliticalProfile,
  ProvidersDto,
  SittingDetail,
  SittingListItem,
  SuggestedRef,
  User,
  AgendaItem,
  Decisione,
} from './types';

// --- AUTH ---
export const authApi = {
  providers: () => api.get<ProvidersDto>('/api/auth/providers').then((r) => r.data),
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/api/auth/login', { email, password }).then((r) => r.data),
  register: (dto: { email: string; password: string; fullName: string; comune?: string; partito?: string }) =>
    api.post<AuthResponse>('/api/auth/register', dto).then((r) => r.data),
  me: () => api.get<User>('/api/auth/me').then((r) => r.data),
};

// --- PROFILE ---
export const profileApi = {
  getPolitical: () => api.get<PoliticalProfile>('/api/profile/political').then((r) => r.data),
  putPolitical: (dto: PoliticalProfile) =>
    api.put<PoliticalProfile>('/api/profile/political', dto).then((r) => r.data),
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
  create: (dto: { tipo: ActType; titolo: string; oggetto: string; contextNotes?: string; bodyMd?: string; parentActId?: string }) =>
    api.post<ActDetail>('/api/acts', dto).then((r) => r.data),
  update: (id: string, dto: { titolo: string; oggetto: string; contextNotes?: string; bodyMd: string; status: ActStatus }) =>
    api.put<ActDetail>(`/api/acts/${id}`, dto).then((r) => r.data),
  remove: (id: string) => api.delete(`/api/acts/${id}`).then((r) => r.data),
  aiDraft: (id: string, additionalInstructions?: string) =>
    api
      .post<{ text: string }>(`/api/acts/${id}/ai-draft`, { additionalInstructions })
      .then((r) => r.data),
  suggestLegalRefs: (id: string, text?: string) =>
    api
      .post<{ suggestions: SuggestedRef[] }>(`/api/acts/${id}/legal-refs/suggest`, { text })
      .then((r) => r.data),
  insertLegalRefs: (id: string, referenceIds: string[], mode: 'append' | 'placeholder' = 'append') =>
    api
      .post<ActDetail>(`/api/acts/${id}/legal-refs/insert`, { referenceIds, mode })
      .then((r) => r.data),
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
};

// --- USERS ---
export const usersApi = {
  list: () => api.get<User[]>('/api/users').then((r) => r.data),
};
