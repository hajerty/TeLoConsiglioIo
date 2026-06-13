import type { AxiosError } from 'axios';

interface ApiErrorBody {
  error?: string;
  message?: string;
  spentUsd?: number;
  limitUsd?: number;
}

export function getAIErrorMessage(err: unknown): string {
  const ax = err as AxiosError<ApiErrorBody>;
  const status = ax?.response?.status;
  const body = ax?.response?.data;
  const code = body?.error;

  if (status === 429 && code === 'ai_daily_quota_exceeded') {
    return 'Limite giornaliero AI raggiunto, riprova domani.';
  }
  if (status === 429 && code === 'budget_exceeded') {
    const spent = body?.spentUsd?.toFixed(2);
    const limit = body?.limitUsd?.toFixed(2);
    return `Budget AI mensile esaurito ($${spent} / $${limit}). Contatta l'amministratore.`;
  }
  if (status === 503) {
    return 'Servizio AI non configurato. Imposta GEMINI_API_KEY per abilitare le funzioni AI.';
  }
  return body?.message || body?.error || 'Errore durante la richiesta AI.';
}
