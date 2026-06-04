import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../api/endpoints';
import { useAuthStore } from '../auth/store';

export default function Register() {
  const [form, setForm] = useState({ email: '', password: '', fullName: '', comune: '', partito: '' });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const login = useAuthStore((s) => s.login);
  const navigate = useNavigate();

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const res = await authApi.register(form);
      login(res);
      navigate('/');
    } catch (err: unknown) {
      const data = (err as { response?: { data?: { error?: string; errors?: string[] } } })?.response?.data;
      setError(data?.error || data?.errors?.join('; ') || 'Errore registrazione');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
      <div className="bg-white rounded-xl shadow-lg w-full max-w-md p-8">
        <h1 className="text-2xl font-semibold text-slate-900 mb-1">Crea un account</h1>
        <p className="text-sm text-slate-500 mb-6">Registrati come consigliere</p>
        <form onSubmit={submit} className="space-y-3">
          <div>
            <label className="label">Nome completo</label>
            <input className="input" value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} required />
          </div>
          <div>
            <label className="label">Email</label>
            <input className="input" type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required />
          </div>
          <div>
            <label className="label">Password</label>
            <input className="input" type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required />
            <div className="text-xs text-slate-500 mt-1">Almeno 8 caratteri, maiuscola, minuscola, numero.</div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Comune</label>
              <input className="input" value={form.comune} onChange={(e) => setForm({ ...form, comune: e.target.value })} />
            </div>
            <div>
              <label className="label">Partito/Lista</label>
              <input className="input" value={form.partito} onChange={(e) => setForm({ ...form, partito: e.target.value })} />
            </div>
          </div>
          {error && <div className="text-sm text-red-600">{error}</div>}
          <button className="btn-primary w-full" disabled={busy}>
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
