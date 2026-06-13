import { useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, RefreshCw, Star } from 'lucide-react';
import { sittingsApi, usersApi } from '../api/endpoints';
import type { AgendaItemStatus, Decisione, DocumentSuggestion } from '../api/types';
import { Modal } from '../components/Modal';

const DECISIONI: Decisione[] = ['DaDecidere', 'Approvare', 'Respingere', 'Astenersi'];

const DECISIONE_LABELS: Record<Decisione, string> = {
  DaDecidere: 'Da decidere',
  Approvare: 'Approvare',
  Respingere: 'Respingere',
  Astenersi: 'Astenersi',
};

const AGENDA_STATUSES: AgendaItemStatus[] = ['DaAnalizzare', 'Analizzata', 'ApprovataPerSeduta'];

const AGENDA_STATUS_LABELS: Record<AgendaItemStatus, string> = {
  DaAnalizzare: 'Da analizzare',
  Analizzata: 'Analizzata',
  ApprovataPerSeduta: 'Approvata per seduta',
};

const AGENDA_STATUS_COLORS: Record<AgendaItemStatus, string> = {
  DaAnalizzare: 'bg-slate-100 text-slate-700',
  Analizzata: 'bg-blue-100 text-blue-800',
  ApprovataPerSeduta: 'bg-green-100 text-green-800',
};

function formatDateIT(dateStr: string) {
  return new Intl.DateTimeFormat('it-IT', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(new Date(dateStr));
}

function ScoreBadge({ score }: { score: number }) {
  const pct = Math.round(score * 100);
  const color =
    pct >= 80 ? 'bg-green-100 text-green-700' :
    pct >= 50 ? 'bg-yellow-100 text-yellow-700' :
    'bg-slate-100 text-slate-600';
  return (
    <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${color}`}>
      {pct}% compatibile
    </span>
  );
}

export default function SedutaDettaglio() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const fileInputRefs = useRef<Record<string, HTMLInputElement | null>>({});
  const [statusDropdown, setStatusDropdown] = useState<string | null>(null);

  // Modal "Riproponi documento"
  const [suggestItemId, setSuggestItemId] = useState<string | null>(null);
  const [suggestions, setSuggestions] = useState<DocumentSuggestion[]>([]);
  const [loadingSuggestions, setLoadingSuggestions] = useState(false);

  const detail = useQuery({
    queryKey: ['sitting', id],
    queryFn: () => sittingsApi.get(id!),
    enabled: !!id,
  });
  const users = useQuery({ queryKey: ['users'], queryFn: usersApi.list });

  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<{
    ordine: number;
    descrizione: string;
    decisione: Decisione;
    motivazione: string;
    assignedUserIds: string[];
  }>({ ordine: 1, descrizione: '', decisione: 'DaDecidere', motivazione: '', assignedUserIds: [] });

  const addM = useMutation({
    mutationFn: () => sittingsApi.addAgenda(id!, form),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sitting', id] });
      setOpen(false);
      setForm({ ordine: (detail.data?.items.length ?? 0) + 2, descrizione: '', decisione: 'DaDecidere', motivazione: '', assignedUserIds: [] });
    },
  });

  const updateM = useMutation({
    mutationFn: ({
      itemId,
      data,
    }: {
      itemId: string;
      data: { ordine: number; descrizione: string; decisione: Decisione; motivazione: string; assignedUserIds: string[] };
    }) => sittingsApi.updateAgenda(itemId, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sitting', id] }),
  });

  const removeM = useMutation({
    mutationFn: (itemId: string) => sittingsApi.removeAgenda(itemId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sitting', id] }),
  });

  const updateStatusM = useMutation({
    mutationFn: ({ itemId, status }: { itemId: string; status: AgendaItemStatus }) =>
      sittingsApi.updateAgendaStatus(itemId, status),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sitting', id] });
      setStatusDropdown(null);
    },
  });

  const uploadDocM = useMutation({
    mutationFn: ({ itemId, file }: { itemId: string; file: File }) =>
      sittingsApi.uploadAgendaDocument(itemId, file),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sitting', id] }),
  });

  const cloneDocM = useMutation({
    mutationFn: ({ itemId, sourceAgendaItemId }: { itemId: string; sourceAgendaItemId: string }) =>
      sittingsApi.cloneDocument(itemId, sourceAgendaItemId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sitting', id] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setSuggestItemId(null);
      setSuggestions([]);
    },
  });

  // Esporta PDF report
  const [exportingPdf, setExportingPdf] = useState(false);

  async function handleExportPdf() {
    if (!id) return;
    setExportingPdf(true);
    try {
      const blob = await sittingsApi.exportReportPdf(id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `report-seduta-${id}.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } finally {
      setExportingPdf(false);
    }
  }

  async function handleOpenSuggestions(itemId: string) {
    setSuggestItemId(itemId);
    setSuggestions([]);
    setLoadingSuggestions(true);
    try {
      const data = await sittingsApi.getDocumentSuggestions(id!, itemId);
      setSuggestions(data);
    } catch {
      setSuggestions([]);
    } finally {
      setLoadingSuggestions(false);
    }
  }

  if (detail.isLoading) return <div className="text-slate-500">Caricamento...</div>;
  if (!detail.data) return <div className="text-slate-500">Seduta non trovata.</div>;

  const hasApprovate = detail.data.items.some((it) => it.status === 'ApprovataPerSeduta');

  return (
    <div>
      <div className="flex items-center justify-between mb-4 flex-wrap gap-2">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">{detail.data.titolo}</h1>
          <div className="text-sm text-slate-500">
            {new Date(detail.data.data).toLocaleString('it-IT')} - {detail.data.luogo}
          </div>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          {/* Esporta report PDF */}
          <div className="relative group">
            <button
              className="btn-secondary flex items-center gap-1.5 text-sm py-1.5 disabled:opacity-50"
              onClick={handleExportPdf}
              disabled={!hasApprovate || exportingPdf}
              title={hasApprovate ? 'Esporta report PDF' : 'Nessuna voce approvata'}
            >
              <Download className="w-4 h-4" />
              {exportingPdf ? 'Generazione...' : 'Esporta report PDF'}
            </button>
            {!hasApprovate && (
              <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-1.5 hidden group-hover:block bg-slate-800 text-white text-xs rounded px-2 py-1 whitespace-nowrap z-10">
                Nessuna voce approvata
              </div>
            )}
          </div>

          <button className="btn-primary" onClick={() => {
            setForm((f) => ({ ...f, ordine: detail.data!.items.length + 1 }));
            setOpen(true);
          }}>+ Punto ODG</button>
        </div>
      </div>

      <div className="card">
        <h2 className="text-lg font-semibold mb-3">Ordine del giorno</h2>
        {detail.data.items.length === 0 ? (
          <div className="text-slate-500">Nessun punto ancora. Aggiungine uno.</div>
        ) : (
          <ul className="space-y-3">
            {detail.data.items.map((it) => {
              const itemStatus: AgendaItemStatus = it.status ?? 'DaAnalizzare';
              return (
                <li key={it.id} className="border border-slate-200 rounded p-3 bg-slate-50">
                  <div className="flex items-center justify-between gap-2 flex-wrap">
                    <div className="font-semibold">{it.ordine}. {it.descrizione}</div>
                    <div className="flex items-center gap-2">
                      {/* Badge status con dropdown */}
                      <div className="relative">
                        <button
                          className={`text-xs px-2 py-0.5 rounded-full font-medium cursor-pointer ${AGENDA_STATUS_COLORS[itemStatus]}`}
                          onClick={() => setStatusDropdown(statusDropdown === it.id ? null : it.id)}
                        >
                          {AGENDA_STATUS_LABELS[itemStatus]}
                        </button>
                        {statusDropdown === it.id && (
                          <div className="absolute right-0 top-full mt-1 bg-white border border-slate-200 rounded shadow-lg z-10 min-w-max">
                            {AGENDA_STATUSES.map((s) => (
                              <button
                                key={s}
                                className={`block w-full text-left px-3 py-1.5 text-xs hover:bg-slate-50 ${
                                  s === itemStatus ? 'font-semibold text-brand-600' : 'text-slate-700'
                                }`}
                                onClick={() => updateStatusM.mutate({ itemId: it.id, status: s })}
                                disabled={updateStatusM.isPending}
                              >
                                {AGENDA_STATUS_LABELS[s]}
                              </button>
                            ))}
                          </div>
                        )}
                      </div>
                      <button className="text-xs text-red-600" onClick={() => removeM.mutate(it.id)}>Rimuovi</button>
                    </div>
                  </div>

                  <div className="mt-2 grid grid-cols-1 md:grid-cols-3 gap-2 text-sm">
                    <div>
                      <label className="label">Decisione</label>
                      <select
                        className="input"
                        value={it.decisione}
                        onChange={(e) =>
                          updateM.mutate({
                            itemId: it.id,
                            data: {
                              ordine: it.ordine,
                              descrizione: it.descrizione,
                              decisione: e.target.value as Decisione,
                              motivazione: it.motivazione,
                              assignedUserIds: it.assignedUsers.map((u) => u.userId),
                            },
                          })
                        }
                      >
                        {DECISIONI.map((d) => <option key={d} value={d}>{DECISIONE_LABELS[d]}</option>)}
                      </select>
                    </div>
                    <div className="md:col-span-2">
                      <label className="label">Motivazione</label>
                      <input
                        className="input"
                        defaultValue={it.motivazione}
                        onBlur={(e) =>
                          updateM.mutate({
                            itemId: it.id,
                            data: {
                              ordine: it.ordine,
                              descrizione: it.descrizione,
                              decisione: it.decisione,
                              motivazione: e.target.value,
                              assignedUserIds: it.assignedUsers.map((u) => u.userId),
                            },
                          })
                        }
                      />
                    </div>
                  </div>

                  {/* Documento allegato */}
                  <div className="mt-2 flex items-center gap-2 flex-wrap">
                    {it.documentId ? (
                      <>
                        <a
                          href={`/documenti/${it.documentId}`}
                          className="text-xs text-brand-600 hover:underline"
                        >
                          Vai al documento
                        </a>
                        <button
                          className="text-xs text-slate-500 hover:text-slate-700"
                          onClick={() => fileInputRefs.current[it.id]?.click()}
                          disabled={uploadDocM.isPending}
                        >
                          Sostituisci
                        </button>
                      </>
                    ) : (
                      <>
                        <button
                          className="text-xs btn-secondary py-0.5"
                          onClick={() => fileInputRefs.current[it.id]?.click()}
                          disabled={uploadDocM.isPending}
                        >
                          Carica documento
                        </button>
                        <button
                          className="text-xs btn-secondary py-0.5 flex items-center gap-1"
                          onClick={() => handleOpenSuggestions(it.id)}
                          disabled={uploadDocM.isPending}
                        >
                          <RefreshCw className="w-3 h-3" />
                          Riproponi da seduta precedente
                        </button>
                      </>
                    )}
                    <input
                      type="file"
                      className="hidden"
                      ref={(el) => { fileInputRefs.current[it.id] = el; }}
                      onChange={(e) => {
                        const file = e.target.files?.[0];
                        if (file) uploadDocM.mutate({ itemId: it.id, file });
                        e.target.value = '';
                      }}
                    />
                    {uploadDocM.isPending && (
                      <span className="text-xs text-slate-500">Caricamento...</span>
                    )}
                  </div>

                  {it.assignedUsers.length > 0 && (
                    <div className="text-xs mt-2 text-slate-600">
                      Assegnatari: {it.assignedUsers.map((u) => u.fullName || u.email).join(', ')}
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {/* Modal Nuovo punto ODG */}
      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title="Nuovo punto ODG"
        footer={
          <>
            <button className="btn-secondary" onClick={() => setOpen(false)}>Annulla</button>
            <button className="btn-primary" disabled={!form.descrizione} onClick={() => addM.mutate()}>Aggiungi</button>
          </>
        }
      >
        <div className="space-y-3">
          <div>
            <label className="label">Ordine</label>
            <input className="input" type="number" value={form.ordine} onChange={(e) => setForm({ ...form, ordine: parseInt(e.target.value || '1', 10) })} />
          </div>
          <div>
            <label className="label">Descrizione</label>
            <textarea className="input" rows={3} value={form.descrizione} onChange={(e) => setForm({ ...form, descrizione: e.target.value })} />
          </div>
          <div>
            <label className="label">Decisione iniziale</label>
            <select className="input" value={form.decisione} onChange={(e) => setForm({ ...form, decisione: e.target.value as Decisione })}>
              {DECISIONI.map((d) => <option key={d} value={d}>{d}</option>)}
            </select>
          </div>
          <div>
            <label className="label">Motivazione</label>
            <textarea className="input" rows={2} value={form.motivazione} onChange={(e) => setForm({ ...form, motivazione: e.target.value })} />
          </div>
          <div>
            <label className="label">Assegna consiglieri</label>
            <select
              multiple
              className="input h-32"
              value={form.assignedUserIds}
              onChange={(e) => {
                const opts = Array.from(e.target.selectedOptions).map((o) => o.value);
                setForm({ ...form, assignedUserIds: opts });
              }}
            >
              {(users.data ?? []).map((u) => (
                <option key={u.id} value={u.id}>{u.displayName}</option>
              ))}
            </select>
          </div>
        </div>
      </Modal>

      {/* Modal Riproponi documento */}
      <Modal
        open={suggestItemId !== null}
        onClose={() => { setSuggestItemId(null); setSuggestions([]); }}
        title="Riproponi documento da seduta precedente"
        size="lg"
      >
        {loadingSuggestions ? (
          <div className="text-slate-500 py-4 text-center">Ricerca suggerimenti...</div>
        ) : suggestions.length === 0 ? (
          <div className="text-slate-500 py-4 text-center">
            Nessun documento simile trovato nelle sedute precedenti.
          </div>
        ) : (
          <ul className="space-y-3">
            {suggestions.map((s) => (
              <li
                key={`${s.sittingId}-${s.agendaItemId}`}
                className="border border-slate-200 rounded-lg p-3 bg-slate-50"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex-1 min-w-0">
                    <div className="text-xs text-slate-400 mb-0.5">
                      {formatDateIT(s.sittingData)} — {s.sittingTitolo}
                    </div>
                    <div className="text-sm font-medium text-slate-800 mb-0.5 line-clamp-2">
                      {s.descrizione}
                    </div>
                    <div className="flex items-center gap-2 flex-wrap">
                      <Star className="w-3 h-3 text-slate-400 flex-shrink-0" />
                      <span className="text-xs text-slate-600 truncate">{s.documentName}</span>
                      <ScoreBadge score={s.score} />
                    </div>
                  </div>
                  <button
                    className="btn-primary text-xs py-1 px-3 flex-shrink-0"
                    disabled={cloneDocM.isPending}
                    onClick={() =>
                      cloneDocM.mutate({
                        itemId: suggestItemId!,
                        sourceAgendaItemId: s.agendaItemId,
                      })
                    }
                  >
                    {cloneDocM.isPending ? 'Riuso...' : 'Riusa questo documento'}
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Modal>
    </div>
  );
}
