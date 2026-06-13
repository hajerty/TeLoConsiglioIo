import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Search, ChevronLeft, ChevronRight } from 'lucide-react';
import { sittingsApi } from '../api/endpoints';
import type { SittingsQueryParams } from '../api/types';
import { Modal } from '../components/Modal';
import { useAuthStore } from '../auth/store';

const ADMIN_ROLES = ['Admin', 'Capogruppo', 'Vice'];

type Period = 'all' | 'future' | 'past';

const PERIOD_LABELS: Record<Period, string> = {
  all: 'Tutte',
  future: 'Future',
  past: 'Passate',
};

const PAGE_SIZE = 20;

export default function Sedute() {
  const qc = useQueryClient();
  const user = useAuthStore((s) => s.user);
  const canManage = user?.roles?.some((r) => ADMIN_ROLES.includes(r)) ?? false;

  const [period, setPeriod] = useState<Period>('all');
  const [q, setQ] = useState('');
  const [debouncedQ, setDebouncedQ] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);
  const [showDateFilters, setShowDateFilters] = useState(false);

  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ data: '', luogo: '', titolo: '' });

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

  const sittings = list.data?.data ?? [];

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-semibold text-slate-900">Sedute</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setOpen(true)}>
            + Nuova seduta
          </button>
        )}
      </div>

      {/* Toolbar filtri */}
      <div className="card mb-4">
        {/* Tab periodo */}
        <div className="flex gap-1 mb-3 border-b border-slate-200 pb-3">
          {(['all', 'future', 'past'] as Period[]).map((p) => (
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
            className={`btn-secondary text-sm py-1.5 ${showDateFilters ? 'bg-slate-100' : ''}`}
          >
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
    </div>
  );
}
