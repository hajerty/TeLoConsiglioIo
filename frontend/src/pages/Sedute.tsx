import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { sittingsApi } from '../api/endpoints';
import { Modal } from '../components/Modal';

export default function Sedute() {
  const qc = useQueryClient();
  const list = useQuery({ queryKey: ['sittings'], queryFn: () => sittingsApi.list() });
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ data: '', luogo: '', titolo: '' });

  const createM = useMutation({
    mutationFn: () =>
      sittingsApi.create({
        data: new Date(form.data).toISOString(),
        luogo: form.luogo,
        titolo: form.titolo,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sittings'] });
      setOpen(false);
      setForm({ data: '', luogo: '', titolo: '' });
    },
  });

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-semibold text-slate-900">Sedute</h1>
        <button className="btn-primary" onClick={() => setOpen(true)}>+ Nuova seduta</button>
      </div>

      <div className="card">
        {list.isLoading ? (
          <div className="text-slate-500">Caricamento...</div>
        ) : !list.data?.length ? (
          <div className="text-slate-500">Nessuna seduta.</div>
        ) : (
          <ul className="divide-y divide-slate-100">
            {list.data.map((s) => (
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
      </div>

      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title="Nuova seduta"
        footer={
          <>
            <button className="btn-secondary" onClick={() => setOpen(false)}>Annulla</button>
            <button className="btn-primary" disabled={!form.data || !form.titolo} onClick={() => createM.mutate()}>Crea</button>
          </>
        }
      >
        <div className="space-y-3">
          <div>
            <label className="label">Data e ora</label>
            <input className="input" type="datetime-local" value={form.data} onChange={(e) => setForm({ ...form, data: e.target.value })} />
          </div>
          <div>
            <label className="label">Luogo</label>
            <input className="input" value={form.luogo} onChange={(e) => setForm({ ...form, luogo: e.target.value })} />
          </div>
          <div>
            <label className="label">Titolo</label>
            <input className="input" value={form.titolo} onChange={(e) => setForm({ ...form, titolo: e.target.value })} />
          </div>
        </div>
      </Modal>
    </div>
  );
}
