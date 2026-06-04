import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { sittingsApi, usersApi } from '../api/endpoints';
import type { Decisione } from '../api/types';
import { Modal } from '../components/Modal';

const DECISIONI: Decisione[] = ['DaDecidere', 'Approvare', 'Respingere', 'Astenersi'];

const DECISIONE_LABELS: Record<Decisione, string> = {
  DaDecidere: 'Da decidere',
  Approvare: 'Approvare',
  Respingere: 'Respingere',
  Astenersi: 'Astenersi',
};

export default function SedutaDettaglio() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
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

  if (detail.isLoading) return <div className="text-slate-500">Caricamento...</div>;
  if (!detail.data) return <div className="text-slate-500">Seduta non trovata.</div>;

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">{detail.data.titolo}</h1>
          <div className="text-sm text-slate-500">
            {new Date(detail.data.data).toLocaleString('it-IT')} - {detail.data.luogo}
          </div>
        </div>
        <button className="btn-primary" onClick={() => {
          setForm((f) => ({ ...f, ordine: detail.data!.items.length + 1 }));
          setOpen(true);
        }}>+ Punto ODG</button>
      </div>

      <div className="card">
        <h2 className="text-lg font-semibold mb-3">Ordine del giorno</h2>
        {detail.data.items.length === 0 ? (
          <div className="text-slate-500">Nessun punto ancora. Aggiungine uno.</div>
        ) : (
          <ul className="space-y-3">
            {detail.data.items.map((it) => (
              <li key={it.id} className="border border-slate-200 rounded p-3 bg-slate-50">
                <div className="flex items-center justify-between">
                  <div className="font-semibold">{it.ordine}. {it.descrizione}</div>
                  <button className="text-xs text-red-600" onClick={() => removeM.mutate(it.id)}>Rimuovi</button>
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
                {it.assignedUsers.length > 0 && (
                  <div className="text-xs mt-2 text-slate-600">
                    Assegnatari: {it.assignedUsers.map((u) => u.fullName || u.email).join(', ')}
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

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
    </div>
  );
}
