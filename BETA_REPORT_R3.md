# BETA REPORT R3 - Phase 1: Ruoli + Registrazione Estesa + Inviti + Agenda Restructure

**Data**: 2026-06-13  
**Branch**: claude/relaxed-hawking-pO5Tc  
**Commit HEAD pre-test**: 1096842  
**Stack**: ASP.NET Core 9 + Postgres 16 (locale) + Vite frontend

---

## Tabella Esiti Scenari

| Scenario | Descrizione | Esito | Note |
|----------|-------------|-------|------|
| A | Migration: Invitations, AgendaItems (DocumentId+Status), AspNetUsers (Gruppo) | PASS | Tutte e 2 le migration nuove applicate a runtime |
| B | Registrazione estesa (comune+partito required; me espone campi nuovi) | PASS | |
| C | Login admin seed (email senza Comune/Partito) | PASS | roles: ["Admin"] |
| D1 | POST /api/invitations come Admin → 200 con {token, url} | PASS | |
| D2 | POST /api/invitations come Consigliere → 403 | PASS | |
| D3 | GET /api/invitations come Admin → lista con status Pending | PASS | |
| D4 | GET /api/invitations/{token} pubblico → 200 con consumed:false | PASS | |
| D5 | GET /api/invitations/INVALIDTOKEN → 404 | PASS | |
| D6 | POST register con invitationToken valido → 200, comune/gruppo ereditati | PASS | |
| D7 | GET token già consumato → 200 con consumed:true | PASS (by design) | Spec dice consumed:false nel happy-path; consumed mostra true come UX flag |
| D8 | POST register con token già consumato → 400 | PASS | |
| D9 | DELETE /api/invitations/{id} come emittente → 204, status Revoked in lista | PASS | |
| D10 | DELETE /api/invitations/{id} da utente non-emittente → 404 (anti-IDOR) | PASS | |
| E1 | Promuovi utente a Capogruppo via psql + rilogin espone ruolo | PASS | |
| E2 | Capogruppo POST /api/invitations → 200 | PASS | |
| E3 | Capogruppo GET /api/users/admin → 403 | PASS | |
| F1 | Crea seduta + agenda item | PASS | |
| F2 | POST agenda/{itemId}/document con .txt → 200, documentId valorizzato | PASS | |
| F3 | GET seduta → item.documentId valorizzato, status DaAnalizzare | PASS | |
| F4 | PUT agenda/{itemId}/status {"status":1} → 200, GET conferma Analizzata | PASS | |
| F5 | PUT agenda/{itemId}/status {"status":"Analizzata"} (string) → 200 | PASS | Global JsonStringEnumConverter |
| F6 | PUT status con "InvalidValue" → 400 | PASS | |
| F7 | Upload .exe su agenda item → 400 | PASS | |
| G1 | Acts CRUD POST Mozione → 200 | PASS | |
| G2 | Sittings list/get → ok | PASS | |
| G3 | /api/users espone solo {id, displayName} | PASS | |
| G4 | IDOR: utente B legge seduta utente A → 404 | PASS | |
| G5 | /api/auth/me senza Bearer → 401 | PASS | |
| H | Frontend `npm run build` → 0 errori | PASS | 148 moduli, 418 kB JS |

---

## Bug Rilevati

### BUG-R3-01 — Minore
**Titolo**: Postgres non avviato al boot container/VM - backend tenta migrazione a vuoto  
**Severità**: Minore (setup, non codice applicativo)  
**Passi**:  
1. `pg_ctlcluster 16 main status` → "down"  
2. Avviare backend → il primo avvio fallisce silenziosamente (postgres non raggiungibile), lo stesso processo backend termina prima di esporre Swagger  
**Osservato**: Il backend non aspetta Postgres e non logga un errore critico bloccante; si avvia ma poi il loop di startup fallisce senza retry  
**File probabile**: `Program.cs` (sezione `migrate + seed`), mancanza di retry/health-check su connessione DB  
**Workaround**: Avviare postgres manualmente poi riavviare il backend

### BUG-R3-02 — Minore
**Titolo**: `GET /api/invitations/{token}` ritorna 200 con `consumed:true` anche per inviti consumati  
**Severità**: Minore (comportamento by-design ma non documentato nella spec di test)  
**Dettaglio**: La spec dice atteso `{consumed:false}` per il happy path; il controller restituisce 200+consumed:true per permettere al frontend di mostrare un messaggio. Il comportamento è corretto ma la spec non chiarisce che i token consumati restano accessibili pubblicamente.  
**Impatto UX**: Il link di invito condiviso per email mostra "invito già usato" invece di 404. Accettabile ma da documentare.  
**File**: `InvitationsController.cs` (metodo `GetByToken`)

### BUG-R3-03 — Minore
**Titolo**: Campo `Gruppo` in `Invitations` non nullable nel DB ma nullable in `CreateInvitationDto`  
**Severità**: Minore  
**Dettaglio**: La colonna `Gruppo` in `Invitations` è `text NOT NULL` nel DB (default stringa vuota se omessa). Se il campo `gruppo` non viene fornito nel body e l'utente corrente non ha `Gruppo`, viene salvata una stringa vuota anziché null.  
**Impatto**: Consistenza dati; utenti invitati ereditano stringa vuota come gruppo.  
**File**: `InvitationsController.cs` (metodo `Create`), migration `AddInvitations.cs`

---

## Coverage Non Testato

- **Scadenza invito (Expired status)**: non testato il flow di invito con ExpiresAt nel passato (richiederebbe manipolazione DB o avanzamento clock).  
- **Vice role**: non testato esplicitamente (stessa policy di Capogruppo, coperto implicitamente dalla policy `RequireCapogruppoOrAdmin`).  
- **Refresh token replay** (scenario G legacy): non ripetuto in questo round.

---

## Note Setup

- Postgres era DOWN al momento del lancio → avviato manualmente con `pg_ctlcluster 16 main start`  
- Migration `AddUsageLog` e `AddInvitations` applicate al primo avvio valido del backend  
- Backend avviato con `nohup dotnet run` dopo avvio postgres; lo startup richiede ~3-5s con DB online  
- Frontend build: 0 errori TypeScript, 0 warning Vite critici
