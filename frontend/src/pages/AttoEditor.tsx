import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Save,
  Trash2,
  FileDown,
  Sparkles,
  BookOpen,
  Paperclip,
  Link as LinkIcon,
} from 'lucide-react';
import { actsApi } from '../api/endpoints';
import { getAIErrorMessage } from '../api/aiError';
import type { ActStatus } from '../api/types';
import { Modal } from '../components/Modal';

const STATUS: ActStatus[] = ['Bozza', 'Depositato', 'Approvato', 'Respinto'];

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function isValidUrl(url: string): boolean {
  return /^https?:\/\/.+/.test(url.trim());
}

export default function AttoEditor() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const detail = useQuery({
    queryKey: ['act', id],
    queryFn: () => actsApi.get(id!),
    enabled: !!id,
  });
  const attachmentsQ = useQuery({
    queryKey: ['act-attachments', id],
    queryFn: () => actsApi.listAttachments(id!),
    enabled: !!id,
  });

  const [titolo, setTitolo] = useState('');
  const [oggetto, setOggetto] = useState('');
  const [notes, setNotes] = useState('');
  const [body, setBody] = useState('');
  const [status, setStatus] = useState<ActStatus>('Bozza');
  const [referenceUrls, setReferenceUrls] = useState<string[]>([]);
  const [referenceNotesMd, setReferenceNotesMd] = useState('');
  const [urlInput, setUrlInput] = useState('');
  const [urlError, setUrlError] = useState<string | null>(null);
  const [aiInstructions, setAiInstructions] = useState('');
  const [refsModalOpen, setRefsModalOpen] = useState(false);
  const [selectedRefIds, setSelectedRefIds] = useState<Set<string>>(new Set());
  const [insertMode, setInsertMode] = useState<'append' | 'placeholder'>('append');
  const [feedback, setFeedback] = useState<string | null>(null);
  const [pdfBusy, setPdfBusy] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (detail.data) {
      setTitolo(detail.data.titolo);
      setOggetto(detail.data.oggetto);
      setNotes(detail.data.contextNotes || '');
      setBody(detail.data.bodyMd);
      setStatus(detail.data.status);
      setReferenceUrls(detail.data.referenceUrls ?? []);
      setReferenceNotesMd(detail.data.referenceNotesMd ?? '');
    }
  }, [detail.data]);

  const saveM = useMutation({
    mutationFn: () =>
      actsApi.update(id!, {
        titolo,
        oggetto,
        contextNotes: notes,
        bodyMd: body,
        status,
        referenceUrls,
        referenceNotesMd: referenceNotesMd || null,
      }),
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
      setFeedback(getAIErrorMessage(e));
    },
  });

  const suggestM = useMutation({
    mutationFn: () => actsApi.suggestLegalRefs(id!, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['act', id] });
      setRefsModalOpen(true);
    },
    onError: (e: unknown) => {
      setFeedback(getAIErrorMessage(e));
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

  const uploadAttM = useMutation({
    mutationFn: (file: File) => actsApi.uploadAttachment(id!, file),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['act-attachments', id] });
    },
    onError: () => setFeedback('Errore durante il caricamento dell\'allegato.'),
  });

  const deleteAttM = useMutation({
    mutationFn: (attId: string) => actsApi.deleteAttachment(id!, attId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['act-attachments', id] }),
  });

  const handleDownloadAttachment = async (attId: string, originalName: string) => {
    try {
      const blob = await actsApi.downloadAttachment(id!, attId);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = originalName;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch {
      setFeedback('Errore durante il download dell\'allegato.');
    }
  };

  const handleExportPdf = async () => {
    setPdfBusy(true);
    try {
      const blob = await actsApi.exportPdf(id!);
      const safeName = (titolo || 'atto').replace(/[^a-z0-9]/gi, '-').toLowerCase();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `atto-${safeName}.pdf`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch {
      setFeedback('Errore durante l\'esportazione PDF.');
    } finally {
      setPdfBusy(false);
    }
  };

  const addUrl = () => {
    const trimmed = urlInput.trim();
    if (!trimmed) return;
    if (!isValidUrl(trimmed)) {
      setUrlError('L\'URL deve iniziare con http:// o https://');
      return;
    }
    if (!referenceUrls.includes(trimmed)) {
      setReferenceUrls([...referenceUrls, trimmed]);
    }
    setUrlInput('');
    setUrlError(null);
  };

  const removeUrl = (idx: number) => setReferenceUrls(referenceUrls.filter((_, i) => i !== idx));

  if (detail.isLoading) return <div className="text-slate-500">Caricamento...</div>;
  if (!detail.data) return <div className="text-slate-500">Atto non trovato.</div>;

  const unconfirmedRefs = detail.data.legalReferences.filter((r) => !r.inserted);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h1 className="text-xl md:text-2xl font-semibold text-slate-900">
          {detail.data.tipo} - {titolo || '(senza titolo)'}
        </h1>
        <div className="flex gap-2 flex-wrap">
          <button
            className="btn-secondary text-sm flex items-center gap-1.5 min-h-[44px]"
            onClick={handleExportPdf}
            disabled={pdfBusy}
          >
            <FileDown className="w-4 h-4" />
            <span className="hidden sm:inline">{pdfBusy ? 'Export...' : 'Esporta PDF'}</span>
          </button>
          <button
            className="btn-secondary text-sm flex items-center gap-1.5 min-h-[44px] text-red-600 hover:text-red-700"
            onClick={() => removeM.mutate()}
          >
            <Trash2 className="w-4 h-4" />
            <span className="hidden sm:inline">Elimina</span>
          </button>
          <button
            className="btn-primary flex items-center gap-1.5 min-h-[44px]"
            disabled={saveM.isPending}
            onClick={() => saveM.mutate()}
          >
            <Save className="w-4 h-4" />
            {saveM.isPending ? 'Salvataggio...' : 'Salva'}
          </button>
        </div>
      </div>

      {feedback && (
        <div className="text-sm bg-brand-50 text-brand-700 border border-brand-200 px-3 py-2 rounded">
          {feedback}
        </div>
      )}

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
        <div className="flex items-center justify-between mb-2 flex-wrap gap-2">
          <h2 className="text-lg font-semibold">Testo dell'atto (markdown)</h2>
          <div className="flex gap-2 flex-wrap">
            <button
              className="btn-secondary text-sm flex items-center gap-1.5 min-h-[44px]"
              disabled={suggestM.isPending}
              onClick={() => suggestM.mutate()}
            >
              <BookOpen className="w-4 h-4" />
              {suggestM.isPending ? 'AI...' : 'Suggerisci riferimenti'}
            </button>
            <button
              className="btn-primary text-sm flex items-center gap-1.5 min-h-[44px]"
              disabled={draftM.isPending}
              onClick={() => draftM.mutate()}
            >
              <Sparkles className="w-4 h-4" />
              {draftM.isPending ? 'AI...' : 'Genera bozza AI'}
            </button>
          </div>
        </div>
        <div className="mb-3">
          <label className="label">Istruzioni aggiuntive per l'AI (facoltative)</label>
          <input
            className="input"
            value={aiInstructions}
            onChange={(e) => setAiInstructions(e.target.value)}
            placeholder="es: tono moderato, max 600 parole, includi rinvio a Statuto"
          />
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
                <span className={`font-medium ${r.inserted ? 'text-green-700' : 'text-slate-700'}`}>
                  {r.citation}
                </span>
                <span className="text-slate-500"> — {r.description}</span>
                {r.inserted && <span className="text-xs text-green-600 ml-2">(inserito)</span>}
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Box Allegati */}
      <div className="card">
        <div className="flex items-center gap-2 mb-3">
          <Paperclip className="w-5 h-5 text-brand-600" />
          <h2 className="text-lg font-semibold">Allegati</h2>
        </div>
        {attachmentsQ.isLoading ? (
          <div className="text-sm text-slate-500">Caricamento allegati...</div>
        ) : (
          <ul className="divide-y divide-slate-100 mb-3">
            {(attachmentsQ.data ?? []).length === 0 && (
              <li className="py-2 text-sm text-slate-500">Nessun allegato.</li>
            )}
            {(attachmentsQ.data ?? []).map((att) => (
              <li key={att.id} className="py-2 flex items-center justify-between gap-2">
                <div className="flex items-center gap-2 min-w-0">
                  <Paperclip className="w-4 h-4 text-slate-400 flex-shrink-0" />
                  <span className="text-sm font-medium truncate">{att.originalName}</span>
                  <span className="text-xs text-slate-400 flex-shrink-0">{formatBytes(att.sizeBytes)}</span>
                </div>
                <div className="flex gap-2 flex-shrink-0">
                  <button
                    className="btn-secondary text-xs min-h-[44px]"
                    onClick={() => handleDownloadAttachment(att.id, att.originalName)}
                  >
                    Download
                  </button>
                  <button
                    className="btn-secondary text-xs text-red-600 hover:text-red-700 min-h-[44px]"
                    onClick={() => deleteAttM.mutate(att.id)}
                    disabled={deleteAttM.isPending}
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
        <div className="flex items-center gap-3">
          <input
            ref={fileInputRef}
            type="file"
            accept=".pdf,.txt,.md,.docx"
            className="hidden"
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) uploadAttM.mutate(f);
              if (fileInputRef.current) fileInputRef.current.value = '';
            }}
          />
          <button
            className="btn-secondary text-sm flex items-center gap-1.5 min-h-[44px]"
            onClick={() => fileInputRef.current?.click()}
            disabled={uploadAttM.isPending}
          >
            <Paperclip className="w-4 h-4" />
            {uploadAttM.isPending ? 'Caricamento...' : 'Aggiungi allegato'}
          </button>
          <span className="text-xs text-slate-400">Formati: PDF, TXT, MD, DOCX</span>
        </div>
      </div>

      {/* Box Link di spunto */}
      <div className="card">
        <div className="flex items-center gap-2 mb-2">
          <LinkIcon className="w-5 h-5 text-brand-600" />
          <h2 className="text-lg font-semibold">Link di spunto</h2>
        </div>
        <p className="text-sm text-slate-500 mb-3">
          URL di riferimento per questo atto (articoli, normative, documenti online). Verranno salvati con l'atto.
        </p>
        <ul className="space-y-1 mb-3">
          {referenceUrls.map((url, i) => (
            <li key={i} className="flex items-center gap-2 text-sm">
              <a
                href={url}
                target="_blank"
                rel="noopener noreferrer"
                className="text-brand-700 hover:underline truncate flex-1"
              >
                {url}
              </a>
              <button
                type="button"
                onClick={() => removeUrl(i)}
                className="text-slate-400 hover:text-red-500 flex-shrink-0 min-h-[44px] min-w-[44px] flex items-center justify-center"
                aria-label="Rimuovi URL"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </li>
          ))}
          {referenceUrls.length === 0 && (
            <li className="text-sm text-slate-500">Nessun link aggiunto.</li>
          )}
        </ul>
        <div className="flex gap-2 items-start">
          <div className="flex-1">
            <input
              className="input"
              value={urlInput}
              onChange={(e) => { setUrlInput(e.target.value); setUrlError(null); }}
              onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addUrl(); } }}
              placeholder="https://..."
            />
            {urlError && <div className="text-xs text-red-600 mt-1">{urlError}</div>}
          </div>
          <button type="button" className="btn-secondary min-h-[44px] flex items-center gap-1" onClick={addUrl}>
            <LinkIcon className="w-4 h-4" />
            Aggiungi
          </button>
        </div>
        <p className="text-xs text-slate-400 mt-2">I link vengono salvati premendo "Salva" nella toolbar.</p>
      </div>

      {/* Box Riferimenti utili (note) */}
      <div className="card">
        <h2 className="text-lg font-semibold mb-2">Riferimenti utili (note)</h2>
        <p className="text-sm text-slate-500 mb-3">
          Note libere in markdown: citazioni, estratti, commenti su normative. Vengono salvate con l'atto.
        </p>
        <textarea
          className="input font-mono text-sm"
          rows={6}
          value={referenceNotesMd}
          onChange={(e) => setReferenceNotesMd(e.target.value)}
          placeholder="# Note&#10;- Art. 42 dello Statuto comunale&#10;- Delibera n. 123/2024..."
        />
        <p className="text-xs text-slate-400 mt-1">Le note vengono salvate premendo "Salva" nella toolbar.</p>
      </div>

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
