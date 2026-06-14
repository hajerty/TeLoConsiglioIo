import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../api/endpoints';
import { partyManifestsApi } from '../api/endpoints';
import { useAuthStore } from '../auth/store';
import type { PartySummary } from '../api/types';

export default function CompletaProfilo() {
  const navigate = useNavigate();
  const setUser = useAuthStore((s) => s.setUser);

  const [comune, setComune] = useState('');
  const [partito, setPartito] = useState('');
  const [gruppo, setGruppo] = useState('');
  const [parties, setParties] = useState<PartySummary[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    partyManifestsApi.list().then(setParties).catch(() => setParties([]));
  }, []);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!comune.trim()) {
      setError('Il campo Comune è obbligatorio.');
      return;
    }
    if (!partito) {
      setError('Seleziona un partito.');
      return;
    }
    setBusy(true);
    try {
      const dto: { comune: string; partito: string; gruppo?: string } = {
        comune: comune.trim(),
        partito,
      };
      if (gruppo.trim()) dto.gruppo = gruppo.trim();

      await authApi.completeProfile(dto);

      // Refetch user per aggiornare lo store
      const updated = await authApi.me();
      setUser(updated);
      navigate('/', { replace: true });
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg || 'Errore durante il salvataggio del profilo.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
      <div className="bg-white rounded-xl shadow-lg w-full max-w-md p-6 sm:p-8">
        <h1 className="text-2xl font-semibold text-slate-900 mb-1">Completa il profilo</h1>

        <div className="bg-brand-50 border border-brand-200 rounded-lg p-4 mb-6 text-sm text-brand-700">
          Per usare TeLoConsiglio.io completa il tuo profilo politico. Questi dati saranno usati per
          personalizzare le funzioni AI.
        </div>

        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="label">
              Comune <span className="text-red-500">*</span>
            </label>
            <input
              className="input min-h-[48px]"
              type="text"
              value={comune}
              onChange={(e) => setComune(e.target.value)}
              placeholder="es. Roma"
              required
            />
          </div>

          <div>
            <label className="label">
              Partito <span className="text-red-500">*</span>
            </label>
            <select
              className="input min-h-[48px]"
              value={partito}
              onChange={(e) => setPartito(e.target.value)}
              required
            >
              <option value="">-- Seleziona partito --</option>
              {parties.map((p) => (
                <option key={p.key} value={p.key}>
                  {p.fullName}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="label">Gruppo consiliare (opzionale)</label>
            <input
              className="input min-h-[48px]"
              type="text"
              value={gruppo}
              onChange={(e) => setGruppo(e.target.value)}
              placeholder="es. Gruppo Misto"
            />
          </div>

          {error && <div className="text-sm text-red-600">{error}</div>}

          <button className="btn-primary w-full min-h-[48px] sm:min-h-[44px]" disabled={busy}>
            {busy ? 'Salvataggio...' : 'Salva e continua'}
          </button>
        </form>
      </div>
    </div>
  );
}
