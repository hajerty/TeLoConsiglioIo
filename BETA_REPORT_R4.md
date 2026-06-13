# BETA REPORT R4 - Phase 2: Party Manifests + Profilo Indirizzo + Allegati Atti + PDF Export

**Data**: 2026-06-13  
**Branch**: claude/relaxed-hawking-pO5Tc  
**Commit HEAD pre-test**: cb00d7c  
**Stack**: ASP.NET Core 9 + Postgres 16 (locale) + Vite frontend

---

## Tabella Esiti Scenari

| Scenario | Descrizione | Esito | Note |
|----------|-------------|-------|------|
| A1 | GET /api/party-manifests (anon) → array con tutti e 9 i partiti, campi `key` + `fullName` | PASS | |
| A2 | GET /api/party-manifests/PD (auth) → `lineaPoliticaMd` non vuoto, `fullName` corretto | PASS | |
| A3 | GET /api/party-manifests/INVALIDO → 404 | PASS | |
| A4 | GET /api/party-manifests/PD senza token → 401 | PASS | |
| B1 | GET /api/profile/political primo accesso (utente PD) → autopopolata da manifesto, `lineaPoliticaSource=Partito`, `argomentiForti=[]`, `temiInteresse=[]` | PASS | |
| B2 | PUT /api/profile/political con nuovo testo → `lineaPoliticaSource=Manuale` | PASS | |
| B3 | PUT con `argomentiForti=["scuola","trasporti"]` e `temiInteresse=["ambiente"]` → salvati, GET conferma | PASS | |
| B4 | POST /api/profile/political/reset-linea → ripristina dal manifesto PD, `lineaPoliticaSource=Partito`, campi argomenti preservati | PASS | |
| C1 | POST /api/acts/{id}/attachments con `.txt` → 200, risposta include `id, originalName, contentType, sizeBytes, createdAt` | PASS | |
| C2 | GET /api/acts/{id}/attachments → lista contiene il nuovo allegato | PASS | |
| C3 | GET /api/acts/{id}/attachments/{attId}/download → Content-Disposition con filename, contenuto corretto | PASS | |
| C4 | POST con `.exe` → 400 `Estensione non consentita` | PASS | |
| C5 | POST con file >20MB → 400 | PASS | Errore formato non uniforme (vedi BUG-R4-01) |
| C6 | DELETE /api/acts/{id}/attachments/{attId} → 204, GET conferma lista vuota | PASS | |
| C7 | IDOR: utente B GET attachments di atto utente A → 404 | PASS | |
| C8 | IDOR: utente B POST attachment su atto utente A → 404 | PASS | |
| C9 | Path traversal nel nome file (`../../etc/passwd.txt`) → salvato come `passwd.txt` (sanitizzato via `Path.GetFileName`) | PASS | |
| D1 | POST /api/acts con `referenceUrls` e `referenceNotesMd` → 200, GET conferma valori | PASS | |
| D2 | PUT con `referenceUrls:[]` e `referenceNotesMd:null` → svuotati, GET conferma | PASS | |
| E1 | GET /api/acts/{id}/pdf → 200, `Content-Type: application/pdf`, `Content-Disposition` con filename slugged | PASS | |
| E2 | PDF riconosciuto come `PDF document, version 1.7, 1 page(s)` da `file` | PASS | pdfinfo non disponibile, SKIP |
| E3 | IDOR: utente B chiama /api/acts/{id}/pdf di atto altrui → 404 | PASS | |
| F | POST /api/acts/{id}/ai-draft → 503 (GEMINI_API_KEY non configurata) | SKIP | Atteso, non è un bug |
| G1 | Login admin seed (admin@teloconsiglio.io / Admin!2026) → PASS, roles: ["Admin"] | PASS | |
| G2 | Lista atti, sedute, documenti → ok | PASS | |
| G3 | /api/users (consigliere normale) → solo `{id, displayName}`, no email | PASS | |
| G4 | Frontend `npm run build` → 0 errori TS, 148 moduli, 431 kB JS | PASS | |

---

## Bug Rilevati

### BUG-R4-01 — Minore
**Titolo**: Errore >20MB usa formato validation standard ASP.NET, non `{error: ...}` custom  
**Severità**: Minore (inconsistenza UX)  
**Passi**:
1. POST /api/acts/{id}/attachments con file da 21 MB
2. Il limite `[RequestSizeLimit]` viene colpito prima che il body venga letto
**Osservato**: `{"title":"Validazione fallita","status":400,"errors":{"":["Failed to read the request form. Request body too large. The max request body size is 20971520 bytes."]}}`  
**Atteso**: `{"error": "File troppo grande (max 20 MB)"}` (come per l'oversize check interno di `UploadValidator`)  
**File probabile**: `ActsController.cs` (dipende dall'ordine middleware; il filtro ASP.NET scatta prima di `UploadValidator.Validate`)  
**Impatto**: Il frontend deve gestire due formati distinti di errore per la stessa casistica

### BUG-R4-02 — Informativo
**Titolo**: Nota: admin seed usa password `Admin!2026`, non quella indicata nei report precedenti  
**Severità**: Informativo (documentazione)  
**Dettaglio**: In BETA_REPORT_R3 si usava `Admin@1234!` come tentativo ma la password reale nel seed è `Admin!2026`. Annotare in documentazione di onboarding.  
**File**: `backend/TeLoConsiglio.Api/Seed/DataSeeder.cs`

---

## Coverage Non Testato

- **Scenario F (AI draft con allegati)**: SKIP per assenza `GEMINI_API_KEY`; il flusso di costruzione del prompt con allegati è visibile nel codice (`BuildAttachmentsContext`) ma non verificabile a runtime
- **pdfinfo**: tool non disponibile nell'ambiente; confermato solo via `file` che riconosce 1 pagina
- **Upload allegato su agenda item**: non fatto in questo round (coperto in R3)
- **Scadenza inviti Expired**: non ripetuto

---

## Note Setup

- Postgres era DOWN → avviato manualmente con `pg_ctlcluster 16 main start`
- Backend avviato con env vars minime (`JWT__Key`, `Frontend__Url`, `ConnectionStrings__Default`, `ASPNETCORE_ENVIRONMENT=Development`, `GEMINI_API_KEY=""`)
- Nessuna migration nuova rilevata rispetto a R3
- Frontend build: 0 errori TypeScript, 0 warning critici Vite, 148 moduli, 431.21 kB JS
