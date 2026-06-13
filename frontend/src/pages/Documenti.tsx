import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { documentsApi } from '../api/endpoints';
import { getAIErrorMessage } from '../api/aiError';

export default function Documenti() {
  const qc = useQueryClient();
  const docs = useQuery({ queryKey: ['docs'], queryFn: () => documentsApi.list() });
  const [openId, setOpenId] = useState<string | null>(null);
  const [aiError, setAiError] = useState<string | null>(null);
  const detail = useQuery({
    queryKey: ['docs', openId],
    queryFn: () => documentsApi.get(openId!),
    enabled: !!openId,
  });

  const uploadM = useMutation({
    mutationFn: (file: File) => documentsApi.upload(file),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['docs'] }),
  });
  const summM = useMutation({
    mutationFn: (id: string) => documentsApi.summarize(id),
    onSuccess: (_, id) => {
      setAiError(null);
      qc.invalidateQueries({ queryKey: ['docs', id] });
      qc.invalidateQueries({ queryKey: ['docs'] });
    },
    onError: (e: unknown) => setAiError(getAIErrorMessage(e)),
  });
  const delM = useMutation({
    mutationFn: (id: string) => documentsApi.delete(id),
    onSuccess: () => {
      setOpenId(null);
      qc.invalidateQueries({ queryKey: ['docs'] });
    },
  });

  return (
    <div>
      <h1 className="page-title">Documenti</h1>

      <div className="card mb-4">
        <h2 className="text-lg font-semibold mb-2">Carica un nuovo documento</h2>
        <input
          type="file"
          accept=".pdf,.doc,.docx,.txt,.md"
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) uploadM.mutate(f);
            e.target.value = '';
          }}
        />
        {uploadM.isPending && <div className="text-sm text-slate-500 mt-2">Caricamento ed estrazione testo...</div>}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="card">
          <h2 className="text-lg font-semibold mb-3">Elenco</h2>
          {docs.isLoading ? (
            <div className="text-slate-500">Caricamento...</div>
          ) : !docs.data?.length ? (
            <div className="text-slate-500">Nessun documento.</div>
          ) : (
            <ul className="divide-y divide-slate-100">
              {docs.data.map((d) => (
                <li key={d.id} className="py-2 flex justify-between items-center">
                  <button onClick={() => setOpenId(d.id)} className="text-left hover:underline">
                    <div className="font-medium">{d.originalName}</div>
                    <div className="text-xs text-slate-500">
                      {new Date(d.createdAt).toLocaleString('it-IT')} {d.hasSummary && '- riassunto disponibile'}
                    </div>
                  </button>
                  <button className="btn-secondary text-xs" onClick={() => delM.mutate(d.id)}>
                    Elimina
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="card">
          {!openId ? (
            <div className="text-slate-500">Seleziona un documento per vederne il dettaglio.</div>
          ) : detail.isLoading ? (
            <div className="text-slate-500">Caricamento...</div>
          ) : !detail.data ? (
            <div className="text-slate-500">Documento non trovato.</div>
          ) : (
            <div>
              <h2 className="text-lg font-semibold mb-1">{detail.data.originalName}</h2>
              <div className="text-xs text-slate-500 mb-3">{detail.data.type}</div>
              <div className="mb-3">
                <button className="btn-primary text-sm" disabled={summM.isPending} onClick={() => { setAiError(null); summM.mutate(detail.data!.id); }}>
                  {summM.isPending ? 'Generazione...' : detail.data.summary ? 'Rigenera riassunto AI' : 'Genera riassunto AI'}
                </button>
              </div>
              {aiError && (
                <div className="mb-3 text-sm bg-amber-50 text-amber-800 border border-amber-200 px-3 py-2 rounded">
                  {aiError}
                </div>
              )}
              {detail.data.summary && (
                <div className="space-y-3">
                  <div>
                    <h3 className="font-semibold text-sm mb-1">Riassunto</h3>
                    <pre className="whitespace-pre-wrap text-sm bg-slate-50 p-3 rounded border border-slate-200">
                      {detail.data.summary.summaryMd}
                    </pre>
                  </div>
                  {detail.data.summary.keyPoints.length > 0 && (
                    <div>
                      <h3 className="font-semibold text-sm mb-1">Punti chiave</h3>
                      <ul className="list-disc list-inside text-sm space-y-1">
                        {detail.data.summary.keyPoints.map((k, i) => <li key={i}>{k}</li>)}
                      </ul>
                    </div>
                  )}
                  {detail.data.summary.criticities.length > 0 && (
                    <div>
                      <h3 className="font-semibold text-sm mb-1">Criticita'</h3>
                      <ul className="list-disc list-inside text-sm space-y-1 text-amber-700">
                        {detail.data.summary.criticities.map((k, i) => <li key={i}>{k}</li>)}
                      </ul>
                    </div>
                  )}
                </div>
              )}
              <details className="mt-4">
                <summary className="text-sm cursor-pointer text-slate-500">Testo estratto</summary>
                <pre className="whitespace-pre-wrap text-xs bg-slate-50 p-3 rounded border border-slate-200 mt-2 max-h-96 overflow-auto">
                  {detail.data.extractedText}
                </pre>
              </details>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
