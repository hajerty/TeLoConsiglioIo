# TeLoConsiglio.io

Assistente AI per consiglieri comunali italiani.
Supporta la gestione del profilo politico, l'analisi di documenti, la redazione assistita di atti
(mozioni, ordini del giorno, delibere, emendamenti) con suggerimenti di riferimenti normativi,
e l'organizzazione delle sedute con ordini del giorno e assegnazioni.

## Stack

- **Backend**: ASP.NET Core 8 + EF Core + Identity + JWT, PostgreSQL via Npgsql
- **AI**: Google Gemini (`gemini-2.5-flash` di default) via HTTPS — free tier copre tutto
- **Frontend**: React 19 + Vite + TypeScript + TailwindCSS + React Router v6 + TanStack Query + Zustand
- **Persistenza**: Postgres 16

## Requisiti

- Docker + Docker Compose (consigliato)
- Oppure: .NET SDK 8, Node.js 22+, PostgreSQL 16 locali

## Avvio rapido con Docker

```bash
cp .env.example .env
# (opzionale) modifica .env e imposta GEMINI_API_KEY se vuoi usare le feature AI
# API key gratis (no carta richiesta): https://aistudio.google.com/apikey
docker compose up --build
```

Servizi esposti:

- Frontend: <http://localhost:8080>
- API (Swagger): <http://localhost:5000/swagger>
- Postgres: localhost:5432

### Credenziali di seed

Al primo avvio viene creato automaticamente:

- Email: `admin@teloconsiglio.io`
- Password: `Admin!2026`

**ATTENZIONE**: cambia la password subito dopo il primo accesso in produzione.

## Avvio senza Docker

### Postgres

Avvia un'istanza locale, crea un DB `teloconsiglio` con utente/password a piacere.

### Backend

```bash
cd backend
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=teloconsiglio;Username=postgres;Password=postgres"
export GEMINI_API_KEY="AIza..."   # opzionale, gratis su https://aistudio.google.com/apikey
dotnet run --project TeLoConsiglio.Api
# API in ascolto su http://localhost:5000 (o porta da launchSettings)
```

Le migration EF sono applicate automaticamente all'avvio.

### Frontend

```bash
cd frontend
npm install
echo "VITE_API_URL=http://localhost:5000" > .env.local
npm run dev
# http://localhost:5173
```

## Variabili d'ambiente

Vedi `.env.example`. Le principali:

| Variabile | Descrizione |
|---|---|
| `POSTGRES_DB/USER/PASSWORD` | Configurazione Postgres |
| `JWT__Key` | Chiave segreta JWT (almeno 32 caratteri) |
| `JWT__Issuer` / `JWT__Audience` | Issuer/Audience JWT |
| `GEMINI_API_KEY` | Chiave Google Gemini (se mancante, gli endpoint AI rispondono 503). Free tier: <https://aistudio.google.com/apikey> |
| `GEMINI_MODEL` | Modello Gemini (default `gemini-2.5-flash`, gratis nel free tier) |
| `BUDGET_MONTHLY_USD` | Cap di spesa AI mensile per utente in USD (default `5.0`) |
| `GOOGLE_CLIENT_ID/SECRET` | OAuth Google (facoltativo) |
| `MICROSOFT_CLIENT_ID/SECRET` | OAuth Microsoft (facoltativo) |
| `Frontend__Url` | URL frontend per CORS |
| `VITE_API_URL` | URL backend usato dal frontend |

## API principali

Tutte le rotte (eccetto `/api/auth/*` e `/health`) richiedono header `Authorization: Bearer <jwt>`.

### Autenticazione

- `GET  /api/auth/providers` - elenco provider attivi (password + Google/MS opzionali)
- `POST /api/auth/register` - registrazione consigliere (body: `email, password, fullName, comune, partito, gruppo?, invitationToken?`)
- `POST /api/auth/login` - login con email/password
- `POST /api/auth/refresh` - refresh token
- `GET  /api/auth/me` - utente corrente (espone `comune, partito, gruppo, roles`)

### Partiti (manifesti)

- `GET  /api/party-manifests` — elenco partiti `{ key, fullName }[]` (pubblico, no auth)
- `GET  /api/party-manifests/{key}` — testo linea politica del partito (auth richiesta)

### Profilo politico

- `GET  /api/profile/political` — restituisce `lineaPoliticaMd, argomentiForti, temiInteresse, lineaPoliticaSource`; auto-popola dal manifesto partito se vuoto
- `PUT  /api/profile/political` — aggiorna profilo; imposta `lineaPoliticaSource=Manuale` se il testo differisce dal manifesto
- `POST /api/profile/political/reset-linea` — ri-applica il manifesto del partito (resetta source=Partito)
- `GET  /api/profile/programs`
- `POST /api/profile/programs` (multipart upload)
- `DELETE /api/profile/programs/{id}`

### Documenti

- `GET  /api/documents`
- `GET  /api/documents/{id}`
- `POST /api/documents` (multipart upload + estrazione testo)
- `POST /api/documents/{id}/summarize` (AI: riassunto, punti chiave, criticita')
- `DELETE /api/documents/{id}`

### Atti

- `GET  /api/acts`
- `GET  /api/acts/{id}`
- `POST /api/acts`
- `PUT  /api/acts/{id}`
- `DELETE /api/acts/{id}`
- `POST /api/acts/{id}/ai-draft` - genera bozza basata sulla linea politica
- `POST /api/acts/{id}/legal-refs/suggest` - suggerisce riferimenti normativi
- `POST /api/acts/{id}/legal-refs/confirm` - alias di insert
- `POST /api/acts/{id}/legal-refs/insert` - inserisce i riferimenti selezionati nel testo

### Dashboard

- `GET /api/dashboard` - dashboard utente: documenti recenti (top 5), prossima seduta del Comune, voci ODG da analizzare, contatori (atti in bozza, sedute future, inviti pending)

### Sedute

- `GET  /api/sittings` — lista sedute; query params: `from`, `to` (DateTime), `q` (full-text su Titolo+Luogo), `period` (`All|Past|Upcoming`), `page`, `pageSize`; header `X-Total-Count`
- `GET  /api/sittings/{id}`
- `POST /api/sittings`
- `DELETE /api/sittings/{id}`
- `GET  /api/sittings/{id}/report.pdf` - scarica report PDF della seduta (solo voci con stato `ApprovataPerSeduta`; header `Content-Disposition: attachment`)
- `POST /api/sittings/{id}/agenda`
- `PUT  /api/sittings/agenda/{itemId}`
- `DELETE /api/sittings/agenda/{itemId}`
- `POST /api/sittings/agenda/{itemId}/document` - carica documento per un punto ODG (multipart, stessa whitelist upload)
- `PUT  /api/sittings/agenda/{itemId}/status` - aggiorna stato punto ODG (`DaAnalizzare|Analizzata|ApprovataPerSeduta`)
- `GET  /api/sittings/{id}/agenda/{itemId}/document-suggestions` - top 5 documenti usati in altri punti ODG dell'utente con descrizione simile (score 100=exact, 50=substring)
- `POST /api/sittings/agenda/{itemId}/clone-document` - copia DocumentId da un altro AgendaItem (body: `{sourceAgendaItemId}`; link al file, nessuna copia fisica)

### Inviti

- `POST /api/invitations` - crea invito (solo Capogruppo/Vice/Admin; body: `{nome, cognome, email, gruppo?, comune?}`; risposta: `{token, url}`)
- `GET  /api/invitations` - lista inviti emessi (solo Capogruppo/Vice/Admin; paginata con `?page=&pageSize=`; header `X-Total-Count`)
- `GET  /api/invitations/{token}` - dati pubblici invito per pre-fillare form registrazione (no auth; 404 se scaduto/revocato)
- `DELETE /api/invitations/{id}` - revoca invito (solo emittente o Admin)

### Utenti

- `GET /api/users`

### Usage / Budget AI

- `GET /api/usage/me` - spesa AI dell'utente nel mese corrente, breakdown per operazione e per giorno, limite mensile
- `GET /api/usage/admin` - (solo Admin) spesa AI di tutti gli utenti nel mese corrente

## Costi e budget AI

Il provider AI è **Google Gemini**. Sul free tier le chiamate sono **gratis** (zero USD):
quote indicative 1500 richieste/giorno, contesto fino a 1M token, per modello `flash`.

### Ottieni una API key gratis

1. Vai su <https://aistudio.google.com/apikey>
2. Login con account Google (non serve carta di credito)
3. **Create API key** → copia il valore (inizia con `AIza...`)
4. Incollalo in `.env` come `GEMINI_API_KEY=...`

### Pricing (USD per milione di token)

Riferimento ufficiale: <https://ai.google.dev/pricing>. Valori usati dal calcolatore interno:

| Modello | Input | Output | Note |
|---|---|---|---|
| `gemini-2.5-flash` (default) | $0 | $0 | free tier, qualità ottima per atti amministrativi |
| `gemini-2.5-flash-lite` | $0 | $0 | free tier, più veloce / meno capace |
| `gemini-2.5-pro` | $1.25 | $10 | tier a pagamento, top quality |

Con `flash` (default) il costo per utente è **0 USD**: il budget guard è una rete di sicurezza
attiva ma non scatta mai. Se passi a `gemini-2.5-pro` (a pagamento), `BUDGET_MONTHLY_USD` torna a fare cap.

### Cambiare modello

```bash
# .env locale o env var Render/Docker
GEMINI_MODEL=gemini-2.5-flash-lite   # ancora più veloce, gratis
# oppure
GEMINI_MODEL=gemini-2.5-pro          # più capace, a pagamento
```

### Cap di spesa mensile per utente

`BUDGET_MONTHLY_USD` (default `5.0`):

- ogni chiamata AI viene loggata in `UsageLog` con token e costo stimato
- prima di eseguire l'endpoint AI, `BudgetGuardAttribute` somma la spesa del mese corrente dell'utente
- a >= 80% del budget aggiunge header `X-Budget-Warning: 80%` e logga warning
- a >= 100% restituisce `HTTP 429` con body `{ "error":"budget_exceeded", "spentUsd":..., "limitUsd":... }`

Sui modelli free il costo è 0, quindi il cap non si attiva mai — è una rete di sicurezza per quando si migra su tier a pagamento.

L'utente può comunque consultare token consumati e operazioni via `GET /api/usage/me`.

### Cap globale lato Google

Per un cap di sicurezza GLOBALE a livello di progetto Google Cloud (indipendente dall'utente), configura **Quotas** in Google Cloud Console: <https://console.cloud.google.com/> → APIs & Services → Quotas → cerca *Generative Language API*.

## Flusso "Suggerimento riferimenti legislativi"

1. Apri un atto (`/atti/{id}`) e clicca **Suggerisci riferimenti**.
2. Il backend invia il testo a Gemini, riceve una lista di citazioni pertinenti (TUEL, Costituzione, regolamenti) e la persiste.
3. Si apre una **modal** con checkbox accanto a ciascun riferimento (citazione + motivazione).
4. Scegli se inserire **in coda al testo** oppure **al posto del placeholder `[[REF]]`**.
5. Premi **Inserisci selezionati** -> il backend aggiorna `BodyMd` dell'atto e marca i riferimenti come confermati.

## Screenshots

_Placeholder: aggiungere screenshot dei flussi (Dashboard, Profilo, Editor atto con modal riferimenti, Seduta)._

## Struttura del repository

```
.
├── backend/
│   ├── TeLoConsiglio.Api/          # ASP.NET Core (controller, Program.cs, seed)
│   ├── TeLoConsiglio.Domain/       # Entita'
│   ├── TeLoConsiglio.Infrastructure/  # DbContext, migrations, services (Gemini AI, extractor)
│   └── Dockerfile
├── frontend/
│   ├── src/api/                    # axios client + endpoint wrappers
│   ├── src/auth/                   # store zustand + ProtectedRoute
│   ├── src/components/             # Layout, Sidebar, Modal
│   ├── src/pages/                  # Login, Dashboard, Atti, AttoEditor, ...
│   └── Dockerfile
├── docker-compose.yml
├── .env.example
└── README.md
```

## Deploy gratuito (Netlify + Render + Neon)

Stack 100% free tier:

- **Frontend** → [Netlify](https://app.netlify.com) (statico)
- **Backend** → [Render](https://dashboard.render.com) (web service Docker, free, va in sleep dopo 15 min di inattività)
- **Database** → [Neon](https://console.neon.tech) (Postgres serverless, 0.5 GB free)

### 1. Crea il database su Neon

1. Registrati su Neon, crea un nuovo progetto (regione EU per minimizzare latenza con Render `frankfurt`)
2. Copia la **connection string** (formato `postgresql://user:pass@ep-xxx.eu-central-1.aws.neon.tech/neondb?sslmode=require`)
3. Convertila in formato Npgsql:
   ```
   Host=ep-xxx.eu-central-1.aws.neon.tech;Database=neondb;Username=user;Password=pass;SslMode=Require;Trust Server Certificate=true
   ```

### 2. Deploy backend su Render

1. Su Render dashboard → **New +** → **Blueprint**
2. Connetti il repo GitHub, Render legge `render.yaml` e crea il servizio
3. Nel pannello del servizio appena creato, **Environment** → aggiungi a mano:
   - `ConnectionStrings__Default` = la stringa Npgsql del passo 1
   - `Frontend__Url` = URL Netlify (es. `https://teloconsiglio.netlify.app`) — lo metterai dopo il punto 3
   - `GEMINI_API_KEY` = la tua API key Google Gemini (opzionale ma necessaria per le funzioni AI). Gratis su <https://aistudio.google.com/apikey>
4. Render builda l'immagine Docker e applica le migrations all'avvio. URL finale tipo `https://teloconsiglio-api.onrender.com`

### 3. Deploy frontend su Netlify

1. Su Netlify → **Add new site** → **Import from Git** → seleziona il repo
2. Netlify legge `netlify.toml` (build dir `frontend`, output `frontend/dist`)
3. Prima del primo deploy, **Site configuration** → **Environment variables**:
   - `VITE_API_URL` = URL del backend Render (es. `https://teloconsiglio-api.onrender.com`)
4. Trigger deploy
5. Torna su Render, aggiorna `Frontend__Url` con l'URL Netlify finale (es. `https://teloconsiglio.netlify.app`) e riavvia il servizio per applicare CORS

### Caveat free tier

- Render free: il backend va in sleep dopo 15 min; la prima request dopo lo sleep ha cold start di ~30 secondi
- Neon free: 0.5 GB storage, branching limitato, compute autosuspend dopo 5 min (riprende automaticamente)
- Netlify free: 100 GB/mese di banda, build 300 min/mese
- OAuth Google/Microsoft: vanno configurati con i domini pubblici nei rispettivi cloud console

## Note di sicurezza

- Cambia `JWT__Key` e la password dell'admin in produzione.
- Le feature AI sono disabilitate (HTTP 503) se manca `GEMINI_API_KEY`: il resto dell'app funziona normalmente.
- Gli upload risiedono in `backend/uploads/` (volume Docker `uploads` in compose).
