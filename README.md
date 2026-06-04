# TeLoConsiglio.io

Assistente AI per consiglieri comunali italiani.
Supporta la gestione del profilo politico, l'analisi di documenti, la redazione assistita di atti
(mozioni, ordini del giorno, delibere, emendamenti) con suggerimenti di riferimenti normativi,
e l'organizzazione delle sedute con ordini del giorno e assegnazioni.

## Stack

- **Backend**: ASP.NET Core 8 + EF Core + Identity + JWT, PostgreSQL via Npgsql
- **AI**: Anthropic Claude (`claude-opus-4-5`) via HTTPS
- **Frontend**: React 19 + Vite + TypeScript + TailwindCSS + React Router v6 + TanStack Query + Zustand
- **Persistenza**: Postgres 16

## Requisiti

- Docker + Docker Compose (consigliato)
- Oppure: .NET SDK 8, Node.js 22+, PostgreSQL 16 locali

## Avvio rapido con Docker

```bash
cp .env.example .env
# (opzionale) modifica .env e imposta ANTHROPIC_API_KEY se vuoi usare le feature AI
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
export ANTHROPIC_API_KEY="sk-ant-..."   # opzionale
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
| `ANTHROPIC_API_KEY` | Chiave Claude (se mancante, gli endpoint AI rispondono 503) |
| `ANTHROPIC_MODEL` | Modello Claude (default `claude-opus-4-5`) |
| `GOOGLE_CLIENT_ID/SECRET` | OAuth Google (facoltativo) |
| `MICROSOFT_CLIENT_ID/SECRET` | OAuth Microsoft (facoltativo) |
| `Frontend__Url` | URL frontend per CORS |
| `VITE_API_URL` | URL backend usato dal frontend |

## API principali

Tutte le rotte (eccetto `/api/auth/*` e `/health`) richiedono header `Authorization: Bearer <jwt>`.

### Autenticazione

- `GET  /api/auth/providers` - elenco provider attivi (password + Google/MS opzionali)
- `POST /api/auth/register` - registrazione consigliere
- `POST /api/auth/login` - login con email/password
- `POST /api/auth/refresh` - refresh token
- `GET  /api/auth/me` - utente corrente

### Profilo politico

- `GET  /api/profile/political`
- `PUT  /api/profile/political`
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

### Sedute

- `GET  /api/sittings`
- `GET  /api/sittings/{id}`
- `POST /api/sittings`
- `DELETE /api/sittings/{id}`
- `POST /api/sittings/{id}/agenda`
- `PUT  /api/sittings/agenda/{itemId}`
- `DELETE /api/sittings/agenda/{itemId}`

### Utenti

- `GET /api/users`

## Flusso "Suggerimento riferimenti legislativi"

1. Apri un atto (`/atti/{id}`) e clicca **Suggerisci riferimenti**.
2. Il backend invia il testo a Claude, riceve una lista di citazioni pertinenti (TUEL, Costituzione, regolamenti) e la persiste.
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
│   ├── TeLoConsiglio.Infrastructure/  # DbContext, migrations, services (Anthropic, extractor)
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

## Note di sicurezza

- Cambia `JWT__Key` e la password dell'admin in produzione.
- Le feature AI sono disabilitate (HTTP 503) se manca `ANTHROPIC_API_KEY`: il resto dell'app funziona normalmente.
- Gli upload risiedono in `backend/uploads/` (volume Docker `uploads` in compose).
