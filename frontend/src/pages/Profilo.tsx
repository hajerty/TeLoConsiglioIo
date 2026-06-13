import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Compass, Heart, FileBadge, RotateCcw, Upload, Trash2 } from 'lucide-react';
import { profileApi } from '../api/endpoints';
import { Modal } from '../components/Modal';

function TagInput({
  label,
  values,
  onChange,
}: {
  label: string;
  values: string[];
  onChange: (v: string[]) => void;
}) {
  const [input, setInput] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  const add = () => {
    const trimmed = input.trim();
    if (trimmed && !values.includes(trimmed)) {
      onChange([...values, trimmed]);
    }
    setInput('');
    inputRef.current?.focus();
  };

  const remove = (idx: number) => onChange(values.filter((_, i) => i !== idx));

  return (
    <div>
      <label className="label">{label}</label>
      <div className="flex flex-wrap gap-2 mb-2">
        {values.map((v, i) => (
          <span
            key={i}
            className="inline-flex items-center gap-1 bg-brand-100 text-brand-800 text-xs font-medium px-2 py-1 rounded-full"
          >
            {v}
            <button
              type="button"
              onClick={() => remove(i)}
              className="ml-1 text-brand-600 hover:text-brand-900 leading-none"
              aria-label={`Rimuovi ${v}`}
            >
              ×
            </button>
          </span>
        ))}
      </div>
      <div className="flex gap-2">
        <input
          ref={inputRef}
          className="input flex-1"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') { e.preventDefault(); add(); }
          }}
          placeholder="Scrivi e premi Invio o +"
        />
        <button type="button" className="btn-secondary min-h-[44px]" onClick={add}>+</button>
      </div>
    </div>
  );
}

export default function Profilo() {
  const qc = useQueryClient();
  const profileQ = useQuery({ queryKey: ['profile'], queryFn: profileApi.getPolitical });
  const programsQ = useQuery({ queryKey: ['programs'], queryFn: profileApi.listPrograms });

  const [linea, setLinea] = useState('');
  const [argomentiForti, setArgomentiForti] = useState<string[]>([]);
  const [temiInteresse, setTemiInteresse] = useState<string[]>([]);
  const [resetModalOpen, setResetModalOpen] = useState(false);

  useEffect(() => {
    if (profileQ.data) {
      setLinea(profileQ.data.lineaPoliticaMd || '');
      setArgomentiForti(profileQ.data.argomentiForti ?? []);
      setTemiInteresse(profileQ.data.temiInteresse ?? []);
    }
  }, [profileQ.data]);

  const saveM = useMutation({
    mutationFn: () =>
      profileApi.updatePolitical({
        lineaPoliticaMd: linea,
        argomentiForti,
        temiInteresse,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['profile'] }),
  });

  const resetM = useMutation({
    mutationFn: () => profileApi.resetLineaPolitica(),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ['profile'] });
      setLinea(data.lineaPoliticaMd || '');
      setResetModalOpen(false);
    },
  });

  const uploadM = useMutation({
    mutationFn: (file: File) => profileApi.uploadProgram(file),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['programs'] }),
  });

  const deleteM = useMutation({
    mutationFn: (id: string) => profileApi.deleteProgram(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['programs'] }),
  });

  const source = profileQ.data?.lineaPoliticaSource;
  const sourceBadge =
    source === 'Partito' ? (
      <span className="ml-2 text-xs font-medium bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">
        Da manifesto del partito
      </span>
    ) : source === 'Manuale' ? (
      <span className="ml-2 text-xs font-medium bg-slate-100 text-slate-600 px-2 py-0.5 rounded-full">
        Personalizzata
      </span>
    ) : null;

  return (
    <div className="space-y-6">
      <h1 className="page-title">Profilo politico</h1>

      {/* Indirizzo di mandato */}
      <div className="card space-y-5">
        <div className="flex items-center gap-3 pb-3 border-b border-slate-100">
          <div className="w-10 h-10 bg-brand-50 rounded-lg flex items-center justify-center flex-shrink-0">
            <Compass className="w-5 h-5 text-brand-600" />
          </div>
          <div>
            <h2 className="text-base font-bold text-slate-900">Indirizzo di mandato</h2>
            <p className="text-sm text-slate-500">
              Definisci le priorita' del tuo mandato. L'AI usera' questi dati per generare atti coerenti con la tua posizione.
            </p>
          </div>
        </div>

        <TagInput
          label="Argomenti forti (tesi che sosterrai)"
          values={argomentiForti}
          onChange={setArgomentiForti}
        />

        <TagInput
          label="Temi di interesse (aree che vuoi seguire)"
          values={temiInteresse}
          onChange={setTemiInteresse}
        />

        <div className="flex items-center gap-3">
          <button
            className="btn-primary min-h-[44px]"
            onClick={() => saveM.mutate()}
            disabled={saveM.isPending}
          >
            {saveM.isPending ? 'Salvataggio...' : 'Salva indirizzo di mandato'}
          </button>
          {saveM.isSuccess && <span className="text-sm text-green-600">Salvato.</span>}
        </div>
      </div>

      {/* Linea politica */}
      <div className="card">
        <div className="flex items-center justify-between mb-1 pb-3 border-b border-slate-100 flex-wrap gap-2">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-brand-50 rounded-lg flex items-center justify-center flex-shrink-0">
              <Heart className="w-5 h-5 text-brand-600" />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900 flex items-center flex-wrap gap-1">
                Linea politica
                {sourceBadge}
              </h2>
              <p className="text-sm text-slate-500">
                Orientamento e priorita' del mandato per la generazione AI.
              </p>
            </div>
          </div>
          {(source === 'Partito' || source === 'Manuale') && (
            <button
              className="btn-secondary text-xs flex items-center gap-1.5 min-h-[44px]"
              onClick={() => setResetModalOpen(true)}
            >
              <RotateCcw className="w-3.5 h-3.5" />
              Ripristina dal manifesto
            </button>
          )}
        </div>
        <div className="mt-4">
          <label className="label">Linea politica (markdown)</label>
          <textarea
            className="input font-mono text-sm"
            rows={10}
            value={linea}
            onChange={(e) => setLinea(e.target.value)}
          />
          <div className="mt-3 flex items-center gap-3">
            <button
              className="btn-primary min-h-[44px]"
              onClick={() => saveM.mutate()}
              disabled={saveM.isPending}
            >
              {saveM.isPending ? 'Salvataggio...' : 'Salva linea politica'}
            </button>
            {saveM.isSuccess && <span className="text-sm text-green-600">Salvato.</span>}
          </div>
        </div>
      </div>

      {/* Programma elettorale */}
      <div className="card">
        <div className="flex items-center gap-3 mb-4 pb-3 border-b border-slate-100">
          <div className="w-10 h-10 bg-brand-50 rounded-lg flex items-center justify-center flex-shrink-0">
            <FileBadge className="w-5 h-5 text-brand-600" />
          </div>
          <div>
            <h2 className="text-base font-bold text-slate-900">Programma elettorale</h2>
            <p className="text-sm text-slate-500">
              Carica il PDF/Word del tuo programma. Verra' indicizzato per gli strumenti AI.
            </p>
          </div>
        </div>

        <label className="flex items-center gap-3 cursor-pointer mb-4">
          <input
            type="file"
            accept=".pdf,.doc,.docx,.txt,.md"
            className="hidden"
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) uploadM.mutate(f);
              e.target.value = '';
            }}
          />
          <span className="btn-secondary flex items-center gap-2 min-h-[44px]">
            <Upload className="w-4 h-4" />
            Carica programma
          </span>
        </label>
        {uploadM.isPending && <div className="text-sm text-slate-500 mb-2">Caricamento...</div>}

        <ul className="divide-y divide-slate-100">
          {(programsQ.data ?? []).map((p) => (
            <li key={p.id} className="py-2 flex justify-between items-center gap-2">
              <div className="flex items-center gap-2 min-w-0">
                <FileBadge className="w-4 h-4 text-slate-400 flex-shrink-0" />
                <div className="min-w-0">
                  <div className="font-medium truncate">{p.originalName}</div>
                  <div className="text-xs text-slate-500">
                    {new Date(p.uploadedAt).toLocaleString('it-IT')}
                  </div>
                </div>
              </div>
              <button
                className="btn-secondary text-xs flex items-center gap-1.5 min-h-[44px] flex-shrink-0"
                onClick={() => deleteM.mutate(p.id)}
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </li>
          ))}
          {!programsQ.data?.length && (
            <div className="text-sm text-slate-500">Nessun programma caricato.</div>
          )}
        </ul>
      </div>

      {/* Modal conferma reset linea politica */}
      <Modal
        open={resetModalOpen}
        onClose={() => setResetModalOpen(false)}
        title="Ripristina linea politica"
        footer={
          <>
            <button className="btn-secondary" onClick={() => setResetModalOpen(false)}>
              Annulla
            </button>
            <button
              className="btn-primary"
              disabled={resetM.isPending}
              onClick={() => resetM.mutate()}
            >
              {resetM.isPending ? 'Ripristino...' : 'Conferma ripristino'}
            </button>
          </>
        }
      >
        <p className="text-sm text-slate-700">
          Vuoi ripristinare la linea politica dal manifesto del tuo partito? Il testo attuale verra'
          sostituito con quello del manifesto ufficiale. L'operazione non e' reversibile.
        </p>
        {resetM.isError && (
          <div className="mt-3 text-sm text-red-600">
            Errore durante il ripristino. Verifica che il tuo partito abbia un manifesto disponibile.
          </div>
        )}
      </Modal>
    </div>
  );
}
