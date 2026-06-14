import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { authApi } from '../api/endpoints';
import { useAuthStore } from '../auth/store';

export default function OAuthCallback() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const setTokens = useAuthStore((s) => s.setTokens);
  const setUser = useAuthStore((s) => s.setUser);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const at = searchParams.get('at');
    const rt = searchParams.get('rt');
    const needsProfile = searchParams.get('needsProfile');

    if (!at) {
      setError('Errore durante il login OAuth: token mancante.');
      return;
    }

    // Salva i token nello store
    setTokens({ accessToken: at, refreshToken: rt ?? '' });

    // Recupera i dati utente dalla me
    authApi
      .me()
      .then((user) => {
        setUser(user);
        if (needsProfile === 'true') {
          navigate('/completa-profilo', { replace: true });
        } else {
          navigate('/', { replace: true });
        }
      })
      .catch(() => {
        setError('Errore durante il recupero del profilo utente.');
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
        <div className="bg-white rounded-xl shadow-lg w-full max-w-md p-6 sm:p-8 text-center">
          <p className="text-red-600 font-medium mb-4">{error}</p>
          <a href="/login" className="btn-primary inline-block">
            Torna al login
          </a>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
      <div className="bg-white rounded-xl shadow-lg w-full max-w-md p-6 sm:p-8 text-center">
        <svg className="animate-spin w-8 h-8 mx-auto text-brand-600 mb-4" viewBox="0 0 24 24" fill="none">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z" />
        </svg>
        <p className="text-slate-600">Completamento accesso in corso...</p>
      </div>
    </div>
  );
}
