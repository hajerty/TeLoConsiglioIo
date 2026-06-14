import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { invitationsApi } from '../api/endpoints';
import type { Invitation, InvitationCreated } from '../api/types';

type FilterStatus = 'Tutti' | 'Pending' | 'Consumed' | 'Expired' | 'Revoked';

const STATUS_LABELS: Record<string, string> = {
  Pending: 'In attesa',
  Consumed: 'Utilizzato',
  Expired: 'Scaduto',
  Revoked: 'Revocato',
};

const STATUS_COLORS: Record<string, string> = {
  Pending: 'bg-blue-100 text-blue-800',
  Consumed: 'bg-green-100 text-green-800',
  Expired: 'bg-slate-100 text-slate-600',
  Revoked: 'bg-red-100 text-red-700',
};

export default function GestioneConsiglieri() {
  const qc = useQueryClient();
  const [form, setForm] = useState({ nome: '', cognome: '', email: '', gruppo: '', comune: '' });
  const [formError, setFormError] = useState<string | null>(null);
  const [lastCreated, setLastCreated] = useState<InvitationCreated | null>(null);
  const [copied, setCopied] = useState(false);
  const [filter, setFilter] = useState<FilterStatus>('Tutti');
  const linkRef = useRef<HTMLInputElement>(null);

  const invitations = useQuery({
    queryKey: ['invitations'],
    queryFn: invitationsApi.list,
  });

  const createM = useMutation({
    mutationFn: () =>
      invitationsApi.create({
        nome: form.nome,
        cognome: form.cognome,
        email: form.email,
        gruppo: form.gruppo || undefined,
        comune: form.comune || undefined,
      }),
    onSuccess: (data) => {
      setLastCreated(data);
      setCopied(false);
      setForm({ nome: '', cognome: '', email: '', gruppo: '', comune: '' });
      setFormError(null);
      qc.invalidateQueries({ queryKey: ['invitations'] });
    },
    onError: (err: unknown) => {
      const data = (err as { response?: { data?: { error?: string; errors?: string[] } } })?.response?.data;
      setFormError(data?.error || data?.errors?.join('; ') || 'Errore durante la creazione dell\'invito');
    },
  });

  const removeM = useMutation({
    mutationFn: (id: string) => invitationsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['invitations'] }),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    createM.mutate();
  };

  const copyLink = (url: string) => {
    navigator.clipboard.writeText(url).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  const filtered: Invitation[] = (invitations.data ?? []).filter(
    (inv) => filter === 'Tutti' || inv.status === filter
  );

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-slate-900">Gestione consiglieri</h1>

      {/* Card invita */}
      <div className="card">
        <h2 className="text-lg font-semibold mb-4">Invita un nuovo consigliere</h2>
        <form onSubmit={handleSubmit} className="space-y-3">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="label">Nome <span className="text-red-500">*</span></label>
              <input
                className="input"
                value={form.nome}
                onChange={(e) => setForm({ ...form, nome: e.target.value })}
                required
              />
            </div>
            <div>
              <label className="label">Cognome <span className="text-red-500">*</span></label>
              <input
                className="input"
                value={form.cognome}
                onChange={(e) => setForm({ ...form, cognome: e.target.value })}
                required
              />
            </div>
          </div>
          <div>
            <label className="label">Email <span className="text-red-500">*</span></label>
            <input
              className="input"
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              required
            />
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="label">Comune <span className="text-slate-400 text-xs">(opzionale)</span></label>
              <input
                className="input"
                value={form.comune}
                onChange={(e) => setForm({ ...form, comune: e.target.value })}
                placeholder="es. Milano"
              />
            </div>
            <div>
              <label className="label">Gruppo consiliare <span className="text-slate-400 text-xs">(opzionale)</span></label>
              <input
                className="input"
                value={form.gruppo}
                onChange={(e) => setForm({ ...form, gruppo: e.target.value })}
                placeholder="es. Gruppo Misto"
              />
            </div>
          </div>
          {formError && <div className="text-sm text-red-600">{formError}</div>}
          <button className="btn-primary" disabled={createM.isPending}>
            {createM.isPending ? 'Generazione...' : 'Genera link di invito'}
          </button>
        </form>

        {/* Banner link generato */}
        {lastCreated && (
          <div className="mt-4 p-4 bg-green-50 border border-green-200 rounded-lg">
            <div className="flex items-center gap-2 mb-2 flex-wrap">
              <span className="text-sm font-medium text-green-800">Invito generato con successo!</span>
              {lastCreated.emailSent ? (
                <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-green-200 text-green-900 font-medium">
                  <svg className="w-3 h-3" viewBox="0 0 20 20" fill="currentColor"><path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd"/></svg>
                  Email inviata
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-yellow-100 text-yellow-800 font-medium">
                  <svg className="w-3 h-3" viewBox="0 0 20 20" fill="currentColor"><path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd"/></svg>
                  Email non recapitata — usa il link sotto
                </span>
              )}
            </div>
            <div className="flex gap-2">
              <input
                ref={linkRef}
                className="input flex-1 text-xs font-mono"
                readOnly
                value={lastCreated.url}
                onClick={() => linkRef.current?.select()}
              />
              <button
                className="btn-secondary whitespace-nowrap"
                onClick={() => copyLink(lastCreated.url)}
              >
                {copied ? 'Copiato!' : 'Copia link'}
              </button>
            </div>
            <div className="text-xs text-green-700 mt-2">
              Condividi questo link con il consigliere. Scade dopo 7 giorni.
            </div>
          </div>
        )}
      </div>

      {/* Card lista inviti */}
      <div className="card">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold">Inviti inviati</h2>
          <div className="flex gap-2 flex-wrap">
            {(['Tutti', 'Pending', 'Consumed', 'Expired', 'Revoked'] as FilterStatus[]).map((s) => (
              <button
                key={s}
                onClick={() => setFilter(s)}
                className={`text-xs px-3 py-1 rounded-full border transition ${
                  filter === s
                    ? 'bg-brand-600 text-white border-brand-600'
                    : 'border-slate-300 text-slate-600 hover:border-slate-400'
                }`}
              >
                {s === 'Tutti' ? 'Tutti' : STATUS_LABELS[s]}
              </button>
            ))}
          </div>
        </div>

        {invitations.isLoading && <div className="text-slate-500 text-sm">Caricamento...</div>}

        {!invitations.isLoading && filtered.length === 0 && (
          <div className="text-slate-500 text-sm">Nessun invito trovato.</div>
        )}

        {filtered.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 text-left text-slate-500">
                  <th className="pb-2 pr-4 font-medium">Nome</th>
                  <th className="pb-2 pr-4 font-medium">Email</th>
                  <th className="pb-2 pr-4 font-medium">Gruppo</th>
                  <th className="pb-2 pr-4 font-medium">Stato</th>
                  <th className="pb-2 pr-4 font-medium">Scadenza</th>
                  <th className="pb-2 font-medium">Azioni</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filtered.map((inv) => (
                  <tr key={inv.id} className="hover:bg-slate-50">
                    <td className="py-2 pr-4 font-medium text-slate-800">
                      {inv.nome} {inv.cognome}
                    </td>
                    <td className="py-2 pr-4 text-slate-600">{inv.email}</td>
                    <td className="py-2 pr-4 text-slate-600">{inv.gruppo ?? '—'}</td>
                    <td className="py-2 pr-4">
                      <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_COLORS[inv.status] ?? ''}`}>
                        {STATUS_LABELS[inv.status] ?? inv.status}
                      </span>
                    </td>
                    <td className="py-2 pr-4 text-slate-500 text-xs">
                      {new Date(inv.expiresAt).toLocaleDateString('it-IT')}
                    </td>
                    <td className="py-2">
                      <div className="flex gap-2">
                        {inv.status === 'Pending' && (
                          <button
                            className="text-xs text-brand-600 hover:underline"
                            onClick={() => {
                              const url = `${window.location.origin}/register?invite=${inv.token}`;
                              copyLink(url);
                            }}
                          >
                            Copia link
                          </button>
                        )}
                        {(inv.status === 'Pending') && (
                          <button
                            className="text-xs text-red-600 hover:underline"
                            disabled={removeM.isPending}
                            onClick={() => {
                              if (confirm(`Revocare l'invito per ${inv.email}?`)) {
                                removeM.mutate(inv.id);
                              }
                            }}
                          >
                            Revoca
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
