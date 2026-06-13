import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../api/endpoints';
import type { ProvidersDto } from '../api/types';
import { useAuthStore } from '../auth/store';

export default function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [providers, setProviders] = useState<ProvidersDto | null>(null);
  const login = useAuthStore((s) => s.login);
  const navigate = useNavigate();

  useEffect(() => {
    authApi.providers().then(setProviders).catch(() => setProviders({ password: true, google: false, microsoft: false }));
  }, []);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const res = await authApi.login(email, password);
      login(res);
      navigate('/');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg || 'Errore di login');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
      <div className="bg-white rounded-xl shadow-lg w-full max-w-md p-6 sm:p-8">
        <h1 className="text-2xl font-semibold text-slate-900 mb-1">TeLoConsiglio</h1>
        <p className="text-sm text-slate-500 mb-6">Accedi al tuo spazio</p>
        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="label">Email</label>
            <input
              className="input min-h-[48px]"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>
          <div>
            <label className="label">Password</label>
            <input
              className="input min-h-[48px]"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>
          {error && <div className="text-sm text-red-600">{error}</div>}
          <button className="btn-primary w-full min-h-[48px] sm:min-h-[44px]" disabled={busy}>
            {busy ? 'Attendere...' : 'Accedi'}
          </button>
        </form>

        {providers && (providers.google || providers.microsoft) && (
          <div className="mt-6">
            <div className="text-xs text-center text-slate-500 mb-2">oppure</div>
            <div className="space-y-2">
              {providers.google && (
                <a
                  className="btn-secondary w-full min-h-[48px] sm:min-h-[44px] flex items-center justify-center"
                  href={`${import.meta.env.VITE_API_URL || 'http://localhost:5000'}/api/auth/external/google`}
                >
                  Accedi con Google
                </a>
              )}
              {providers.microsoft && (
                <a
                  className="btn-secondary w-full min-h-[48px] sm:min-h-[44px] flex items-center justify-center"
                  href={`${import.meta.env.VITE_API_URL || 'http://localhost:5000'}/api/auth/external/microsoft`}
                >
                  Accedi con Microsoft
                </a>
              )}
            </div>
          </div>
        )}

        <p className="text-sm text-center mt-6 text-slate-500">
          Non hai un account?{' '}
          <a href="/register" className="text-brand-600 font-medium">
            Registrati
          </a>
        </p>
      </div>
    </div>
  );
}
