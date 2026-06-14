# E2E Multi-Role Test Report — Round 1

**Data:** 2026-06-14  
**Branch:** `claude/relaxed-hawking-pO5Tc`  
**Backend:** pre-compiled DLL da `bin/Debug/net8.0/` (build sorgente bloccata — vedi BUG-001)  
**DB:** `teloconsiglio` su PostgreSQL 16 (locale)  
**GEMINI_API_KEY:** vuota → endpoint AI skippati (503 atteso)  

---

## Setup Notes

- PostgreSQL 16 cluster era `down`; avviato con `pg_ctlcluster 16 main start`.
- `dotnet build` fallisce con 3 errori su `AuditLogger.cs` (vedi BUG-001); il backend è stato avviato dai DLL pre-compilati (`bin/Debug/net8.0/`).
- Tutte le personas create via `POST /api/auth/register`; ruoli assegnati via `psql` con INSERT in `AspNetUserRoles`.
- Rilogin effettuato dopo assegnazione ruoli per ottenere JWT aggiornati.
- Utente legacy `userc@test.it` (Capogruppo, Gruppo PD) già presente nel DB da run precedenti — ha ricevuto correttamente la notifica "Atto depositato" insieme a `cap-pd@test.it` (comportamento corretto: tutti i Capogruppo dello stesso gruppo sono notificati).
- I nomi dei campi nelle DTO usano nomi italiani (`Titolo`, `Luogo`, `Data`, `Descrizione`, `Ordine`, `Decisione`, `Tipo`, `Oggetto`) non inglesi come indicato nella spec e2e-tester.md — questo è stato un problema di documentazione, non di codice.

---

## Journey 1: Onboarding via Invito

| Step | Descrizione | Esito |
|------|-------------|-------|
| J1.S1 | cap-pd invia invito a cons-pd2 (POST /api/invitations con nome+cognome+email) | PASS |
| J1.S1 | Email di invito loggata per cons-pd2 (ConsoleEmailSender) | PASS |
| J1.S1 | URL invito generata con `/register?invite=<token>` | PASS |
| J1.S2 | GET /api/invitations/{token} pubblico → dati prefillati | PASS |
| J1.S2 | consumed=false prima della registrazione | PASS |
| J1.S3 | cons-pd2 si registra con invitationToken | PASS |
| J1.S3 | cons-pd2 ha ruolo Consigliere post-registrazione | PASS |
| J1.S3 | cons-pd2 in Gruppo PD come indicato nell'invito | PASS |
| J1.S4 | Invito marcato consumed=true dopo registrazione | PASS |
| J1.S5 | Riuso del token scaduto/consumato → 400 | PASS |
| J1.S6 | Consigliere POST /api/invitations → 403 | PASS |

**Esito: PASS** — 11/11 step OK. Il DTO `CreateInvitationDto` richiede `nome` e `cognome` obbligatori (non presenti nella spec e2e-tester.md, ma comportamento corretto).

---

## Journey 2: Ciclo di Vita Seduta

| Step | Descrizione | Esito |
|------|-------------|-------|
| J2.S1 | cap-pd crea seduta (campi: `titolo`, `data`, `luogo`) | PASS |
| J2.S2 | Aggiunge 3 voci ODG (campi: `descrizione`, `ordine`, `decisione`) | PASS |
| J2.S3 | Assegna cons-pd1 a item 1 via PUT (campo `assignedUserIds`) | PASS |
| J2.S3 | Assegna cons-pd2 a item 2 via PUT | PASS |
| J2.S3 | Email assegnazione inviata a cons-pd1 e cons-pd2 | PASS |
| J2.S4 | Upload documento .txt su item 1 | PASS |
| J2.S4 | Notifica "Nuovo documento" inviata agli assegnati | PASS |
| J2.S5 | Aggiorna decisione item 1 → Approvare (decisione=1) via PUT | PASS |
| J2.S5 | Notifica "Decisione aggiornata" inviata a cons-pd1 | PASS |
| J2.S6 | cons-pd1 GET seduta di cap-pd → 404 (non propria) | PASS |
| J2.S7 | Aggiorna status item 1 → ApprovataPerSeduta (2) via PUT /agenda/{id}/status | PASS |
| J2.S8 | GET /api/sittings/{id}/report.pdf → 200 PDF | PASS |

**Esito: PASS** — 12/12 step OK.

**Nota documentazione:** La spec e2e-tester.md menziona endpoint separati (`POST /api/sittings/agenda/{itemId}/assignments`, `PUT /api/sittings/agenda/{itemId}/decision`) che non esistono. Le operazioni si effettuano via `PUT /api/sittings/agenda/{itemId}` con il DTO completo. Non è un bug ma una discrepanza nella documentazione.

---

## Journey 3: Atto + Notifica Capogruppo

| Step | Descrizione | Esito |
|------|-------------|-------|
| J3.S1 | cons-pd1 crea Mozione (tipo=0, titolo, oggetto) | PASS |
| J3.S2 | Aggiunge riferimento legale manuale (POST /api/acts/{id}/legal-refs) | PASS |
| J3.S3 | Aggiorna stato Bozza → Depositato (status=1) via PUT /api/acts/{id} | PASS |
| J3.S3 | Email "Atto depositato" inviata a cap-pd@test.it | PASS |
| J3.S4 | cons-fdi crea e deposita Mozione FdI | PASS |
| J3.S4 | Email "Atto depositato" inviata a cap-fdi@test.it | PASS |
| J3.S4 | Isolamento notifiche: cap-pd NON notificato per atto FdI | PASS |
| J3.S5 | Export PDF atto → GET /api/acts/{id}/pdf → 200 | PASS |
| J3.SKIP | ai-draft senza GEMINI_API_KEY → 503 (atteso) | SKIP |

**Esito: PASS** — 8/8 step testati, 1 SKIP (AI).

**Nota:** Il `PUT /api/acts/{id}/status` non esiste come endpoint separato; lo status si aggiorna via `PUT /api/acts/{id}` con il DTO completo. Discrepanza documentazione.

---

## Journey 4: Permission Cross-Role (IDOR + Policy)

| Step | Descrizione | Esito |
|------|-------------|-------|
| J4.S1 | cap-roma GET seduta Milano PD → 404 (IDOR corretto) | PASS |
| J4.S2 | cap-roma POST document su seduta non sua → 404 | PASS |
| J4.S3 | cap-fdi POST invitation → 200 (Capogruppo autorizzato) | PASS |
| J4.S4 | GET /api/users → 200, espone solo {id, displayName} | PASS |
| J4.S4 | GET /api/users non espone email né altri dati sensibili | PASS |
| J4.S5 | GET /api/users/admin con token Capogruppo → 403 | PASS |
| J4.S6 | cons-pd1 POST /api/invitations → 403 (Consigliere non può) | PASS |
| J4.S7 | cons-pd1 POST /api/sittings → 200 (tutti autenticati possono creare sedute) | PASS* |
| J4.S8 | vice-pd POST /api/invitations → 200 (Vice autorizzato) | PASS |
| J4.S9 | vice-pd POST /api/sittings/import-pdf → 503 (no Gemini, policy OK) | PASS |
| J4.S10 | Admin GET /api/users/admin → 200 con campi completi (email, fullName, ruoli) | PASS |
| J4.S11 | Admin GET /api/admin/encryption-status → 200 | PASS |
| J4.S12 | GET /api/auth/me senza token → 401 | PASS |

**Esito: PASS** — 13/13 step OK.

*J4.S7: Il controller `/api/sittings` usa `[Authorize]` senza restrizione di ruolo: tutti gli utenti autenticati possono creare sedute. Questo è coerente con il design (ogni consigliere ha le sue sedute personali).

---

## Journey 5: Opt-Out Notifiche Email

| Step | Descrizione | Esito |
|------|-------------|-------|
| J5.S1 | cons-pd1 PUT /api/profile/notifications {emailNotificationsEnabled: false} → 200 | PASS |
| J5.S2 | cap-pd assegna cons-pd1 a nuovo agenda item con opt-out attivo | PASS |
| J5.S3 | Nessuna email inviata a cons-pd1 (toggle rispettato) | PASS |
| J5.S4 | cons-pd1 riabilita notifiche → 200 | PASS |
| J5.S5 | Assegnazione successiva produce email a cons-pd1 | PASS |

**Esito: PASS** — 5/5 step OK.

---

## Journey 6: Audit Log

| Step | Descrizione | Esito |
|------|-------------|-------|
| J6 | GET /api/admin/audit-log → 404 (Phase 8.A non implementata) | SKIP |

**Esito: SKIP** — `/api/admin/audit-log` restituisce 404. L'`AdminController` non ha questo endpoint. Il codice `AuditLogger.cs` esiste nell'Infrastructure layer ma la build fallisce per riferimento mancante (BUG-001), e il controller REST non è stato ancora aggiunto.

---

## Riepilogo Journey

| Journey | Descrizione | Esito | Note |
|---------|-------------|-------|------|
| J1 | Onboarding via invito | PASS | DTO richiede nome+cognome |
| J2 | Ciclo seduta + ODG + assegnazioni + report PDF | PASS | API usa nomi italiani |
| J3 | Atto + notifica capogruppo | PASS | Status update via PUT full DTO |
| J4 | Permission cross-role + IDOR | PASS | |
| J5 | Opt-out notifiche | PASS | |
| J6 | Audit log | SKIP | Phase 8.A pending |

---

## Bug per Severità

### BUG-001 — Bloccante: Build `dotnet build` fallisce su `TeLoConsiglio.Infrastructure`

**Severità:** Bloccante  
**Personas:** N/A (build-time)  
**Passi per riprodurre:**
```bash
dotnet build backend/TeLoConsiglio.sln
```
**Atteso:** Build verde  
**Osservato:**
```
AuditLogger.cs(2,28): error CS0234: The type or namespace name 'Http' does not exist 
in the namespace 'Microsoft.AspNetCore'
AuditLogger.cs(13,22): error CS0246: The type or namespace name 'IHttpContextAccessor' 
could not be found
```
**Causa:** `TeLoConsiglio.Infrastructure.csproj` manca del riferimento a `Microsoft.AspNetCore.Http.Abstractions`. Il progetto usa `Sdk="Microsoft.NET.Sdk"` (non Web SDK) e quindi non include automaticamente le astrazioni ASP.NET Core.  
**File coinvolto:** `backend/TeLoConsiglio.Infrastructure/TeLoConsiglio.Infrastructure.csproj` — manca `<PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" Version="2.2.0" />`

---

### BUG-002 — Maggiore: Journey 6 `/api/admin/audit-log` assente — AdminController non implementato

**Severità:** Maggiore  
**Personas:** admin@teloconsiglio.io  
**Passi per riprodurre:**
```
GET /api/admin/audit-log
Authorization: Bearer <admin-token>
```
**Atteso:** 200 lista azioni audit  
**Osservato:** 404  
**Causa:** `AdminController` non ha l'endpoint `/audit-log`. `AuditLogger.cs` e `IAuditLogger.cs` esistono ma non sono registrati nel DI container e non c'è il controller REST.  
**File coinvolto:** `backend/TeLoConsiglio.Api/Controllers/AdminController.cs` (endpoint mancante), `backend/TeLoConsiglio.Infrastructure/Services/AuditLogger.cs`, `backend/TeLoConsiglio.Infrastructure/Services/IAuditLogger.cs`

---

### BUG-003 — Maggiore: AuditLogs table assente nel database

**Severità:** Maggiore  
**Causa:** `AppDbContext` dichiara `DbSet<AuditLog> AuditLogs` ma la migrazione EF corrispondente non è stata applicata al database. `AuditLog.cs` esiste in `TeLoConsiglio.Domain` ma la tabella `AuditLogs` è assente in PostgreSQL.  
**File coinvolto:** `backend/TeLoConsiglio.Infrastructure/Migrations/` (migrazione mancante), `backend/TeLoConsiglio.Infrastructure/Data/AppDbContext.cs`

---

### BUG-004 — Minore: Spec e2e-tester.md documenta endpoint inesistenti

**Severità:** Minore (documentazione)  
**Descrizione:** La spec `e2e-tester.md` menziona endpoint separati che non esistono nell'API:
- `POST /api/sittings/agenda/{itemId}/assignments` → non esiste; si usa `PUT /api/sittings/agenda/{itemId}` con `assignedUserIds`
- `PUT /api/sittings/agenda/{itemId}/decision` → non esiste; si usa `PUT /api/sittings/agenda/{itemId}` con `decisione`
- `PUT /api/acts/{id}/status` → non esiste; si usa `PUT /api/acts/{id}` con `status`
- I campi DTO per sittings usano nomi italiani (`titolo`, `luogo`, `data`, `descrizione`, `ordine`) non inglesi (`title`, `location`, `scheduledAt`, `description`, `order`)
**File coinvolto:** `.claude/agents/e2e-tester.md`

---

### BUG-005 — Minore: Notifica atto depositato inviata a tutti i Capogruppo del gruppo (inclusi legacy)

**Severità:** Minore (comportamento atteso per design, potenziale rumore)  
**Descrizione:** Quando `cons-pd1` deposita un atto, la notifica viene inviata a TUTTI gli utenti con ruolo `Capogruppo` nel gruppo `Gruppo PD`. Il database ha utenti legacy da run precedenti (`userc@test.it` con ruolo Capogruppo in Gruppo PD) che ricevono anche loro la notifica. Questo è corretto per il design ma potrebbe essere inaspettato se un Capogruppo lascia il gruppo senza essere rimosso.  
**File coinvolto:** `backend/TeLoConsiglio.Infrastructure/Services/EmailNotificationService.cs` (linea 174-180)

---

## Coverage Non Raggiunta

| Feature | Motivo Skip |
|---------|-------------|
| ai-draft atti (POST /api/acts/{id}/ai-draft) | GEMINI_API_KEY vuota → 503 atteso |
| suggest-legal-refs AI | GEMINI_API_KEY vuota → 503 atteso |
| import-pdf seduta (AI) | GEMINI_API_KEY vuota → 503 atteso |
| Journey 6: Audit log completo | Phase 8.A non implementata (endpoint 404) |
| Refresh token rotation + replay | Non coperto in questo round |
| Upload file .exe / >20MB / path traversal | Non coperto in questo round |
| Frontend build (`npm run build`) | Non avviato frontend in questo round |

