import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actsApi } from '../api/endpoints';
import type { ActType } from '../api/types';
import { Modal } from '../components/Modal';

const ACT_TYPES: ActType[] = ['Mozione', 'OrdineDelGiorno', 'Delibera', 'Emendamento', 'Comunicazione'];

export default function Atti() {
  const qc = useQueryClient();
  const list = useQuery({ queryKey: ['acts'], queryFn: () => actsApi.list() });
  const [openCreate, setOpenCreate] = useState(false);
  const [form, setForm] = useState<{ tipo: ActType; titolo: string; oggetto: string; contextNotes: string }>({
    tipo: 'Mozione',
    titolo: '',
    oggetto: '',
    contextNotes: '',
  });

  const createM = useMutation({
    mutationFn: () =>
      actsApi.create({
        tipo: form.tipo,
        titolo: form.titolo,
        oggetto: form.oggetto,
        contextNotes: form.contextNotes,
      }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: ['acts'] });
      setOpenCreate(false);
      window.location.assign(`/atti/${created.id}`);
    },
  });

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-semibold text-slate-900">Atti</h1>
        <button className="btn-primary" onClick={() => setOpenCreate(true)}>+ Nuovo atto</button>
      </div>

      <div className="card">
        {list.isLoading ? (
          <div className="text-slate-500">Caricamento...</div>
        ) : !list.data?.length ? (
          <div className="text-slate-500">Nessun atto. Creane uno per iniziare.</div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-slate-500 border-b border-slate-200">
                <th className="py-2">Titolo</th>
                <th>Tipo</th>
                <th>Stato</th>
                <th>Aggiornato</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {list.data.map((a) => (
                <tr key={a.id} className="border-b border-slate-100">
                  <td className="py-2">
                    <Link to={`/atti/${a.id}`} className="font-medium text-brand-700 hover:underline">
                      {a.titolo || '(senza titolo)'}
                    </Link>
                    <div className="text-xs text-slate-500">{a.oggetto}</div>
                  </td>
                  <td>{a.tipo}</td>
                  <td>{a.status}</td>
                  <td className="text-xs text-slate-500">{new Date(a.updatedAt).toLocaleString('it-IT')}</td>
                  <td>
                    <Link to={`/atti/${a.id}`} className="text-brand-600 text-xs">Apri</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <Modal
        open={openCreate}
        onClose={() => setOpenCreate(false)}
        title="Nuovo atto"
        footer={
          <>
            <button className="btn-secondary" onClick={() => setOpenCreate(false)}>Annulla</button>
            <button className="btn-primary" disabled={createM.isPending || !form.titolo} onClick={() => createM.mutate()}>
              Crea
            </button>
          </>
        }
      >
        <div className="space-y-3">
          <div>
            <label className="label">Tipo</label>
            <select className="input" value={form.tipo} onChange={(e) => setForm({ ...form, tipo: e.target.value as ActType })}>
              {ACT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <div>
            <label className="label">Titolo</label>
            <input className="input" value={form.titolo} onChange={(e) => setForm({ ...form, titolo: e.target.value })} />
          </div>
          <div>
            <label className="label">Oggetto</label>
            <input className="input" value={form.oggetto} onChange={(e) => setForm({ ...form, oggetto: e.target.value })} />
          </div>
          <div>
            <label className="label">Note / contesto</label>
            <textarea className="input" rows={4} value={form.contextNotes} onChange={(e) => setForm({ ...form, contextNotes: e.target.value })} />
          </div>
        </div>
      </Modal>
    </div>
  );
}
