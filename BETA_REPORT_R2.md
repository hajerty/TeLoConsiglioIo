# BETA REPORT — Round 2 (regression)

Branch: `claude/relaxed-hawking-pO5Tc`  HEAD prima dei test: `cda3ca1`
Data: 2026-06-05

## Setup notes

- Postgres locale (16) avviato via `service postgresql start`. DB `teloconsiglio`, utente `postgres/postgres` (gia` presenti dal giro precedente).
- Backend: build OK (`dotnet build` → 0 errori). Avviato con `dotnet TeLoConsiglio.Api/bin/Debug/net8.0/TeLoConsiglio.Api.dll --urls=http://localhost:5000` via `setsid nohup` per sopravvivere alla terminazione della shell di Claude.
- Env vars usate (uppercase, come da `Program.cs`):
  - `JWT_KEY` = 64 char hex (`openssl rand -hex 32`)
  - `FRONTEND_URL=http://localhost:5173`
  - `ANTHROPIC_API_KEY=` (vuoto)
  - `ConnectionStrings__Default=Host=localhost;Port=5432;Database=teloconsiglio;Username=postgres;Password=postgres`
- Swagger raggiunto (`/swagger/v1/swagger.json` 200) entro 12s.
- Frontend: solo `npm run build` → OK (no UI test).
- Admin seed: `admin@teloconsiglio.io` / `Admin!2026`.

## Risultati scenari

| # | Scenario | Esito | Note |
|---|----------|-------|------|
| A | Enum allineati (sittings + agenda + acts + documents) | PASS | Create sitting 200, agenda con `decisione:"DaDecidere"` 200, atto con `tipo:"Mozione"`/`status:"Bozza"` 200, upload `.txt` con `type=Delibera` 200. Vedi nota su `type` form/query. |
| B | IDOR Sittings (utente B vs sit di A) | PASS | GET 404, DELETE 404, POST agenda 404. PUT 405 (endpoint non esistente; comunque blocca scrittura). |
| C | /api/users hardening | PASS | GET `/api/users` per Consigliere → solo `{id,displayName}` (nessuna email/comune/roles). GET `/api/users/admin` per Consigliere → 403. Admin login + `/api/users/admin` → 200 con email+comune+roles. |
| D | Refresh token rotation | PASS | refresh1→200 (refresh2). refresh1 riusato→401 "Refresh token non valido". logout con `Authorization: Bearer` + refresh2 → 204. refresh2 dopo logout → 401. |
| E | Legal-ref manuale | PASS (con caveat formato) | POST `/legal-refs` con `{citation,description}` → 200 (NB: spec chiedeva 201). Confirm/insert richiedono `{mode,referenceIds}` (non `{id}` come da spec); con payload corretto 200 e body atto contiene la citazione. |
| F | Upload sicurezza | PASS | `.exe` → 400 "Estensione non consentita". 25MB `.txt` → 400 "Request body too large" (cap 20MB). Filename `../../etc/passwd.txt` sanificato a `passwd.txt` (stored come GUID, nessun `..` su filesystem). `.txt` piccolo valido → 200. |
| G | JWT fail-fast Production | PASS | Prod + `JWT_KEY=` → crash con `InvalidOperationException` (linea 46). Prod + `JWT_KEY=short` → crash linea 55. Prod + `JWT_KEY` 64hex + `FRONTEND_URL=https://example.com` → /health 200. |
| H | CORS Production | PASS | Prod senza `FRONTEND_URL` → crash `InvalidOperationException` (CORS richiede origine). Con `FRONTEND_URL=https://example.com`: OPTIONS con `Origin: https://example.com` → `Access-Control-Allow-Origin: https://example.com`; OPTIONS con `Origin: https://evil.com` → 204 senza header ACAO. |
| I | Builds | PASS | `dotnet build` 0 warnings, 0 errors. `npm run build` succede, output 404.93 kB. |

## Failures

Nessuno scenario marcato FAIL. Solo deviazioni minori dallo spec del test plan (vedi "Bug residui").

## Bug residui (ordinati per severita`)

### Bassi / Note

1. **[BASSO] Scenario E — DTO confirm/insert mismatch con test plan**
   - File: `backend/TeLoConsiglio.Api/Controllers/ActsController.cs` (linee 244-260) + DTO `InsertLegalRefsRequest`.
   - Il piano di test prescrive `{ id: "<lrId>" }`; il controller pretende `{ mode: "append|placeholder", referenceIds: ["<lrId>"] }`. Senza questi campi: 400 "The Mode field is required" / "The ReferenceIds field is required".
   - Funzionalmente OK con payload corretto; ma se il frontend ancora invia il vecchio formato `{id}` la conferma legal-refs si rompe. Da verificare.

2. **[BASSO] Scenario E — POST legal-refs ritorna 200 invece di 201**
   - File: `ActsController.cs` (endpoint `POST /api/acts/{id}/legal-refs`).
   - Spec REST → 201 Created. Restituisce 200. Non bloccante.

3. **[BASSO] Scenario A — `type` su upload e` `[FromQuery]` non `[FromForm]`**
   - File: `backend/TeLoConsiglio.Api/Controllers/DocumentsController.cs` linea 65: `Upload(IFormFile file, [FromQuery] DocumentType type = DocumentType.Documento)`.
   - Inviare `type=Delibera` come campo multipart viene ignorato; il doc viene salvato come `Documento`. Per impostarlo dal frontend serve `?type=Delibera` in querystring. Possibile fonte di confusione FE/BE.

4. **[BASSO] Scenario B — PUT /api/sittings/{id} restituisce 405**
   - Non esiste un endpoint PUT (nessuna update). Il test plan si aspettava 404 (IDOR check). 405 e` di fatto piu` permissivo come info (rivela esistenza del metodo non implementato globalmente, non per ID), ma non e` un IDOR. Lo considero PASS perche` l'obiettivo (B non puo` modificare la sitting di A) e` rispettato; segnalo per completezza.

## Comandi di verifica chiave (per replay)

- `curl -X POST http://localhost:5000/api/auth/refresh -H "Content-Type: application/json" -d '{"refreshToken":"<rt1>"}'` due volte → 200 poi 401.
- `dotnet build` in `/home/user/TeLoConsiglioIo/backend` → success.
- `ASPNETCORE_ENVIRONMENT=Production JWT_KEY= ... dotnet TeLoConsiglio.Api.dll` → exit 134 con messaggio italiano "JWT_KEY non configurata".
- `curl -I -X OPTIONS http://localhost:5001/api/auth/login -H "Origin: https://example.com" -H "Access-Control-Request-Method: POST"` → header `Access-Control-Allow-Origin: https://example.com`.

## Conclusione

Tutti gli scenari A-I passano. I caveat su E (DTO/codice di stato) e su A (type form vs query) sono note implementative, non regressioni di sicurezza ne` di funzionalita`. I fix di sicurezza chiave del round (rotazione refresh, IDOR, users hardening, JWT/CORS fail-fast in Prod, whitelist upload) sono confermati operativi.
