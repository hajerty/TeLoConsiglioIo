import { useState, useEffect } from 'react';
import {
  Sparkles,
  LayoutDashboard,
  User,
  ScrollText,
  CalendarDays,
  FileText,
  Users,
  CheckCircle,
  X,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';

interface Step {
  Icon: React.ElementType;
  title: string;
  desc: string;
  tip: string | null;
}

const STEPS: Step[] = [
  {
    Icon: Sparkles,
    title: 'Benvenuto in TeLoConsiglio.io',
    desc: 'Il tuo assistente digitale per il lavoro in consiglio comunale. Ti guidiamo in pochi passi.',
    tip: null,
  },
  {
    Icon: LayoutDashboard,
    title: 'Dashboard',
    desc: 'Qui trovi la prossima seduta, i documenti recenti e gli elementi che richiedono la tua attenzione.',
    tip: "Il badge rosso 'da analizzare' indica le delibere urgenti",
  },
  {
    Icon: User,
    title: 'Profilo politico',
    desc: 'Imposta la tua linea politica, gli argomenti forti e i temi di interesse. L\'AI userà queste informazioni per aiutarti a redigere atti coerenti.',
    tip: 'Puoi importare automaticamente la linea del tuo partito',
  },
  {
    Icon: ScrollText,
    title: 'Atti consiliari',
    desc: 'Crea mozioni, delibere, ordini del giorno ed emendamenti. L\'AI può generare una bozza a partire dal titolo e dall\'oggetto.',
    tip: "Usa il tasto 'Bozza AI' nell'editor per risparmiare tempo",
  },
  {
    Icon: CalendarDays,
    title: 'Sedute e ODG',
    desc: 'Pianifica le sedute, carica i documenti dell\'ordine del giorno e assegna i punti ai consiglieri per l\'analisi.',
    tip: 'Puoi importare l\'ODG da PDF con un clic',
  },
  {
    Icon: FileText,
    title: 'Documenti',
    desc: 'Carica delibere, verbali e altri documenti. L\'AI può generare un riassunto con punti chiave e criticità in pochi secondi.',
    tip: 'I documenti sono crittografati AES-256 sul server',
  },
  {
    Icon: Users,
    title: 'Collaborazione con il gruppo',
    desc: 'Invita altri consiglieri del tuo gruppo, assegna ruoli (Capogruppo, Vice, Consigliere) e lavorate insieme sugli stessi atti e sedute.',
    tip: null,
  },
  {
    Icon: CheckCircle,
    title: 'Sei pronto!',
    desc: 'Inizia dal tuo Profilo politico per configurare la linea del tuo partito, poi esplora le sedute e gli atti.',
    tip: "Puoi riaprire questa guida in qualsiasi momento dal menu 'Tutorial'",
  },
];

const STORAGE_KEY = 'tlc_onboarding_v1';

export function OnboardingTour() {
  const [isOpen, setIsOpen] = useState(false);
  const [current, setCurrent] = useState(0);

  useEffect(() => {
    if (localStorage.getItem(STORAGE_KEY) !== 'done') {
      setIsOpen(true);
    }
  }, []);

  function handleDone() {
    localStorage.setItem(STORAGE_KEY, 'done');
    setIsOpen(false);
    setCurrent(0);
  }

  function handleSkip() {
    handleDone();
  }

  function handleNext() {
    if (current < STEPS.length - 1) {
      setCurrent((c) => c + 1);
    } else {
      handleDone();
    }
  }

  function handlePrev() {
    if (current > 0) {
      setCurrent((c) => c - 1);
    }
  }

  if (!isOpen) return null;

  const step = STEPS[current];
  const isLast = current === STEPS.length - 1;

  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md">
        <div className="relative p-6 pb-4">
          <button
            onClick={handleSkip}
            className="absolute top-4 right-4 text-slate-400 hover:text-slate-600 transition"
            aria-label="Salta guida"
          >
            <X className="w-5 h-5" />
          </button>
          <button
            onClick={handleSkip}
            className="absolute top-4 right-10 text-xs text-slate-400 hover:text-slate-600 transition mr-1"
          >
            Salta
          </button>

          <div className="flex flex-col items-center text-center">
            <div className="w-16 h-16 bg-brand-100 rounded-full flex items-center justify-center mb-4">
              <step.Icon className="w-8 h-8 text-brand-600" />
            </div>
            <h2 className="text-xl font-bold text-slate-900 mb-2">{step.title}</h2>
            <p className="text-slate-600 text-sm leading-relaxed">{step.desc}</p>
            {step.tip && (
              <div className="mt-4 w-full bg-brand-50 border border-brand-100 rounded-lg px-4 py-3 text-left">
                <p className="text-xs text-brand-700 font-medium">
                  <span className="font-semibold">Suggerimento:</span> {step.tip}
                </p>
              </div>
            )}
          </div>
        </div>

        <div className="flex justify-center gap-2 pb-2">
          {STEPS.map((_, i) => (
            <button
              key={i}
              onClick={() => setCurrent(i)}
              aria-label={`Vai al passo ${i + 1}`}
              className={`rounded-full transition-all ${
                i === current
                  ? 'w-5 h-2.5 bg-brand-600'
                  : 'w-2.5 h-2.5 bg-slate-200 hover:bg-slate-300'
              }`}
            />
          ))}
        </div>

        <div className="flex items-center justify-between px-6 py-4 border-t border-slate-100">
          <button
            onClick={handlePrev}
            disabled={current === 0}
            className="btn-secondary disabled:opacity-30 flex items-center gap-1 px-3 py-2 text-sm"
          >
            <ChevronLeft className="w-4 h-4" />
            Indietro
          </button>
          <span className="text-xs text-slate-400">
            {current + 1} / {STEPS.length}
          </span>
          <button
            onClick={handleNext}
            className="btn-primary flex items-center gap-1 px-3 py-2 text-sm"
          >
            {isLast ? 'Inizia' : 'Avanti'}
            {!isLast && <ChevronRight className="w-4 h-4" />}
          </button>
        </div>
      </div>
    </div>
  );
}
