import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actsApi } from '../api/endpoints';
import type { ActStatus } from '../api/types';
import { Modal } from '../components/Modal';

const STATUS: ActStatus[] = ['Bozza', 'InRevisione', 'Pronto', 'Presentato', 'Archiviato'];

export default function AttoEditor() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const detail = useQuery({
    queryKey: ['act', id],
    queryFn: () => actsApi.get(id!),
    enabled: !!id,
  });

  const [titolo, setTitolo] = useState('');
  const [oggetto, setOggetto] = useState('');
  const [notes, setNotes] = useState('');
  const [body, setBody] = useState('');
  const [status, setStatus] = useState<ActStatus>('Bozza');
  const [aiInstructions, setAiInstructions] = useState('');
  const [refsModalOpen, setRefsModalOpen] = useState(false);
  const [selectedRefIds, setSelectedRefIds] = useState<Set<string>>(new Set());
  const [insertMode, setInsertMode] = useState<'append' | 'placeholder'>('append');
  const [feedback, setFeedback] = useState<string | null>(null);

  useEffect(() => {
    if (detail.data) {
      setTitolo(detail.data.titolo);
      setOggetto(detail.data.oggetto);
      setNotes(detail.data.contextNotes || '');
      setBody(detail.data.bodyMd);
      setStatus(detail.data.status);
    }
  }, [detail.data]);

  const saveM = useMutation({
    mutationFn: () =>
      actsApi.update(id!, { titolo, oggetto, contextNotes: notes, bodyMd: body, status }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['act', id] });
      qc.invalidateQueries({ queryKey: ['acts'] });
      setFeedback('Salvato.');
      setTimeout(() => setFeedback(null), 2500);
    },
  });

  const draftM = useMutation({
    mutationFn: () => actsApi.aiDraft(id!, aiInstructions),
    onSuccess: (res) => {
      setBody(res.text);
      setFeedback('Bozza generata. Ricordati di salvare.');
    },
    onError: (e: unknown) => {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setFeedback(msg || 'Errore generazione bozza');
    },
  });

  const suggestM = useMutation({
    mutationFn: () => actsApi.suggestLegalRefs(id!, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['act', id] });
      setRefsModalOpen(true);
    },
    onError: (e: unknown) => {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setFeedback(msg || 'Errore suggerimento riferimenti');
    },
  });

  const insertM = useMutation({
    mutationFn: () => actsApi.insertLegalRefs(id!, [...selectedRefIds], insertMode),
    onSuccess: (updated) => {
      setBody(updated.bodyMd);
      setSelectedRefIds(new Set());
      setRefsModalOpen(false);
      qc.invalidateQueries({ queryKey: ['act', id] });
      setFeedback('Riferimenti inseriti nel testo.');
    },
  });

  const removeM = useMutation({
    mutationFn: () => actsApi.remove(id!),
    onSuccess: () => navigate('/atti'),
  });

  if (detail.isLoading) return <div className="text-slate-500">Caricamento...</div>;
  if (!detail.data) return <div className="text-slate-500">Atto non trovato.</div>;

  const unconfirmedRefs = detail.data.legalReferences.filter((r) => !r.inserted);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-slate-900">
          {detail.data.tipo} - {titolo || '(senza titolo)'}
        </h1>
        <div className="flex gap-2">
          <button className="btn-secondary" onClick={() => removeM.mutate()}>Elimina</button>
          <button className="btn-primary" disabled={saveM.isPending} onClick={() => saveM.mutate()}>
            {saveM.isPending ? 'Salvataggio...' : 'Salva'}
          </button>
        </div>
      </div>

      {feedback && <div className="text-sm bg-brand-50 text-brand-700 border border-brand-200 px-3 py-2 rounded">{feedback}</div>}

      <div className="card grid grid-cols-1 md:grid-cols-2 gap-3">
        <div>
          <label className="label">Titolo</label>
          <input className="input" value={titolo} onChange={(e) => setTitolo(e.target.value)} />
        </div>
        <div>
          <label className="label">Oggetto</label>
          <input className="input" value={oggetto} onChange={(e) => setOggetto(e.target.value)} />
        </div>
        <div className="md:col-span-2">
          <label className="label">Note / contesto</label>
          <textarea className="input" rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>
        <div>
          <label className="label">Stato</label>
          <select className="input" value={status} onChange={(e) => setStatus(e.target.value as ActStatus)}>
            {STATUS.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
      </div>

      <div className="card">
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-lg font-semibold">Testo dell'atto (markdown)</h2>
          <div className="flex gap-2">
            <button className="btn-secondary text-sm" disabled={suggestM.isPending} onClick={() => suggestM.mutate()}>
              {suggestM.isPending ? 'AI...' : 'Suggerisci riferimenti'}
            </button>
            <button className="btn-primary text-sm" disabled={draftM.isPending} onClick={() => draftM.mutate()}>
              {draftM.isPending ? 'AI...' : 'Genera bozza AI'}
            </button>
          </div>
        </div>
        <div className="mb-3">
          <label className="label">Istruzioni aggiuntive per l'AI (facoltative)</label>
          <input className="input" value={aiInstructions} onChange={(e) => setAiInstructions(e.target.value)} placeholder="es: tono moderato, max 600 parole, includi rinvio a Statuto" />
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
          <textarea
            className="input font-mono text-sm min-h-[28rem]"
            value={body}
            onChange={(e) => setBody(e.target.value)}
            placeholder="Scrivi qui o usa 'Genera bozza AI'. Inserisci [[REF]] dove vuoi i riferimenti normativi."
          />
          <div className="border border-slate-200 rounded p-3 bg-white min-h-[28rem] overflow-auto">
            <h3 className="text-xs uppercase text-slate-500 mb-2">Anteprima</h3>
            <pre className="whitespace-pre-wrap text-sm">{body}</pre>
          </div>
        </div>
      </div>

      {detail.data.legalReferences.length > 0 && (
        <div className="card">
          <h2 className="text-lg font-semibold mb-2">Riferimenti normativi proposti</h2>
          <ul className="text-sm space-y-1">
            {detail.data.legalReferences.map((r) => (
              <li key={r.id} className="border-b border-slate-100 py-1">
                <span className={`font-medium ${r.inserted ? 'text-green-700' : 'text-slate-700'}`}>{r.citation}</span>
                <span className="text-slate-500"> — {r.description}</span>
                {r.inserted && <span className="text-xs text-green-600 ml-2">(inserito)</span>}
              </li>
            ))}
          </ul>
        </div>
      )}

      <Modal
        open={refsModalOpen}
        onClose={() => setRefsModalOpen(false)}
        title="Riferimenti suggeriti"
        size="lg"
        footer={
          <>
            <button className="btn-secondary" onClick={() => setRefsModalOpen(false)}>Annulla</button>
            <button
              className="btn-primary"
              disabled={selectedRefIds.size === 0 || insertM.isPending}
              onClick={() => insertM.mutate()}
            >
              {insertM.isPending ? 'Inserimento...' : 'Inserisci selezionati'}
            </button>
          </>
        }
      >
        {unconfirmedRefs.length === 0 ? (
          <div className="text-slate-500">Nessun riferimento da confermare. Riprova "Suggerisci".</div>
        ) : (
          <>
            <div className="mb-3 text-sm">
              <label className="inline-flex items-center gap-2">
                <input
                  type="radio"
                  checked={insertMode === 'append'}
                  onChange={() => setInsertMode('append')}
                />
                Aggiungi in coda al testo
              </label>
              <label className="inline-flex items-center gap-2 ml-4">
                <input
                  type="radio"
                  checked={insertMode === 'placeholder'}
                  onChange={() => setInsertMode('placeholder')}
                />
                Inserisci al posto di [[REF]]
              </label>
            </div>
            <ul className="space-y-2">
              {unconfirmedRefs.map((r) => (
                <li key={r.id} className="border border-slate-200 rounded p-3 bg-slate-50">
                  <label className="flex items-start gap-3 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={selectedRefIds.has(r.id)}
                      onChange={(e) => {
                        const s = new Set(selectedRefIds);
                        if (e.target.checked) s.add(r.id);
                        else s.delete(r.id);
                        setSelectedRefIds(s);
                      }}
                    />
                    <div>
                      <div className="font-medium text-sm">{r.citation}</div>
                      <div className="text-xs text-slate-600">{r.description}</div>
                    </div>
                  </label>
                </li>
              ))}
            </ul>
          </>
        )}
      </Modal>
    </div>
  );
}
