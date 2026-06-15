import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Sparkles,
  ShieldCheck,
  CalendarDays,
  Users,
  Smartphone,
  BarChart2,
  Menu,
  X,
} from 'lucide-react';

/* ------------------------------------------------------------------ */
/* Sub-components                                                       */
/* ------------------------------------------------------------------ */

function Header() {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <header className="fixed top-0 left-0 right-0 z-50 bg-slate-900/95 backdrop-blur border-b border-white/10">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex items-center justify-between h-16">
        {/* Logo */}
        <Link to="/landing" className="text-white font-bold text-xl tracking-tight">
          TeLoConsiglio<span className="text-brand-200">.io</span>
        </Link>

        {/* Desktop nav */}
        <nav className="hidden md:flex items-center gap-4">
          <Link
            to="/login"
            className="text-white font-medium hover:text-brand-200 transition"
          >
            Accedi
          </Link>
          <Link
            to="/register"
            className="btn-primary"
          >
            Inizia gratis
          </Link>
        </nav>

        {/* Mobile hamburger */}
        <button
          className="md:hidden text-white p-2"
          onClick={() => setMobileOpen((v) => !v)}
          aria-label={mobileOpen ? 'Chiudi menu' : 'Apri menu'}
        >
          {mobileOpen ? <X size={22} /> : <Menu size={22} />}
        </button>
      </div>

      {/* Mobile dropdown */}
      {mobileOpen && (
        <div className="md:hidden bg-slate-900 border-t border-white/10 px-4 py-4 flex flex-col gap-3">
          <Link
            to="/login"
            className="text-white font-medium hover:text-brand-200 transition"
            onClick={() => setMobileOpen(false)}
          >
            Accedi
          </Link>
          <Link
            to="/register"
            className="btn-primary text-center"
            onClick={() => setMobileOpen(false)}
          >
            Inizia gratis
          </Link>
        </div>
      )}
    </header>
  );
}

function Hero() {
  return (
    <section className="bg-gradient-to-br from-slate-900 to-brand-900 pt-32 pb-20 px-4 sm:px-6 lg:px-8">
      <div className="max-w-7xl mx-auto grid md:grid-cols-2 gap-12 items-center">
        {/* Text side */}
        <div>
          <p className="inline-block text-brand-200 text-sm font-semibold uppercase tracking-widest mb-4">
            Per i consiglieri comunali italiani
          </p>
          <h1 className="text-4xl sm:text-5xl font-extrabold text-white leading-tight mb-6">
            Il tuo assistente AI per il lavoro in consiglio
          </h1>
          <p className="text-slate-300 text-lg mb-8 leading-relaxed">
            Redigi atti, analizza delibere, gestisci le sedute e collabora con il tuo gruppo&nbsp;—
            tutto in un unico strumento sicuro.
          </p>
          <div className="flex flex-wrap gap-4 mb-6">
            <Link
              to="/register"
              className="bg-white text-brand-700 font-semibold px-6 py-3 rounded-lg hover:bg-brand-50 transition shadow"
            >
              Inizia gratis
            </Link>
            <a
              href="#come-funziona"
              className="border border-white/30 text-white px-6 py-3 rounded-lg hover:bg-white/10 transition font-medium"
            >
              Guarda il demo
            </a>
          </div>
          <p className="text-slate-400 text-sm">Gratuito · Sicuro · Made in Italy 🇮🇹</p>
        </div>

        {/* Mock app preview */}
        <div className="hidden md:block">
          <div className="bg-slate-800 rounded-xl shadow-2xl overflow-hidden border border-white/10">
            {/* Top bar */}
            <div className="flex items-center gap-2 px-4 py-3 bg-slate-700 border-b border-white/10">
              <span className="w-3 h-3 rounded-full bg-red-400" />
              <span className="w-3 h-3 rounded-full bg-yellow-400" />
              <span className="w-3 h-3 rounded-full bg-green-400" />
            </div>
            {/* Body */}
            <div className="flex gap-0">
              {/* Fake sidebar */}
              <div className="w-36 bg-slate-900 p-4 flex flex-col gap-3 min-h-[220px]">
                <div className="bg-slate-700 rounded h-3 w-full" />
                <div className="bg-slate-700 rounded h-3 w-4/5" />
                <div className="bg-brand-600 rounded h-3 w-full" />
                <div className="bg-slate-700 rounded h-3 w-3/4" />
              </div>
              {/* Fake main */}
              <div className="flex-1 p-4 flex flex-col gap-3">
                <div className="bg-slate-600 rounded-lg h-16" />
                <div className="bg-slate-600 rounded-lg h-16" />
                <div className="bg-brand-600 rounded-md h-8 w-32" />
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

interface FeatureCardProps {
  icon: React.ReactNode;
  title: string;
  description: string;
}

function FeatureCard({ icon, title, description }: FeatureCardProps) {
  return (
    <div className="bg-white rounded-xl p-6 border border-slate-100 shadow-sm hover:shadow-md transition">
      <div className="mb-4">{icon}</div>
      <h3 className="text-base font-semibold text-slate-900 mb-2">{title}</h3>
      <p className="text-sm text-slate-500 leading-relaxed">{description}</p>
    </div>
  );
}

function Features() {
  const features = [
    {
      icon: <Sparkles size={24} className="text-brand-600" />,
      title: 'AI che lavora con te',
      description:
        'Genera bozze di atti, riassume delibere e suggerisce riferimenti normativi in automatico.',
    },
    {
      icon: <ShieldCheck size={24} className="text-green-600" />,
      title: 'Documenti crittografati',
      description:
        'Tutti i file sono cifrati AES-256-GCM. I tuoi dati restano privati.',
    },
    {
      icon: <CalendarDays size={24} className="text-blue-600" />,
      title: 'Gestione sedute',
      description:
        "Pianifica sedute, importa l'ODG da PDF e assegna le analisi al tuo gruppo.",
    },
    {
      icon: <Users size={24} className="text-purple-600" />,
      title: 'Lavoro di squadra',
      description:
        'Invita i consiglieri, assegna ruoli e collaborate sugli stessi atti in tempo reale.',
    },
    {
      icon: <Smartphone size={24} className="text-orange-500" />,
      title: 'PWA installabile',
      description:
        "Funziona su smartphone come un'app nativa. Consultazioni sempre a portata di mano.",
    },
    {
      icon: <BarChart2 size={24} className="text-teal-600" />,
      title: 'Audit e trasparenza',
      description:
        'Log completo di ogni azione. Esporta i report in CSV per la rendicontazione.',
    },
  ];

  return (
    <section className="bg-white py-20 px-4 sm:px-6 lg:px-8">
      <div className="max-w-7xl mx-auto">
        <h2 className="text-3xl font-extrabold text-slate-900 text-center mb-12">
          Tutto ciò di cui hai bisogno
        </h2>
        <div className="grid sm:grid-cols-2 md:grid-cols-3 gap-6">
          {features.map((f) => (
            <FeatureCard key={f.title} {...f} />
          ))}
        </div>
      </div>
    </section>
  );
}

interface StepProps {
  number: number;
  title: string;
  description: string;
}

function Step({ number, title, description }: StepProps) {
  return (
    <div className="flex gap-5 items-start">
      <div className="flex-shrink-0 w-10 h-10 bg-brand-600 text-white rounded-full flex items-center justify-center text-lg font-bold">
        {number}
      </div>
      <div>
        <h3 className="text-base font-semibold text-slate-900 mb-1">{title}</h3>
        <p className="text-sm text-slate-500 leading-relaxed">{description}</p>
      </div>
    </div>
  );
}

function HowItWorks() {
  const steps = [
    {
      title: 'Registrati in 30 secondi',
      description: 'Crea il tuo account con email o accedi con Google/Microsoft.',
    },
    {
      title: 'Configura il profilo politico',
      description:
        "Imposta la tua linea, argomenti forti e temi di interesse. L'AI si adatta al tuo partito.",
    },
    {
      title: 'Importa la prima seduta',
      description:
        "Carica il PDF dell'ODG e TeLoConsiglio.io lo analizza automaticamente.",
    },
    {
      title: 'Redigi atti in pochi minuti',
      description:
        "Scegli il tipo di atto, descrivi l'oggetto e l'AI genera una bozza completa.",
    },
  ];

  return (
    <section id="come-funziona" className="bg-slate-50 py-20 px-4 sm:px-6 lg:px-8">
      <div className="max-w-3xl mx-auto">
        <h2 className="text-3xl font-extrabold text-slate-900 text-center mb-12">
          Come funziona
        </h2>
        <div className="flex flex-col gap-10">
          {steps.map((s, i) => (
            <Step key={s.title} number={i + 1} title={s.title} description={s.description} />
          ))}
        </div>
      </div>
    </section>
  );
}

interface RoleCardProps {
  role: string;
  description: string;
}

function RoleCard({ role, description }: RoleCardProps) {
  return (
    <div className="bg-white rounded-xl p-6 border border-slate-100 shadow-sm text-center hover:shadow-md transition">
      <h3 className="text-sm font-bold text-brand-700 uppercase tracking-wide mb-2">{role}</h3>
      <p className="text-sm text-slate-500 leading-relaxed">{description}</p>
    </div>
  );
}

function Roles() {
  const roles = [
    {
      role: 'Consigliere',
      description: 'Accedi agli atti, analizza delibere e prepara le sedute',
    },
    {
      role: 'Vice Capogruppo',
      description: 'Gestisci il gruppo, invita consiglieri, coordina le analisi',
    },
    {
      role: 'Capogruppo',
      description: 'Supervisiona gli atti del gruppo e pianifica la strategia',
    },
    {
      role: 'Admin',
      description: 'Controllo completo: utenti, audit log, configurazione',
    },
  ];

  return (
    <section className="bg-white py-20 px-4 sm:px-6 lg:px-8">
      <div className="max-w-7xl mx-auto">
        <h2 className="text-3xl font-extrabold text-slate-900 text-center mb-12">
          Un ruolo per ogni esigenza
        </h2>
        <div className="grid sm:grid-cols-2 md:grid-cols-4 gap-6">
          {roles.map((r) => (
            <RoleCard key={r.role} {...r} />
          ))}
        </div>
      </div>
    </section>
  );
}

function CtaFinal() {
  return (
    <section className="bg-brand-700 py-20 px-4 sm:px-6 lg:px-8 text-center">
      <div className="max-w-2xl mx-auto">
        <h2 className="text-3xl font-extrabold text-white mb-4">
          Pronto a lavorare meglio in consiglio?
        </h2>
        <p className="text-brand-200 text-lg mb-8">
          Attiva il tuo account gratuitamente in meno di un minuto.
        </p>
        <Link
          to="/register"
          className="bg-white text-brand-700 font-semibold px-8 py-4 rounded-lg text-lg hover:bg-brand-50 transition shadow-lg inline-block"
        >
          Inizia subito
        </Link>
      </div>
    </section>
  );
}

function Footer() {
  return (
    <footer className="bg-slate-900 py-8 px-4 sm:px-6 lg:px-8">
      <div className="max-w-7xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4 text-sm">
        <span className="text-slate-400">© 2025 TeLoConsiglio.io</span>
        <nav className="flex gap-6">
          <a href="#come-funziona" className="text-slate-400 hover:text-white transition">
            Tutorial
          </a>
          <Link to="/login" className="text-slate-400 hover:text-white transition">
            Accedi
          </Link>
        </nav>
        <span className="text-slate-500">Made with AI · For Italy</span>
      </div>
    </footer>
  );
}

/* ------------------------------------------------------------------ */
/* Page                                                                 */
/* ------------------------------------------------------------------ */

export default function Landing() {
  return (
    <div className="min-h-screen flex flex-col">
      <Header />
      <main className="flex-1">
        <Hero />
        <Features />
        <HowItWorks />
        <Roles />
        <CtaFinal />
      </main>
      <Footer />
    </div>
  );
}
