import { useRef, useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actsApi } from '../api/endpoints';
import type { ActListItem, ActType } from '../api/types';
import { Modal } from '../components/Modal';

interface CreateMenuProps {
  onSelect: (tipo: ActType) => void;
}

function CreateMenu({ onSelect }: CreateMenuProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  const MENU_ITEMS: { label: string; tipo: ActType }[] = [
    { label: 'Crea mozione', tipo: 'Mozione' },
    { label: 'Crea delibera di consiglio', tipo: 'Delibera' },
    { label: 'Crea ordine del giorno', tipo: 'OrdineDelGiorno' },
    { label: 'Crea emendamento', tipo: 'Emendamento' },
  ];

  return (
    <div className="relative" ref={ref}>
      <button
        className="btn-primary flex items-center gap-2"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="true"
        aria-expanded={open}
      >
        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
        </svg>
        Nuovo atto
        <svg className="w-3 h-3 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>
      {open && (
        <div className="absolute right-0 mt-1 w-56 bg-white border border-slate-200 rounded-lg shadow-lg z-20 py-1">
          {MENU_ITEMS.map(({ label, tipo }) => (
            <button
              key={tipo}
              className="w-full text-left px-4 py-2 text-sm text-slate-700 hover:bg-brand-50 hover:text-brand-800"
              onClick={() => {
                setOpen(false);
                onSelect(tipo);
              }}
            >
              {label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export default function Atti() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const list = useQuery({ queryKey: ['acts'], queryFn: () => actsApi.list() });

  const [openCreate, setOpenCreate] = useState(false);
  const [openParentPicker, setOpenParentPicker] = useState(false);
  const [form, setForm] = useState<{ tipo: ActType; titolo: string; oggetto: string; contextNotes: string; parentActId: string }>({
    tipo: 'Mozione',
    titolo: '',
    oggetto: '',
    contextNotes: '',
    parentActId: '',
  });
  const [parentSearch, setParentSearch] = useState('');

  const createM = useMutation({
    mutationFn: () =>
      actsApi.create({
        tipo: form.tipo,
        titolo: form.titolo,
        oggetto: form.oggetto,
        contextNotes: form.contextNotes,
        parentActId: form.parentActId || undefined,
      }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: ['acts'] });
      setOpenCreate(false);
      navigate(`/atti/${created.id}`);
    },
  });

  const handleMenuSelect = (tipo: ActType) => {
    if (tipo === 'Emendamento') {
      setForm({ tipo, titolo: '', oggetto: '', contextNotes: '', parentActId: '' });
      setOpenParentPicker(true);
    } else {
      setForm({ tipo, titolo: '', oggetto: '', contextNotes: '', parentActId: '' });
      setOpenCreate(true);
    }
  };

  const parentActs: ActListItem[] = (list.data ?? []).filter(
    (a) =>
      a.tipo !== 'Emendamento' &&
      (parentSearch === '' ||
        a.titolo.toLowerCase().includes(parentSearch.toLowerCase()) ||
        a.oggetto.toLowerCase().includes(parentSearch.toLowerCase()))
  );

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-semibold text-slate-900">Atti</h1>
        <CreateMenu onSelect={handleMenuSelect} />
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

      {/* Modal selezione atto parent per emendamento */}
      <Modal
        open={openParentPicker}
        onClose={() => setOpenParentPicker(false)}
        title="Seleziona atto da emendare"
        size="lg"
        footer={
          <>
            <button className="btn-secondary" onClick={() => setOpenParentPicker(false)}>Annulla</button>
            <button
              className="btn-primary"
              disabled={!form.parentActId}
              onClick={() => {
                setOpenParentPicker(false);
                setOpenCreate(true);
              }}
            >
              Avanti
            </button>
          </>
        }
      >
        <div className="space-y-3">
          <input
            className="input"
            placeholder="Cerca per titolo o oggetto..."
            value={parentSearch}
            onChange={(e) => setParentSearch(e.target.value)}
          />
          <ul className="max-h-64 overflow-y-auto divide-y divide-slate-100">
            {parentActs.length === 0 ? (
              <li className="py-3 text-sm text-slate-500">Nessun atto trovato.</li>
            ) : (
              parentActs.map((a) => (
                <li key={a.id}>
                  <label className="flex items-start gap-3 py-2 cursor-pointer hover:bg-slate-50 px-1 rounded">
                    <input
                      type="radio"
                      name="parentAct"
                      value={a.id}
                      checked={form.parentActId === a.id}
                      onChange={() => setForm((f) => ({ ...f, parentActId: a.id }))}
                      className="mt-0.5"
                    />
                    <div>
                      <div className="text-sm font-medium">{a.titolo || '(senza titolo)'}</div>
                      <div className="text-xs text-slate-500">{a.tipo} — {a.oggetto}</div>
                    </div>
                  </label>
                </li>
              ))
            )}
          </ul>
        </div>
      </Modal>

      {/* Modal creazione atto */}
      <Modal
        open={openCreate}
        onClose={() => setOpenCreate(false)}
        title={`Nuovo atto: ${form.tipo}`}
        footer={
          <>
            <button className="btn-secondary" onClick={() => setOpenCreate(false)}>Annulla</button>
            <button
              className="btn-primary"
              disabled={createM.isPending || !form.titolo}
              onClick={() => createM.mutate()}
            >
              {createM.isPending ? 'Creazione...' : 'Crea'}
            </button>
          </>
        }
      >
        <div className="space-y-3">
          {form.tipo === 'Emendamento' && form.parentActId && (
            <div className="text-xs bg-brand-50 text-brand-700 border border-brand-200 px-3 py-2 rounded">
              Emendamento a: <strong>
                {list.data?.find((a) => a.id === form.parentActId)?.titolo || form.parentActId}
              </strong>
            </div>
          )}
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
