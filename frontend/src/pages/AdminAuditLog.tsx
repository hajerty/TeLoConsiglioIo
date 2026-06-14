import { useState, useEffect, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Download } from 'lucide-react';
import { adminApi } from '../api/endpoints';
import type { AuditLogQueryParams } from '../api/types';

// ---- Opzioni filtro azione ----
const ACTION_OPTIONS = [
  { value: '', label: 'Tutte le azioni' },
  { value: 'auth.login.success', label: 'auth.login.success' },
  { value: 'auth.login.failed', label: 'auth.login.failed' },
  { value: 'auth.register', label: 'auth.register' },
  { value: 'auth.logout', label: 'auth.logout' },
  { value: 'auth.refresh', label: 'auth.refresh' },
  { value: 'sitting.create', label: 'sitting.create' },
  { value: 'sitting.update', label: 'sitting.update' },
  { value: 'sitting.delete', label: 'sitting.delete' },
  { value: 'agenda.create', label: 'agenda.create' },
  { value: 'agenda.update', label: 'agenda.update' },
  { value: 'agenda.delete', label: 'agenda.delete' },
  { value: 'act.create', label: 'act.create' },
  { value: 'act.update', label: 'act.update' },
  { value: 'act.delete', label: 'act.delete' },
  { value: 'document.upload', label: 'document.upload' },
  { value: 'document.delete', label: 'document.delete' },
  { value: 'document.summarize', label: 'document.summarize' },
  { value: 'profile.update', label: 'profile.update' },
  { value: 'admin.invitation.create', label: 'admin.invitation.create' },
  { value: 'admin.invitation.revoke', label: 'admin.invitation.revoke' },
];

// ---- Colori badge per categoria azione ----
function actionBadgeClass(action: string): string {
  if (action.startsWith('auth.')) return 'bg-purple-100 text-purple-800';
  if (action.startsWith('sitting.') || action.startsWith('agenda.')) return 'bg-blue-100 text-blue-800';
  if (action.startsWith('act.')) return 'bg-green-100 text-green-800';
  if (action.startsWith('document.')) return 'bg-amber-100 text-amber-800';
  if (action.startsWith('profile.')) return 'bg-slate-100 text-slate-700';
  if (action.startsWith('admin.')) return 'bg-red-100 text-red-800';
  return 'bg-slate-100 text-slate-600';
}

const PAGE_SIZE = 50;

// ---- Debounce hook ----
function useDebounce<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

export default function AdminAuditLog() {
  const [qInput, setQInput] = useState('');
  const [action, setAction] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [userId, setUserId] = useState('');
  const [page, setPage] = useState(1);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [exportLoading, setExportLoading] = useState(false);

  const q = useDebounce(qInput, 300);

  // Reset pagina quando cambiano i filtri
  const prevFilters = useRef({ q, action, from, to, userId });
  useEffect(() => {
    const prev = prevFilters.current;
    if (
      prev.q !== q ||
      prev.action !== action ||
      prev.from !== from ||
      prev.to !== to ||
      prev.userId !== userId
    ) {
      setPage(1);
      prevFilters.current = { q, action, from, to, userId };
    }
  }, [q, action, from, to, userId]);

  const params: AuditLogQueryParams = {
    ...(q ? { q } : {}),
    ...(action ? { action } : {}),
    ...(from ? { from } : {}),
    ...(to ? { to } : {}),
    ...(userId ? { userId } : {}),
    page,
    pageSize: PAGE_SIZE,
  };

  const { data, isLoading, isError } = useQuery({
    queryKey: ['admin-audit-log', params],
    queryFn: () => adminApi.auditLog.list(params),
    placeholderData: (prev) => prev,
  });

  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const handleExport = async () => {
    setExportLoading(true);
    try {
      const exportParams: AuditLogQueryParams = {
        ...(q ? { q } : {}),
        ...(action ? { action } : {}),
        ...(from ? { from } : {}),
        ...(to ? { to } : {}),
        ...(userId ? { userId } : {}),
      };
      await adminApi.auditLog.exportCsv(exportParams);
    } finally {
      setExportLoading(false);
    }
  };

  const fmtDate = (iso: string) =>
    new Date(iso).toLocaleString('it-IT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-slate-900">Audit log</h1>

      {/* Toolbar filtri */}
      <div className="card">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {/* Ricerca full-text */}
          <div>
            <label className="label">Ricerca</label>
            <input
              className="input"
              placeholder="Cerca in action / resource..."
              value={qInput}
              onChange={(e) => setQInput(e.target.value)}
            />
          </div>

          {/* Filtro azione */}
          <div>
            <label className="label">Azione</label>
            <select
              className="input"
              value={action}
              onChange={(e) => setAction(e.target.value)}
            >
              {ACTION_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          {/* UserId */}
          <div>
            <label className="label">User ID</label>
            <input
              className="input"
              placeholder="UUID utente..."
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
            />
          </div>

          {/* Da */}
          <div>
            <label className="label">Dal</label>
            <input
              className="input"
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </div>

          {/* Al */}
          <div>
            <label className="label">Al</label>
            <input
              className="input"
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
          </div>

          {/* Esporta CSV */}
          <div className="flex items-end">
            <button
              className="btn-secondary flex items-center gap-2 w-full justify-center"
              onClick={handleExport}
              disabled={exportLoading}
            >
              <Download className="w-4 h-4" />
              {exportLoading ? 'Esportazione...' : 'Esporta CSV'}
            </button>
          </div>
        </div>
      </div>

      {/* Stato caricamento / errore */}
      {isLoading && (
        <div className="text-slate-500 text-sm">Caricamento...</div>
      )}
      {isError && (
        <div className="text-red-600 text-sm">Errore nel caricamento dell'audit log.</div>
      )}

      {/* Tabella desktop */}
      {!isLoading && !isError && (
        <>
          <div className="hidden md:block card overflow-x-auto p-0">
            {items.length === 0 ? (
              <div className="p-5 text-slate-500 text-sm">Nessun evento trovato.</div>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200 text-left text-slate-500">
                    <th className="px-4 py-3 font-medium whitespace-nowrap">Data / ora</th>
                    <th className="px-4 py-3 font-medium">Utente</th>
                    <th className="px-4 py-3 font-medium">Azione</th>
                    <th className="px-4 py-3 font-medium">Resource</th>
                    <th className="px-4 py-3 font-medium">IP</th>
                    <th className="px-4 py-3 font-medium">Dettagli</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {items.map((item) => (
                    <tr key={item.id} className="hover:bg-slate-50">
                      <td className="px-4 py-2.5 text-slate-500 text-xs whitespace-nowrap">
                        {fmtDate(item.createdAt)}
                      </td>
                      <td className="px-4 py-2.5 text-slate-700 text-xs">
                        {item.userEmail ?? <span className="text-slate-400">—</span>}
                      </td>
                      <td className="px-4 py-2.5">
                        <span
                          className={`inline-block text-xs px-2 py-0.5 rounded-full font-medium whitespace-nowrap ${actionBadgeClass(item.action)}`}
                        >
                          {item.action}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-slate-600 text-xs max-w-[160px] truncate">
                        {item.resource ?? <span className="text-slate-400">—</span>}
                      </td>
                      <td className="px-4 py-2.5 text-slate-500 text-xs whitespace-nowrap">
                        {item.ipAddress ?? <span className="text-slate-400">—</span>}
                      </td>
                      <td className="px-4 py-2.5 text-slate-500 text-xs max-w-[200px]">
                        {item.detailsJson ? (
                          <span
                            className="truncate block max-w-[200px] cursor-help"
                            title={item.detailsJson}
                          >
                            {item.detailsJson}
                          </span>
                        ) : (
                          <span className="text-slate-400">—</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          {/* Card list mobile */}
          <div className="md:hidden space-y-2">
            {items.length === 0 && (
              <div className="text-slate-500 text-sm">Nessun evento trovato.</div>
            )}
            {items.map((item) => {
              const isExpanded = expandedId === item.id;
              return (
                <div
                  key={item.id}
                  className="card cursor-pointer select-none"
                  onClick={() => setExpandedId(isExpanded ? null : item.id)}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span
                          className={`inline-block text-xs px-2 py-0.5 rounded-full font-medium ${actionBadgeClass(item.action)}`}
                        >
                          {item.action}
                        </span>
                      </div>
                      <div className="mt-1 text-xs text-slate-500">{fmtDate(item.createdAt)}</div>
                      <div className="text-xs text-slate-700 mt-0.5">
                        {item.userEmail ?? <span className="text-slate-400">Utente sconosciuto</span>}
                      </div>
                      {item.resource && (
                        <div className="text-xs text-slate-500 mt-0.5 truncate">{item.resource}</div>
                      )}
                    </div>
                    <span className="text-slate-400 text-xs pt-0.5">{isExpanded ? '▲' : '▼'}</span>
                  </div>

                  {isExpanded && (
                    <div className="mt-3 pt-3 border-t border-slate-100 space-y-1.5 text-xs">
                      <div>
                        <span className="font-medium text-slate-600">IP: </span>
                        <span className="text-slate-500">{item.ipAddress ?? '—'}</span>
                      </div>
                      <div>
                        <span className="font-medium text-slate-600">User agent: </span>
                        <span className="text-slate-500 break-all">{item.userAgent ?? '—'}</span>
                      </div>
                      {item.detailsJson && (
                        <div>
                          <span className="font-medium text-slate-600">Dettagli: </span>
                          <span className="text-slate-500 break-all">{item.detailsJson}</span>
                        </div>
                      )}
                      <div>
                        <span className="font-medium text-slate-600">User ID: </span>
                        <span className="text-slate-500 font-mono break-all">{item.userId ?? '—'}</span>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {/* Paginazione */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between gap-4">
              <button
                className="btn-secondary"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                Precedente
              </button>
              <span className="text-sm text-slate-600">
                Pagina {page} di {totalPages}
                <span className="ml-2 text-slate-400">({totalCount} eventi)</span>
              </span>
              <button
                className="btn-secondary"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              >
                Successiva
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
