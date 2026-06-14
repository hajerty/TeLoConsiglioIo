# E2E Report R2 — Journey 6 Audit Log Follow-Up

**Data:** 2026-06-14
**Branch:** `claude/relaxed-hawking-pO5Tc`
**HEAD:** `4b2ee9c`
**Scope:** Journey 6 soltanto — follow-up di E2E_REPORT_R1.md dove J6 era SKIP (Phase 8.A non implementata).
**Backend:** Build da sorgente (`dotnet build` verde — BUG-001 risolto, `FrameworkReference Include="Microsoft.AspNetCore.App"` aggiunto nel .csproj).
**DB:** `teloconsiglio` su PostgreSQL 16 locale; migrazione `20260614144104_AddAuditLogs` applicata automaticamente allo startup.

---

## Contesto: cosa e' cambiato da R1

- `AuditLogger.cs` compila ora correttamente (BUG-001 chiuso).
- Migration `AddAuditLogs` applicata al DB su startup (`db.Database.Migrate()`).
- `AuditLogController.cs` aggiunto in `TeLoConsiglio.Api/Controllers/`.
- `IAuditLogger` registrato nel DI container (`AddScoped<IAuditLogger, AuditLogger>()`).
- Tutti i controller principali (Auth, Acts, Sittings, Invitations, Documents, Profile, Admin) loggano le azioni rilevanti.

---

## Journey 6 — Audit Log Admin

### Setup attivita' di audit (Step 2)

Azioni generate come admin (`admin@teloconsiglio.io`) prima dei test:

| Azione | Esito |
|--------|-------|
| 2x login success | PASS |
| 1x login failure (password errata) | PASS |
| 1x `sitting.create` (seduta futura, Milano) | PASS |
| 1x `agenda.add` (punto ODG) | PASS |
| 1x `agenda.update` (decisione=Approvare) | PASS |
| 1x `act.create` (Mozione in Bozza) | PASS |
| 1x `act.update` (Bozza→Depositato) | PASS |
| 1x `act.delete` | PASS |
| 1x `invitation.create` | PASS |
| 1x `invitation.revoke` | PASS |
| 1x `agenda.document.upload` (file .txt su item ODG) | PASS |

Totale record audit al termine dei test: **67** (incluso overhead di admin.audit-log.view dai test stessi).

---

### Test Steps

| Step | Descrizione | Atteso | Osservato | Esito |
|------|-------------|--------|-----------|-------|
| J6.T1 | GET /api/admin/audit-log (page=1, pageSize=20) | 200, items[].length == 20, X-Total-Count > 0 | 200, 20 items, X-Total-Count=62 (momento del test) | PASS |
| J6.T2 | GET ?action=auth.login.success | 200, solo login success | 200, 17 items, tutti action=auth.login.success | PASS |
| J6.T3 | GET ?action=auth.login.failure | 200, solo login fail | 200, 1 item, action=auth.login.failure | PASS |
| J6.T4 | GET ?userId=\<adminId\> | 200, solo azioni admin con userId popolato | 200, 24 items — ma auth.login.success assente (userId=null su tali record — vedi BUG-J6-002) | PASS/WARN |
| J6.T5 | GET ?q=Act (maiuscola) | almeno 1 risultato con act.* | 200, 3 items: act.create, act.delete, act.update | PASS |
| J6.T5b | GET ?q=act (minuscola) | stesso risultato (case-insensitive) | 200, 3 items identici | PASS |
| J6.T6 | GET ?from=2026-06-13&to=2026-06-15 | entries odierne presenti | 200, 34 items (tutte create oggi) | PASS |
| J6.T7 | Paginazione page=1 vs page=2 con pageSize=5 | 5 item distinti per pagina, nessun overlap | **OVERLAP: 1 item duplicato (`a3b2fa81`) tra p1 e p2** (vedi BUG-J6-001) | FAIL |
| J6.T8 | GET /api/admin/audit-log come Consigliere | 403 | 403 | PASS |
| J6.T9 | GET /api/admin/audit-log/export.csv come Consigliere | 403 | 403 | PASS |
| J6.T10 | GET /api/admin/audit-log/export.csv come Admin | 200, Content-Type: text/csv | 200, text/csv, Content-Disposition: `attachment; filename=audit-log-20260614.csv` | PASS |
| J6.T11 | Prima riga CSV = header corretto | `id,userId,userEmail,action,resource,details,ipAddress,userAgent,createdAt` | Identico | PASS |
| J6.T12 | Escape RFC 4180: JSON in `details` viene quotato | Campo con virgole racchiuso in `"..."`, virgolette interne raddoppiate | Verificato: `"{""comune"":""Milano"",""partito"":""PD"",...}"` — corretto | PASS |
| J6.T12b | CSV parsabile con modulo `csv` standard | 0 errori di parsing | Data rows=47, errori=0 | PASS |
| J6.T13 | Tutte le azioni richieste presenti nel log | act.create, act.update, act.delete, sitting.create, agenda.add, agenda.update, agenda.document.upload, invitation.create, invitation.revoke, auth.login.success, auth.login.failure | Tutte presenti | PASS |

**Esito Journey 6: 13/14 PASS, 1 FAIL (BUG-J6-001), 1 WARN (BUG-J6-002)**

---

## Bug trovati in Journey 6

### BUG-J6-001 — Maggiore: Paginazione non stabile — overlap tra pagine consecutive

**Severita:** Maggiore
**Endpoint:** `GET /api/admin/audit-log?page=N&pageSize=M`
**Passi per riprodurre:**
```
GET /api/admin/audit-log?page=1&pageSize=5
GET /api/admin/audit-log?page=2&pageSize=5
```
**Atteso:** I 5 item di pagina 2 sono distinti dai 5 di pagina 1 (nessun overlap).
**Osservato:** L'item `a3b2fa81` appare sia come ultimo di pagina 1 sia come primo di pagina 2.
**Causa:** `OrderByDescending(a => a.CreatedAt)` senza chiave di ordinamento secondaria stabile. Quando due o piu' record hanno `CreatedAt` uguale o con granularita' inferiore alla risoluzione del database (PostgreSQL archivia `timestamp with time zone` con microsecondi, ma la generazione lato .NET puo' produrre valori identici se due chiamate avvengono nello stesso tick), il risultato del `Skip`/`Take` e' non-deterministico e produce duplicati sui bordi di pagina.
**Fix suggerito:** Aggiungere `.ThenByDescending(a => a.Id)` come chiave di pareggio stabile, o `.ThenBy(a => a.Id)` — qualunque sia consistente tra chiamate.
**File coinvolto:** `backend/TeLoConsiglio.Api/Controllers/AuditLogController.cs` righe 78-82 e 122-125 (stessa query in `ExportCsv`).

---

### BUG-J6-002 — Minore: `auth.login.success` e `auth.login.failure` hanno `userId`/`userEmail` null

**Severita:** Minore (impatto sul filtraggio per `userId`)
**Endpoint:** `GET /api/admin/audit-log?action=auth.login.success`
**Atteso:** Record di login success con `userId` e `userEmail` popolati.
**Osservato:** Tutti i record `auth.login.success` e `auth.login.failure` hanno `userId=null`, `userEmail=null`. L'Id utente e' presente solo nel campo `resource` (es. `User:0984de2c-...`).
**Conseguenza:** Filtrando con `?userId=<adminId>` non compaiono i login dell'admin — non si puo' tracciare tutti gli accessi di un utente specifico tramite il filtro nativo.
**Causa:** Al momento del log `auth.login.success`, il JWT non e' ancora stato impostato sul contesto HTTP. Il metodo `_users.GetUserId(ctx.User)` restituisce null perche' `ctx.User` e' l'identita' anonima della request in entrata (non quella post-autenticazione).
**Fix suggerito:** Nel `LoginAsync` in `AuthController`, passare esplicitamente `userId` e `userEmail` come parametri a `LogAsync`, non affidarsi a `IHttpContextAccessor` per estrarre l'identita'. Estendere `IAuditLogger.LogAsync` con parametri opzionali `string? explicitUserId = null, string? explicitUserEmail = null`.
**File coinvolto:** `backend/TeLoConsiglio.Api/Controllers/AuthController.cs` (chiamata a `_audit.LogAsync("auth.login.success", ...)`), `backend/TeLoConsiglio.Infrastructure/Services/AuditLogger.cs`.

---

## Nota: BUG-001 da R1 e' risolto

La build ora compila senza errori. Il progetto Infrastructure usa `<FrameworkReference Include="Microsoft.AspNetCore.App" />` che include implicitamente `IHttpContextAccessor` e tutto il namespace `Microsoft.AspNetCore.Http`. La tabella `AuditLogs` e' presente nel DB dopo la migration applicata allo startup.

---

## Riepilogo Journey 6

| # | Step | Esito |
|---|------|-------|
| T1 | GET audit-log no filtri, paginazione, X-Total-Count | PASS |
| T2 | Filtro ?action=auth.login.success | PASS |
| T3 | Filtro ?action=auth.login.failure | PASS |
| T4 | Filtro ?userId=adminId | PASS/WARN (login.success esclusi — BUG-J6-002) |
| T5 | Filtro ?q=Act (case-insensitive) | PASS |
| T6 | Filtro ?from=ieri&to=domani | PASS |
| T7 | Paginazione page=1 vs page=2 | FAIL (BUG-J6-001) |
| T8 | 403 per Consigliere su audit-log | PASS |
| T9 | 403 per Consigliere su export.csv | PASS |
| T10 | 200 + Content-Type text/csv + Content-Disposition audit-log-YYYYMMDD.csv | PASS |
| T11 | Header CSV corretto | PASS |
| T12 | Escape RFC 4180 corretto | PASS |
| T13 | Tutte le azioni richieste tracciate | PASS |

**13/14 PASS — 1 FAIL (paginazione non stabile) — 1 WARN (userId null su login)**
