import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { profileApi } from '../api/endpoints';

export default function Profilo() {
  const qc = useQueryClient();
  const profileQ = useQuery({ queryKey: ['profile'], queryFn: profileApi.getPolitical });
  const programsQ = useQuery({ queryKey: ['programs'], queryFn: profileApi.listPrograms });

  const [linea, setLinea] = useState('');
  const [punti, setPunti] = useState('');

  useEffect(() => {
    if (profileQ.data) {
      setLinea(profileQ.data.lineaPoliticaMd || '');
      setPunti((profileQ.data.puntiEvidenza || []).join('\n'));
    }
  }, [profileQ.data]);

  const saveM = useMutation({
    mutationFn: () =>
      profileApi.putPolitical({
        lineaPoliticaMd: linea,
        puntiEvidenza: punti.split('\n').map((s) => s.trim()).filter(Boolean),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['profile'] }),
  });

  const uploadM = useMutation({
    mutationFn: (file: File) => profileApi.uploadProgram(file),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['programs'] }),
  });

  const deleteM = useMutation({
    mutationFn: (id: string) => profileApi.deleteProgram(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['programs'] }),
  });

  return (
    <div className="space-y-6">
      <h1 className="page-title">Profilo politico</h1>

      <div className="card">
        <h2 className="text-lg font-semibold mb-2">Linea politica</h2>
        <p className="text-sm text-slate-500 mb-3">
          Descrivi in modo conciso (in italiano) il tuo orientamento, le priorita' del mandato, il tono con cui vuoi scrivere gli atti.
          Questa linea sara' usata dall'AI per generare bozze e riassunti coerenti.
        </p>
        <label className="label">Linea politica (markdown)</label>
        <textarea className="input font-mono text-sm" rows={8} value={linea} onChange={(e) => setLinea(e.target.value)} />
        <label className="label mt-4">Punti da evidenziare (uno per riga)</label>
        <textarea className="input font-mono text-sm" rows={5} value={punti} onChange={(e) => setPunti(e.target.value)} />
        <div className="mt-3 flex items-center gap-3">
          <button className="btn-primary" onClick={() => saveM.mutate()} disabled={saveM.isPending}>
            {saveM.isPending ? 'Salvataggio...' : 'Salva'}
          </button>
          {saveM.isSuccess && <span className="text-sm text-green-600">Salvato.</span>}
        </div>
      </div>

      <div className="card">
        <h2 className="text-lg font-semibold mb-2">Programma elettorale</h2>
        <p className="text-sm text-slate-500 mb-3">Carica il PDF/Word del tuo programma. Verra' indicizzato per gli strumenti AI.</p>
        <input
          type="file"
          accept=".pdf,.doc,.docx,.txt,.md"
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) uploadM.mutate(f);
            e.target.value = '';
          }}
        />
        {uploadM.isPending && <div className="text-sm text-slate-500 mt-2">Caricamento...</div>}
        <ul className="mt-4 divide-y divide-slate-100">
          {(programsQ.data ?? []).map((p) => (
            <li key={p.id} className="py-2 flex justify-between items-center">
              <div>
                <div className="font-medium">{p.originalName}</div>
                <div className="text-xs text-slate-500">{new Date(p.uploadedAt).toLocaleString('it-IT')}</div>
              </div>
              <button className="btn-secondary text-xs" onClick={() => deleteM.mutate(p.id)}>
                Elimina
              </button>
            </li>
          ))}
          {!programsQ.data?.length && <div className="text-sm text-slate-500">Nessun programma caricato.</div>}
        </ul>
      </div>
    </div>
  );
}
