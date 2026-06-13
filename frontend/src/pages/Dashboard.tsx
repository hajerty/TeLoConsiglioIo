import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  FileText,
  Calendar,
  Mail,
  AlertCircle,
  CheckCircle,
  Clock,
} from 'lucide-react';
import { dashboardApi } from '../api/endpoints';
import { useAuthStore } from '../auth/store';

const ADMIN_ROLES = ['Admin', 'Capogruppo', 'Vice'];

function formatDateIT(dateStr: string) {
  return new Intl.DateTimeFormat('it-IT', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(dateStr));
}

function formatDateShortIT(dateStr: string) {
  return new Intl.DateTimeFormat('it-IT', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date(dateStr));
}

export default function Dashboard() {
  const user = useAuthStore((s) => s.user);
  const canManage = user?.roles?.some((r) => ADMIN_ROLES.includes(r)) ?? false;

  const { data, isLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: dashboardApi.get,
  });

  if (isLoading) {
    return (
      <div>
        <h1 className="page-title">Dashboard</h1>
        <div className="text-slate-500 mt-8">Caricamento...</div>
      </div>
    );
  }

  const nextSitting = data?.nextSitting ?? null;
  const recentDocuments = data?.recentDocuments ?? [];
  const documentsToAnalyze = data?.documentsToAnalyze ?? [];
  const counters = data?.counters ?? { actsBozza: 0, upcomingSittings: 0 };

  return (
    <div>
      <h1 className="page-title">Dashboard</h1>

      {/* Card prossima seduta — full width */}
      <div className="card mb-4">
        <div className="flex items-center gap-2 mb-3">
          <Calendar className="w-5 h-5 text-brand-600" />
          <h2 className="text-lg font-semibold">Prossima seduta del Comune</h2>
        </div>
        {nextSitting ? (
          <div>
            <div className="text-slate-700 font-medium capitalize">
              {formatDateIT(nextSitting.data)}
            </div>
            <div className="text-sm text-slate-500 mt-0.5">{nextSitting.luogo}</div>
            {nextSitting.titolo && (
              <div className="text-sm text-slate-700 mt-1 font-medium">{nextSitting.titolo}</div>
            )}
            <div className="flex items-center gap-2 mt-3 flex-wrap">
              <span className="text-xs bg-slate-100 text-slate-700 px-2 py-0.5 rounded-full">
                {nextSitting.agendaCount} {nextSitting.agendaCount === 1 ? 'voce' : 'voci'} ODG
              </span>
              {nextSitting.daAnalizzareCount > 0 && (
                <span className="text-xs bg-red-100 text-red-700 px-2 py-0.5 rounded-full font-medium">
                  {nextSitting.daAnalizzareCount} da analizzare
                </span>
              )}
              <Link
                to={`/sedute/${nextSitting.id}`}
                className="ml-auto text-sm btn-primary py-1 px-3"
              >
                Vai alla seduta
              </Link>
            </div>
          </div>
        ) : (
          <div className="flex items-center justify-between gap-4">
            <p className="text-slate-500 text-sm">Nessuna seduta futura in programma.</p>
            {canManage && (
              <Link to="/sedute" className="btn-primary py-1 px-3 text-sm">
                Crea seduta
              </Link>
            )}
          </div>
        )}
      </div>

      {/* Grid card */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {/* Documenti recenti */}
        <div className="card">
          <div className="flex items-center gap-2 mb-3">
            <FileText className="w-5 h-5 text-brand-600" />
            <h2 className="text-base font-semibold">Documenti recenti</h2>
          </div>
          {recentDocuments.length === 0 ? (
            <p className="text-slate-500 text-sm">Nessun documento recente.</p>
          ) : (
            <ul className="divide-y divide-slate-100">
              {recentDocuments.map((doc) => (
                <li key={doc.id} className="py-2">
                  <Link
                    to={`/documenti/${doc.id}`}
                    className="flex items-start justify-between gap-2 hover:text-brand-700 group"
                  >
                    <span className="text-sm font-medium text-slate-800 group-hover:text-brand-700 line-clamp-2">
                      {doc.originalName}
                    </span>
                    {doc.hasSummary && (
                      <span className="text-xs bg-green-100 text-green-700 px-1.5 py-0.5 rounded flex-shrink-0">
                        riassunto
                      </span>
                    )}
                  </Link>
                  <div className="text-xs text-slate-400 mt-0.5">
                    {formatDateShortIT(doc.createdAt)}
                  </div>
                </li>
              ))}
            </ul>
          )}
          <Link to="/documenti" className="text-brand-600 text-xs mt-3 inline-block hover:underline">
            Tutti i documenti
          </Link>
        </div>

        {/* Da analizzare */}
        <div className="card">
          <div className="flex items-center gap-2 mb-3">
            <AlertCircle className="w-5 h-5 text-orange-500" />
            <h2 className="text-base font-semibold">Da analizzare</h2>
          </div>
          {documentsToAnalyze.length === 0 ? (
            <div className="flex flex-col items-center py-4 text-center">
              <CheckCircle className="w-8 h-8 text-green-400 mb-2" />
              <p className="text-slate-500 text-sm">Tutto aggiornato. Nessun documento in attesa.</p>
            </div>
          ) : (
            <ul className="divide-y divide-slate-100">
              {documentsToAnalyze.map((item) => (
                <li key={`${item.sittingId}-${item.agendaItemId}`} className="py-2">
                  <Link
                    to={`/sedute/${item.sittingId}`}
                    className="block hover:text-brand-700 group"
                  >
                    <div className="text-xs text-slate-400 mb-0.5">{item.sittingTitolo}</div>
                    <div className="text-sm font-medium text-slate-800 group-hover:text-brand-700 line-clamp-2">
                      {item.descrizione}
                    </div>
                    {item.documentId != null && (
                      <span className="text-xs bg-blue-100 text-blue-700 px-1.5 py-0.5 rounded mt-1 inline-block">
                        Documento allegato
                      </span>
                    )}
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Statistiche */}
        <div className="card">
          <div className="flex items-center gap-2 mb-3">
            <Clock className="w-5 h-5 text-brand-600" />
            <h2 className="text-base font-semibold">Statistiche</h2>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="bg-slate-50 rounded-lg p-3 text-center">
              <FileText className="w-5 h-5 text-slate-500 mx-auto mb-1" />
              <div className="text-2xl font-bold text-slate-800">{counters.actsBozza}</div>
              <div className="text-xs text-slate-500 mt-0.5">Atti in bozza</div>
            </div>
            <div className="bg-slate-50 rounded-lg p-3 text-center">
              <Calendar className="w-5 h-5 text-slate-500 mx-auto mb-1" />
              <div className="text-2xl font-bold text-slate-800">{counters.upcomingSittings}</div>
              <div className="text-xs text-slate-500 mt-0.5">Sedute future</div>
            </div>
            {canManage && counters.pendingInvitations != null && (
              <div className="col-span-2 bg-blue-50 rounded-lg p-3 text-center">
                <Mail className="w-5 h-5 text-blue-500 mx-auto mb-1" />
                <div className="text-2xl font-bold text-blue-800">{counters.pendingInvitations}</div>
                <div className="text-xs text-blue-600 mt-0.5">Inviti in attesa</div>
              </div>
            )}
          </div>
          <div className="mt-3 flex gap-2 flex-wrap">
            <Link to="/atti" className="text-brand-600 text-xs hover:underline">
              Vai agli atti
            </Link>
            <span className="text-slate-300">·</span>
            <Link to="/sedute" className="text-brand-600 text-xs hover:underline">
              Vai alle sedute
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
