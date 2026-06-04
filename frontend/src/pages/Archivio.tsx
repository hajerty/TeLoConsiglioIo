import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { actsApi } from '../api/endpoints';
import type { ActType } from '../api/types';

const TYPES: (ActType | 'Tutti')[] = ['Tutti', 'Mozione', 'OrdineDelGiorno', 'Delibera', 'Emendamento'];

export default function Archivio() {
  const [q, setQ] = useState('');
  const [tipo, setTipo] = useState<ActType | 'Tutti'>('Tutti');
  const list = useQuery({
    queryKey: ['acts', q, tipo],
    queryFn: () => actsApi.list({ q: q || undefined, tipo: tipo === 'Tutti' ? undefined : tipo }),
  });

  return (
    <div>
      <h1 className="page-title">Archivio atti</h1>
      <div className="card mb-4">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <div>
            <label className="label">Ricerca</label>
            <input className="input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="parole chiave nel titolo, oggetto, testo..." />
          </div>
          <div>
            <label className="label">Tipo</label>
            <select className="input" value={tipo} onChange={(e) => setTipo(e.target.value as ActType | 'Tutti')}>
              {TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
        </div>
      </div>

      <div className="card">
        {list.isLoading ? (
          <div className="text-slate-500">Caricamento...</div>
        ) : !list.data?.length ? (
          <div className="text-slate-500">Nessun risultato.</div>
        ) : (
          <ul className="divide-y divide-slate-100">
            {list.data.map((a) => (
              <li key={a.id} className="py-2">
                <Link to={`/atti/${a.id}`} className="font-medium text-brand-700 hover:underline">{a.titolo}</Link>
                <span className="text-xs text-slate-500 ml-2">{a.tipo} - {a.status}</span>
                <div className="text-sm text-slate-500">{a.oggetto}</div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
