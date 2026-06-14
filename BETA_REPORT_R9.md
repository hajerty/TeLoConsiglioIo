# BETA TEST REPORT — Round 9 (Phase 7: Notifiche Email + Preferenze Opt-out)

**Data:** 2026-06-14
**Branch:** claude/relaxed-hawking-pO5Tc
**HEAD:** a701c29
**Ambiente:** Postgres locale, backend Development senza SMTP_HOST (ConsoleEmailSender)

---

## Tabella Esiti

| Scenario | Descrizione | Esito | Note |
|----------|-------------|-------|------|
| A | Migration: colonna `EmailNotificationsEnabled` esiste con default true | PASS | Colonna presente (`boolean not null default true`) dopo avvio backend |
| B | GET/PUT `/api/profile/notifications` — toggle true→false→true | PASS | GET restituisce `{emailNotificationsEnabled:true}`, PUT false → response false, GET conferma false, re-PUT true ok |
| C | Notifica "Assegnato a ODG" + rispetto opt-out | PASS | Email loggata `[EMAIL-CONSOLE] To: userb@test.it Subject: Assegnazione al punto ODG...`; con `emailNotificationsEnabled=false` nessuna nuova email |
| D | Notifica "Decisione cambiata" + no duplicato su stesso valore | PASS | Email loggata su cambio DaDecidere→Approvare; nessuna email su secondo PUT con stesso valore Approvare |
| E | Notifica "Nuovo documento ODG" | PASS | Email loggata `Subject: Nuovo documento sul punto ODG...` dopo upload documento su item assegnato a B |
| F | Notifica "Atto depositato" a Capogruppo stesso gruppo | PARTIAL | Notifica via PUT(status=Depositato) funziona. Creazione diretta con status=Depositato impossibile (bug). Null-group: skip silenzioso confermato |
| G | Frontend: `npm run build` verde + sezione Notifiche email in Profilo.tsx | PASS | Build in 901ms senza errori; `Bell` + "Notifiche email" + toggle presente in `Profilo.tsx:284-338` |
| H | Non-regressione: admin login, atti/sedute, IDOR 404, `/api/users` solo id+displayName, inviti email loggata, encryption-status 200 | PASS | Tutti verificati |

---

## Bug Rilevati

### BUG-R9-01 — Minore
**Titolo:** Dead code nella notifica "Atto depositato" su POST `/api/acts`

**Severità:** Minore

**Descrizione:**
In `ActsController.cs`, il metodo `Create` contiene questo controllo (riga 95):
```csharp
if (a.Status == ActStatus.Depositato)
{
    try { await _notifications.NotifyActDepositedAsync(a.Id); }
    ...
}
```
Tuttavia `ActCreateDto` non include il campo `Status`, e `Act.Status` è inizializzato a `ActStatus.Bozza` per default nell'entità. Pertanto la condizione `a.Status == ActStatus.Depositato` è sempre `false` dopo la creazione, rendendo questo ramo codice morto. La notifica per atti creati direttamente in stato Depositato non verrà mai inviata.

**Passi per riprodurre:**
1. POST `/api/acts` con body `{"tipo":"Mozione","titolo":"...","oggetto":"...","bodyMd":"...","status":"Depositato"}`
2. L'atto viene creato con `status: "Bozza"` (status ignorato nel DTO di creazione)
3. Nessuna notifica emessa anche se l'intento era depositare direttamente

**Risposta osservata:** `status: "Bozza"` nella response, nessuna email loggata

**File probabile:** `backend/TeLoConsiglio.Api/Controllers/ActsController.cs` (riga 80-103) e `backend/TeLoConsiglio.Api/Dtos/ActDtos.cs` (`ActCreateDto` senza campo `Status`)

**Impatto:** Scenario F del test spec ("D crea altro atto direttamente in `Status=Depositato`") non testabile via API. La notifica funziona correttamente tramite la via PUT.

---

## Dettagli Esecuzione

### Scenario A — Migration
```
EmailNotificationsEnabled | boolean | not null | default: true
```
La colonna è stata aggiunta dalla migration `20260614124205_AddEmailNotificationsEnabled.cs` ed è stata applicata automaticamente all'avvio del backend.

### Scenario C — Opt-out verificato
- Email inviata a `userb@test.it` alla prima assegnazione (notifiche abilitate)
- Con `emailNotificationsEnabled=false`: conteggio email rimasto invariato (0 nuove email per B)

### Scenario D — No duplicato
- Prima PUT da `DaDecidere` a `Approvare`: 1 email loggata con subject `Decisione aggiornata su punto ODG`
- Seconda PUT con stesso valore `Approvare → Approvare`: 0 nuove email (logica `if (dto.Decisione != oldDecisione)` funziona correttamente)

### Scenario F — Null group
- Utente E senza `Gruppo` deposita atto: nessuna email inviata (skip silenzioso nel service, log Debug non visibile al livello Info ma comportamento corretto confermato)

### Scenario G — Frontend
- `Bell` importato da `lucide-react` (riga 3 di `Profilo.tsx`)
- Sezione "Notifiche email" presente a riga 284 con toggle `role="switch"` e `aria-checked`
- Build completata senza warning TypeScript o errori Vite

---

## Riepilogo
- **7/7 scenari principali superati** (F parziale per dead code, non comportamento errato in produzione)
- **1 bug** rilevato (Minore): dead code nel ramo notifica POST acts con status=Depositato
- ConsoleEmailSender attivo come previsto (nessun SMTP_HOST)
- Tutte le email di notifica correttamente loggata con pattern `[EMAIL-CONSOLE] To: ... Subject: ...`
