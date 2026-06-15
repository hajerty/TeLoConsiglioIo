import { useState } from 'react';
import {
  BookOpen,
  ScrollText,
  CalendarDays,
  Sparkles,
  ShieldCheck,
  HelpCircle,
  ChevronDown,
} from 'lucide-react';
import { OnboardingTour } from '../components/OnboardingTour';

interface AccordionSectionProps {
  Icon: React.ElementType;
  title: string;
  defaultOpen?: boolean;
  children: React.ReactNode;
}

function AccordionSection({ Icon, title, defaultOpen = false, children }: AccordionSectionProps) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <div className="card mb-3">
      <button
        onClick={() => setOpen((o) => !o)}
        className="w-full flex items-center justify-between gap-3 text-left"
        aria-expanded={open}
      >
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-brand-50 rounded-lg flex items-center justify-center flex-shrink-0">
            <Icon className="w-4 h-4 text-brand-600" />
          </div>
          <span className="font-semibold text-slate-800">{title}</span>
        </div>
        <ChevronDown
          className={`w-4 h-4 text-slate-400 flex-shrink-0 transition-transform duration-200 ${open ? 'rotate-180' : ''}`}
        />
      </button>
      {open && <div className="mt-4 border-t border-slate-100 pt-4">{children}</div>}
    </div>
  );
}

interface QAItem {
  q: string;
  a: string;
}

function FAQList({ items }: { items: QAItem[] }) {
  return (
    <ul className="space-y-3">
      {items.map((item, i) => (
        <li key={i} className="text-sm text-slate-700">
          <p className="font-semibold text-slate-800">{item.q}</p>
          <p className="mt-0.5 text-slate-600">{item.a}</p>
        </li>
      ))}
    </ul>
  );
}

function BulletList({ items }: { items: string[] }) {
  return (
    <ul className="space-y-2">
      {items.map((item, i) => (
        <li key={i} className="flex items-start gap-2 text-sm text-slate-700">
          <span className="mt-1.5 w-1.5 h-1.5 rounded-full bg-brand-400 flex-shrink-0" />
          {item}
        </li>
      ))}
    </ul>
  );
}

export default function Tutorial() {
  const [tourKey, setTourKey] = useState(0);
  const [tourVisible, setTourVisible] = useState(false);

  function handleOpenTour() {
    localStorage.removeItem('tlc_onboarding_v1');
    setTourKey((k) => k + 1);
    setTourVisible(true);
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="page-title mb-0">Tutorial</h1>
        <button onClick={handleOpenTour} className="btn-primary text-sm">
          Rivedi la guida interattiva
        </button>
      </div>
      <p className="text-slate-500 text-sm mb-6">
        Consulta questa pagina in qualsiasi momento per ritrovare le istruzioni su come usare TeLoConsiglio.io.
      </p>

      <AccordionSection Icon={BookOpen} title="Primi passi" defaultOpen>
        <BulletList
          items={[
            'Registrati via email oppure accedi con Google/Microsoft',
            'Al primo accesso completa il profilo (Comune, Partito)',
            'Imposta la linea politica dal menu Profilo',
          ]}
        />
      </AccordionSection>

      <AccordionSection Icon={ScrollText} title="Gestione atti">
        <BulletList
          items={[
            'Clicca su Atti → Nuovo atto e scegli il tipo',
            'Inserisci titolo e oggetto, poi usa "Bozza AI" per generare il corpo',
            'Aggiungi riferimenti normativi con "Suggerisci norme"',
            'Allega file e link di approfondimento',
            'Cambia stato: Bozza → Depositato → Approvato/Respinto',
          ]}
        />
      </AccordionSection>

      <AccordionSection Icon={CalendarDays} title="Sedute e ODG">
        <BulletList
          items={[
            'Crea una seduta con data, luogo e titolo',
            "Aggiungi voci all'ordine del giorno",
            'Carica i documenti per ogni voce',
            "Assegna consiglieri per l'analisi",
            "Usa \"Importa da PDF\" per caricare l'ODG ufficiale",
            'Esporta il report della seduta in PDF',
          ]}
        />
      </AccordionSection>

      <AccordionSection Icon={Sparkles} title="Profilo e AI">
        <BulletList
          items={[
            'Il profilo politico guida l\'AI nella stesura degli atti',
            'Importa automaticamente la linea dal manifesto del tuo partito',
            'Puoi modificare ogni sezione manualmente',
            'L\'AI è basata su Google Gemini (limite giornaliero gratuito: 1500 richieste)',
          ]}
        />
      </AccordionSection>

      <AccordionSection Icon={ShieldCheck} title="Ruoli e permessi">
        <BulletList
          items={[
            'Consigliere: accede agli atti, documenti e sedute del proprio gruppo',
            'Vice / Capogruppo: può invitare consiglieri e gestire il gruppo',
            'Admin: accesso completo + audit log',
          ]}
        />
      </AccordionSection>

      <AccordionSection Icon={HelpCircle} title="Domande frequenti">
        <FAQList
          items={[
            {
              q: 'Perché la bozza AI non funziona?',
              a: 'La quota giornaliera è stata raggiunta, riprova domani.',
            },
            {
              q: 'Come recupero la password?',
              a: "Usa 'Dimentico la password' nella schermata di login.",
            },
            {
              q: 'I miei documenti sono sicuri?',
              a: 'Sì, i documenti sono protetti con crittografia AES-256-GCM lato server.',
            },
            {
              q: "Posso usare l'app sullo smartphone?",
              a: 'Sì, è una PWA installabile direttamente dal browser.',
            },
          ]}
        />
      </AccordionSection>

      {tourVisible && <OnboardingTour key={tourKey} />}
    </div>
  );
}
