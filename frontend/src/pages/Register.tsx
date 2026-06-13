import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { authApi, invitationsApi, partyManifestsApi } from '../api/endpoints';
import { useAuthStore } from '../auth/store';

export default function Register() {
  const [searchParams] = useSearchParams();
  const inviteToken = searchParams.get('invite');

  const partitiQ = useQuery({ queryKey: ['party-manifests'], queryFn: partyManifestsApi.list });

  const [form, setForm] = useState({
    email: '',
    password: '',
    fullName: '',
    comune: '',
    partito: '',
    gruppo: '',
  });
  const [inviteStatus, setInviteStatus] = useState<'loading' | 'valid' | 'consumed' | 'invalid' | null>(
    inviteToken ? 'loading' : null
  );
  const [inviteLocked, setInviteLocked] = useState({ email: false, comune: false, gruppo: false });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const login = useAuthStore((s) => s.login);
  const navigate = useNavigate();

  useEffect(() => {
    if (!inviteToken) return;
    invitationsApi
      .getPublic(inviteToken)
      .then((inv) => {
        if (inv.status === 'Consumed') {
          setInviteStatus('consumed');
          return;
        }
        setForm((f) => ({
          ...f,
          email: inv.email,
          fullName: `${inv.nome} ${inv.cognome}`.trim(),
          comune: inv.comune ?? f.comune,
          gruppo: inv.gruppo ?? f.gruppo,
        }));
        setInviteLocked({
          email: true,
          comune: !!inv.comune,
          gruppo: !!inv.gruppo,
        });
        setInviteStatus('valid');
      })
      .catch(() => {
        setInviteStatus('invalid');
      });
  }, [inviteToken]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const res = await authApi.register({
        email: form.email,
        password: form.password,
        fullName: form.fullName,
        comune: form.comune || undefined,
        partito: form.partito || undefined,
        gruppo: form.gruppo || undefined,
        invitationToken: inviteToken ?? undefined,
      });
      login(res);
      navigate('/');
    } catch (err: unknown) {
      const data = (err as { response?: { data?: { error?: string; errors?: string[] } } })?.response?.data;
      setError(data?.error || data?.errors?.join('; ') || 'Errore registrazione');
    } finally {
      setBusy(false);
    }
  };

  if (inviteStatus === 'consumed') {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
        <div className="bg-white rounded-xl shadow-lg w-full max-w-md p-8 text-center">
          <div className="text-2xl font-semibold text-slate-900 mb-2">Invito gia' utilizzato</div>
          <p className="text-slate-500 mb-6">Questo invito e' stato gia' usato. Prova ad accedere con le tue credenziali.</p>
          <a href="/login" className="btn-primary inline-block">Vai al login</a>
        </div>
      </div>
    );
  }

  if (inviteStatus === 'invalid') {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
        <div className="bg-white rounded-xl shadow-lg w-full max-w-md p-8 text-center">
          <div className="text-2xl font-semibold text-slate-900 mb-2">Invito non valido</div>
          <p className="text-slate-500 mb-6">Il link di invito e' scaduto o non valido. Chiedi un nuovo invito al capogruppo.</p>
          <a href="/login" className="text-brand-600 font-medium">Torna al login</a>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
      <div className="bg-white rounded-xl shadow-lg w-full max-w-md p-8">
        <h1 className="text-2xl font-semibold text-slate-900 mb-1">Crea un account</h1>
        <p className="text-sm text-slate-500 mb-6">
          {inviteStatus === 'valid' ? 'Completa la registrazione con il tuo invito' : 'Registrati come consigliere'}
        </p>

        {inviteStatus === 'loading' && (
          <div className="text-sm text-slate-500 mb-4">Verifica invito in corso...</div>
        )}

        <form onSubmit={submit} className="space-y-3">
          <div>
            <label className="label">Nome completo</label>
            <input
              className="input"
              value={form.fullName}
              onChange={(e) => setForm({ ...form, fullName: e.target.value })}
              required
            />
          </div>
          <div>
            <label className="label">Email</label>
            <input
              className="input"
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              readOnly={inviteLocked.email}
              required
            />
          </div>
          <div>
            <label className="label">Password</label>
            <input
              className="input"
              type="password"
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              required
            />
            <div className="text-xs text-slate-500 mt-1">Almeno 8 caratteri, maiuscola, minuscola, numero.</div>
          </div>

          <div>
            <label className="label">Comune <span className="text-red-500">*</span></label>
            <input
              className="input"
              value={form.comune}
              onChange={(e) => setForm({ ...form, comune: e.target.value })}
              readOnly={inviteLocked.comune}
              required
            />
          </div>

          <div>
            <label className="label">Partito / Lista</label>
            <select
              className="input"
              value={form.partito}
              onChange={(e) => setForm({ ...form, partito: e.target.value })}
            >
              <option value="">-- Seleziona --</option>
              {(partitiQ.data ?? []).map((p) => (
                <option key={p.key} value={p.key}>{p.fullName}</option>
              ))}
              <option value="Civica">Civica</option>
            </select>
          </div>

          <div>
            <label className="label">Gruppo consiliare <span className="text-slate-400 text-xs">(opzionale)</span></label>
            <input
              className="input"
              value={form.gruppo}
              onChange={(e) => setForm({ ...form, gruppo: e.target.value })}
              readOnly={inviteLocked.gruppo}
              placeholder="es. Gruppo Misto"
            />
          </div>

          {error && <div className="text-sm text-red-600">{error}</div>}
          <button className="btn-primary w-full" disabled={busy || inviteStatus === 'loading'}>
            {busy ? 'Attendere...' : 'Registrati'}
          </button>
        </form>
        <p className="text-sm text-center mt-6 text-slate-500">
          Hai gia' un account?{' '}
          <a href="/login" className="text-brand-600 font-medium">
            Accedi
          </a>
        </p>
      </div>
    </div>
  );
}
