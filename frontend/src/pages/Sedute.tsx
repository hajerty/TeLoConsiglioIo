import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Filter, Search, ChevronLeft, ChevronRight, Upload, ArrowUp, ArrowDown, Trash2 } from 'lucide-react';
import { sittingsApi } from '../api/endpoints';
import type { SittingsQueryParams, ParsedAgendaItem, SittingParsed } from '../api/types';
import { Modal } from '../components/Modal';
import { useAuthStore } from '../auth/store';
import { getAIErrorMessage } from '../api/aiError';

const ADMIN_ROLES = ['Admin', 'Capogruppo', 'Vice'];

type Period = 'All' | 'Past' | 'Upcoming';

const PERIOD_LABELS: Record<Period, string> = {
  All: 'Tutte',
  Upcoming: 'Future',
  Past: 'Passate',
};

const PAGE_SIZE = 20;

// Converte una stringa datetime-local in ISO string (o restituisce il valore se già ISO)
function toISO(val: string): string {
  if (!val) return '';
  try {
    return new Date(val).toISOString();
  } catch {
    return val;
  }
}

// Converte una data ISO (o null) in formato datetime-local per input
function toDatetimeLocal(val: string | null): string {
  if (!val) return '';
  try {
    const d = new Date(val);
    // formato: YYYY-MM-DDTHH:mm
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  } catch {
    return '';
  }
}

type ImportStep = 'upload' | 'review';

interface ImportForm {
  data: string;
  luogo: string;
  titolo: string;
  agendaItems: ParsedAgendaItem[];
}

export default function Sedute() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const canManage = user?.roles?.some((r) => ADMIN_ROLES.includes(r)) ?? false;

  const [period, setPeriod] = useState<Period>('All');
  const [q, setQ] = useState('');
  const [debouncedQ, setDebouncedQ] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);
  const [showDateFilters, setShowDateFilters] = useState(false);

  // Modal nuova seduta
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ data: '', luogo: '', titolo: '' });

  // Modal import PDF
  const [importOpen, setImportOpen] = useState(false);
  const [importStep, setImportStep] = useState<ImportStep>('upload');
  const [importFile, setImportFile] = useState<File | null>(null);
  const [importForm, setImportForm] = useState<ImportForm>({ data: '', luogo: '', titolo: '', agendaItems: [] });
  const [importError, setImportError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Debounce ricerca
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setDebouncedQ(q);
      setPage(1);
    }, 300);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [q]);

  // Quando cambiano i filtri, torna a pagina 1
  useEffect(() => {
    setPage(1);
  }, [period, from, to]);

  const queryParams: SittingsQueryParams = {
    period,
    page,
    pageSize: PAGE_SIZE,
    ...(debouncedQ ? { q: debouncedQ } : {}),
    ...(from ? { from } : {}),
    ...(to ? { to } : {}),
  };

  const list = useQuery({
    queryKey: ['sittings', queryParams],
    queryFn: () => sittingsApi.list(queryParams),
  });

  const totalCount = list.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const createM = useMutation({
    mutationFn: () =>
      sittingsApi.create({
        data: new Date(form.data).toISOString(),
        luogo: form.luogo,
        titolo: form.titolo,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sittings'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setOpen(false);
      setForm({ data: '', luogo: '', titolo: '' });
    },
  });

  // Mutation: analizza PDF con AI
  const parsePdfM = useMutation({
    mutationFn: (file: File) => sittingsApi.importPdf(file),
    onSuccess: (parsed: SittingParsed) => {
      setImportForm({
        data: toDatetimeLocal(parsed.data),
        luogo: parsed.luogo ?? '',
        titolo: parsed.titolo ?? '',
        agendaItems: parsed.agendaItems ?? [],
      });
      setImportStep('review');
      setImportError(null);
    },
    onError: (err: unknown) => {
      setImportError(getAIErrorMessage(err));
    },
  });

  // Mutation: crea seduta da import
  const importCreateM = useMutation({
    mutationFn: async () => {
      const sitting = await sittingsApi.create({
        data: toISO(importForm.data),
        luogo: importForm.luogo,
        titolo: importForm.titolo,
      });
      for (const item of importForm.agendaItems) {
        await sittingsApi.addAgenda(sitting.id, {
          ordine: item.ordine,
          descrizione: item.descrizione,
          decisione: 'DaDecidere',
        });
      }
      return sitting;
    },
    onSuccess: (sitting) => {
      qc.invalidateQueries({ queryKey: ['sittings'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      closeImport();
      navigate(`/sedute/${sitting.id}`);
    },
    onError: (err: unknown) => {
      const e = err as { response?: { data?: { message?: string } } };
      setImportError(e?.response?.data?.message || 'Errore durante la creazione della seduta.');
    },
  });

  function closeImport() {
    setImportOpen(false);
    setImportStep('upload');
    setImportFile(null);
    setImportForm({ data: '', luogo: '', titolo: '', agendaItems: [] });
    setImportError(null);
    parsePdfM.reset();
    importCreateM.reset();
  }

  // Gestione agendaItems
  function updateItem(idx: number, descrizione: string) {
    setImportForm((prev) => {
      const items = [...prev.agendaItems];
      items[idx] = { ...items[idx], descrizione };
      return { ...prev, agendaItems: items };
    });
  }

  function removeItem(idx: number) {
    setImportForm((prev) => {
      const items = prev.agendaItems
        .filter((_, i) => i !== idx)
        .map((item, i) => ({ ...item, ordine: i + 1 }));
      return { ...prev, agendaItems: items };
    });
  }

  function addItem() {
    setImportForm((prev) => {
      const ordine = prev.agendaItems.length + 1;
      return { ...prev, agendaItems: [...prev.agendaItems, { ordine, descrizione: '' }] };
    });
  }

  function moveItem(idx: number, dir: -1 | 1) {
    setImportForm((prev) => {
      const items = [...prev.agendaItems];
      const target = idx + dir;
      if (target < 0 || target >= items.length) return prev;
      [items[idx], items[target]] = [items[target], items[idx]];
      return {
        ...prev,
        agendaItems: items.map((item, i) => ({ ...item, ordine: i + 1 })),
      };
    });
  }

  const sittings = list.data?.data ?? [];

  return (
    <div>
      <div className="flex items-center justify-between mb-4 flex-wrap gap-2">
        <h1 className="text-2xl font-semibold text-slate-900">Sedute</h1>
        {canManage && (
          <div className="flex gap-2 flex-wrap">
            <button
              className="btn-secondary flex items-center gap-2 min-h-[44px]"
              onClick={() => { setImportOpen(true); setImportStep('upload'); }}
            >
              <Upload className="w-4 h-4" />
              Importa da PDF
            </button>
            <button className="btn-primary flex items-center gap-2 min-h-[44px]" onClick={() => setOpen(true)}>
              <Plus className="w-4 h-4" />
              Nuova seduta
            </button>
          </div>
        )}
      </div>

      {/* Toolbar filtri */}
      <div className="card mb-4">
        {/* Tab periodo */}
        <div className="flex gap-1 mb-3 border-b border-slate-200 pb-3">
          {(['All', 'Upcoming', 'Past'] as Period[]).map((p) => (
            <button
              key={p}
              onClick={() => setPeriod(p)}
              className={`px-3 py-1.5 rounded text-sm font-medium transition ${
                period === p
                  ? 'bg-brand-600 text-white'
                  : 'text-slate-600 hover:bg-slate-100'
              }`}
            >
              {PERIOD_LABELS[p]}
            </button>
          ))}
        </div>

        {/* Ricerca */}
        <div className="flex gap-2 items-center flex-wrap">
          <div className="relative flex-1 min-w-48">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              className="input pl-8"
              placeholder="Cerca per titolo o luogo..."
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
          </div>
          <button
            onClick={() => setShowDateFilters((v) => !v)}
            className={`btn-secondary text-sm py-1.5 min-h-[44px] flex items-center gap-1.5 ${showDateFilters ? 'bg-slate-100' : ''}`}
          >
            <Filter className="w-4 h-4" />
            {showDateFilters ? 'Nascondi date' : 'Filtra per data'}
          </button>
        </div>

        {/* Date pickers collassabili */}
        {showDateFilters && (
          <div className="flex gap-3 mt-3 flex-wrap">
            <div className="flex-1 min-w-36">
              <label className="label">Dal</label>
              <input
                className="input"
                type="date"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
              />
            </div>
            <div className="flex-1 min-w-36">
              <label className="label">Al</label>
              <input
                className="input"
                type="date"
                value={to}
                onChange={(e) => setTo(e.target.value)}
              />
            </div>
            {(from || to) && (
              <div className="flex items-end">
                <button
                  className="btn-secondary text-sm py-1.5"
                  onClick={() => { setFrom(''); setTo(''); }}
                >
                  Azzera
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Lista sedute */}
      <div className="card">
        {list.isLoading ? (
          <div className="text-slate-500">Caricamento...</div>
        ) : sittings.length === 0 ? (
          <div className="text-slate-500">Nessuna seduta trovata.</div>
        ) : (
          <ul className="divide-y divide-slate-100">
            {sittings.map((s) => (
              <li key={s.id} className="py-2">
                <Link to={`/sedute/${s.id}`} className="font-medium text-brand-700 hover:underline">
                  {s.titolo || '(senza titolo)'}
                </Link>
                <div className="text-xs text-slate-500">
                  {new Date(s.data).toLocaleString('it-IT')} - {s.luogo}
                </div>
              </li>
            ))}
          </ul>
        )}

        {/* Paginazione */}
        {totalCount > PAGE_SIZE && (
          <div className="flex items-center justify-between mt-4 pt-3 border-t border-slate-100">
            <button
              className="btn-secondary py-1 px-2 text-sm flex items-center gap-1 disabled:opacity-40"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              <ChevronLeft className="w-4 h-4" /> Precedente
            </button>
            <span className="text-sm text-slate-500">
              Pagina {page} di {totalPages}
              {totalCount > 0 && ` (${totalCount} in totale)`}
            </span>
            <button
              className="btn-secondary py-1 px-2 text-sm flex items-center gap-1 disabled:opacity-40"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            >
              Successiva <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        )}
      </div>

      {/* Modal nuova seduta */}
      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title="Nuova seduta"
        footer={
          <>
            <button className="btn-secondary" onClick={() => setOpen(false)}>Annulla</button>
            <button
              className="btn-primary"
              disabled={!form.data || !form.titolo || createM.isPending}
              onClick={() => createM.mutate()}
            >
              {createM.isPending ? 'Creazione...' : 'Crea'}
            </button>
          </>
        }
      >
        <div className="space-y-3">
          <div>
            <label className="label">Data e ora</label>
            <input
              className="input"
              type="datetime-local"
              value={form.data}
              onChange={(e) => setForm({ ...form, data: e.target.value })}
            />
          </div>
          <div>
            <label className="label">Luogo</label>
            <input
              className="input"
              value={form.luogo}
              onChange={(e) => setForm({ ...form, luogo: e.target.value })}
            />
          </div>
          <div>
            <label className="label">Titolo</label>
            <input
              className="input"
              value={form.titolo}
              onChange={(e) => setForm({ ...form, titolo: e.target.value })}
            />
          </div>
        </div>
      </Modal>

      {/* Modal importa da PDF */}
      <Modal
        open={importOpen}
        onClose={closeImport}
        title={importStep === 'upload' ? 'Importa convocazione da file' : 'Rivedi dati estratti'}
        size="lg"
        footer={
          importStep === 'upload' ? (
            <>
              <button className="btn-secondary" onClick={closeImport}>Annulla</button>
              <button
                className="btn-primary"
                disabled={!importFile || parsePdfM.isPending}
                onClick={() => {
                  if (importFile) {
                    setImportError(null);
                    parsePdfM.mutate(importFile);
                  }
                }}
              >
                {parsePdfM.isPending ? (
                  <span className="flex items-center gap-2">
                    <svg className="animate-spin w-4 h-4" viewBox="0 0 24 24" fill="none">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z" />
                    </svg>
                    Estrazione e parsing...
                  </span>
                ) : (
                  'Analizza con AI'
                )}
              </button>
            </>
          ) : (
            <>
              <button className="btn-secondary" onClick={closeImport}>Annulla</button>
              <button
                className="btn-secondary"
                onClick={() => {
                  setImportStep('upload');
                  setImportError(null);
                  importCreateM.reset();
                }}
              >
                Indietro
              </button>
              <button
                className="btn-primary"
                disabled={!importForm.data || !importForm.titolo || importCreateM.isPending}
                onClick={() => importCreateM.mutate()}
              >
                {importCreateM.isPending ? 'Creazione...' : 'Crea seduta'}
              </button>
            </>
          )
        }
      >
        {importStep === 'upload' ? (
          <div className="space-y-4">
            <p className="text-sm text-slate-600">
              Carica la convocazione della seduta in formato PDF, DOCX o TXT.
              L'AI estrarrà automaticamente data, luogo, titolo e punti all'ordine del giorno.
            </p>
            <div>
              <label className="label">File convocazione</label>
              <input
                ref={fileInputRef}
                type="file"
                accept=".pdf,.docx,.txt"
                className="block w-full text-sm text-slate-600 file:mr-3 file:py-2 file:px-4 file:rounded file:border-0 file:text-sm file:font-medium file:bg-brand-50 file:text-brand-700 hover:file:bg-brand-100 cursor-pointer"
                onChange={(e) => {
                  const f = e.target.files?.[0] ?? null;
                  setImportFile(f);
                  setImportError(null);
                }}
              />
              {importFile && (
                <div className="mt-1 text-xs text-slate-500">{importFile.name}</div>
              )}
            </div>
            {importError && (
              <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded p-3">
                {importError}
              </div>
            )}
          </div>
        ) : (
          <div className="space-y-4">
            <p className="text-sm text-slate-500">
              Verifica e modifica i dati estratti prima di creare la seduta.
            </p>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label className="label">Data e ora</label>
                <input
                  className="input"
                  type="datetime-local"
                  value={importForm.data}
                  onChange={(e) => setImportForm((prev) => ({ ...prev, data: e.target.value }))}
                />
              </div>
              <div>
                <label className="label">Luogo</label>
                <input
                  className="input"
                  value={importForm.luogo}
                  onChange={(e) => setImportForm((prev) => ({ ...prev, luogo: e.target.value }))}
                />
              </div>
            </div>
            <div>
              <label className="label">Titolo</label>
              <input
                className="input"
                value={importForm.titolo}
                onChange={(e) => setImportForm((prev) => ({ ...prev, titolo: e.target.value }))}
              />
            </div>

            {/* Lista punti odg */}
            <div>
              <div className="flex items-center justify-between mb-2">
                <label className="label mb-0">Punti all'ordine del giorno ({importForm.agendaItems.length})</label>
              </div>
              {importForm.agendaItems.length === 0 ? (
                <div className="text-sm text-slate-400 mb-2">Nessun punto estratto. Aggiungili manualmente.</div>
              ) : (
                <div className="space-y-2 max-h-60 overflow-y-auto pr-1">
                  {importForm.agendaItems.map((item, idx) => (
                    <div key={idx} className="flex gap-2 items-start border border-slate-200 rounded p-2 bg-slate-50">
                      <span className="text-xs font-bold text-slate-500 mt-2 w-5 shrink-0">{item.ordine}.</span>
                      <textarea
                        className="input text-sm flex-1 resize-none"
                        rows={2}
                        value={item.descrizione}
                        onChange={(e) => updateItem(idx, e.target.value)}
                      />
                      <div className="flex flex-col gap-1 shrink-0">
                        <button
                          type="button"
                          className="p-1 text-slate-400 hover:text-slate-700 disabled:opacity-30"
                          disabled={idx === 0}
                          onClick={() => moveItem(idx, -1)}
                          title="Sposta su"
                        >
                          <ArrowUp className="w-3.5 h-3.5" />
                        </button>
                        <button
                          type="button"
                          className="p-1 text-slate-400 hover:text-slate-700 disabled:opacity-30"
                          disabled={idx === importForm.agendaItems.length - 1}
                          onClick={() => moveItem(idx, 1)}
                          title="Sposta giu"
                        >
                          <ArrowDown className="w-3.5 h-3.5" />
                        </button>
                        <button
                          type="button"
                          className="p-1 text-red-400 hover:text-red-600"
                          onClick={() => removeItem(idx)}
                          title="Rimuovi"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
              <button
                type="button"
                className="btn-secondary text-sm mt-2 flex items-center gap-1"
                onClick={addItem}
              >
                <Plus className="w-3.5 h-3.5" />
                Aggiungi voce
              </button>
            </div>

            {importError && (
              <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded p-3">
                {importError}
              </div>
            )}
          </div>
        )}
      </Modal>
    </div>
  );
}
