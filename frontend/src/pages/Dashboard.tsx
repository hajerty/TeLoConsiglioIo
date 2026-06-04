import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { actsApi, documentsApi, sittingsApi } from '../api/endpoints';

export default function Dashboard() {
  const acts = useQuery({ queryKey: ['acts'], queryFn: () => actsApi.list() });
  const docs = useQuery({ queryKey: ['docs'], queryFn: () => documentsApi.list() });
  const sittings = useQuery({ queryKey: ['sittings'], queryFn: () => sittingsApi.list() });

  return (
    <div>
      <h1 className="page-title">Dashboard</h1>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <div className="card">
          <div className="text-sm text-slate-500">Atti</div>
          <div className="text-3xl font-semibold">{acts.data?.length ?? '-'}</div>
          <Link to="/atti" className="text-brand-600 text-sm mt-2 inline-block">Vai agli atti -&gt;</Link>
        </div>
        <div className="card">
          <div className="text-sm text-slate-500">Documenti</div>
          <div className="text-3xl font-semibold">{docs.data?.length ?? '-'}</div>
          <Link to="/documenti" className="text-brand-600 text-sm mt-2 inline-block">Vai ai documenti -&gt;</Link>
        </div>
        <div className="card">
          <div className="text-sm text-slate-500">Sedute</div>
          <div className="text-3xl font-semibold">{sittings.data?.length ?? '-'}</div>
          <Link to="/sedute" className="text-brand-600 text-sm mt-2 inline-block">Vai alle sedute -&gt;</Link>
        </div>
      </div>

      <div className="card">
        <h2 className="text-lg font-semibold mb-3">Ultimi atti</h2>
        {acts.isLoading ? (
          <div className="text-slate-500">Caricamento...</div>
        ) : !acts.data?.length ? (
          <div className="text-slate-500">Nessun atto ancora. <Link to="/atti" className="text-brand-600">Crea il primo</Link>.</div>
        ) : (
          <ul className="divide-y divide-slate-100">
            {acts.data.slice(0, 5).map((a) => (
              <li key={a.id} className="py-2 flex justify-between">
                <Link to={`/atti/${a.id}`} className="hover:underline">
                  <span className="font-medium">{a.titolo}</span>
                  <span className="text-slate-500 text-sm ml-2">- {a.tipo}</span>
                </Link>
                <span className="text-xs text-slate-400">{a.status}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
