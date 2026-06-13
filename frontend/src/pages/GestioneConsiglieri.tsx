import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { invitationsApi } from '../api/endpoints';
import type { Invitation } from '../api/types';

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
  const [generatedLink, setGeneratedLink] = useState<string | null>(null);
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
      setGeneratedLink(data.url);
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
        {generatedLink && (
          <div className="mt-4 p-4 bg-green-50 border border-green-200 rounded-lg">
            <div className="text-sm font-medium text-green-800 mb-2">Invito generato con successo!</div>
            <div className="flex gap-2">
              <input
                ref={linkRef}
                className="input flex-1 text-xs font-mono"
                readOnly
                value={generatedLink}
                onClick={() => linkRef.current?.select()}
              />
              <button
                className="btn-secondary whitespace-nowrap"
                onClick={() => copyLink(generatedLink)}
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
