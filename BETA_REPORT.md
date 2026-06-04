# BETA TEST REPORT — TeLoConsiglio.io

- **Branch**: `claude/relaxed-hawking-pO5Tc`
- **Commit testato**: `092a9f3` (HEAD del branch al momento del test: `04eaaaa` — solo REVIEW.md aggiunto sopra il commit richiesto, fuori dal mio scope)
- **Data esecuzione**: 2026-06-04
- **Ambiente**: backend ASP.NET Core 8 in `dotnet run` su `localhost:5000`, Postgres 16 locale, Vite dev server su `localhost:5173`. Docker daemon non disponibile.
- **Variabili env**: JWT key random generata, `ANTHROPIC_API_KEY` lasciata vuota come richiesto.

## Esiti per scenario

| Scenario | Esito | Note |
|---|---|---|
| **A.** Registrazione + login | **PASS** | Register 200, login 200, /me 200, login errato 401, admin seed login 200. |
| **B.** Profilo / linea politica | **PASS** | PUT/GET /profile/political 200; upload programma `.txt` 200, testo estratto correttamente. |
| **C.** Documento + riassunto | **PASS** | Upload documento 200; summarize ritorna 503 con messaggio chiaro (atteso). |
| **D.** Atti | **PASS (con limitazioni)** | Create/Get/List/Update OK. AI endpoints 503 (attesi). Confirm/Insert legal-refs non testabili end-to-end senza AI (richiedono ReferenceIds creati dal flusso suggest che è AI-gated). |
| **E.** Sedute | **PASS dopo correzione enum** | Vedi Bug #1: il task specificava `decisione:"Pending"` ma l'enum reale è `DaDecidere/Approvare/Respingere/Astenersi`. Con enum corretto 200. |
| **F.** Sicurezza | **PARZIALE** | F1 (401 senza token) OK; F2/F3 (cross-user su acts/documents) OK 404. **F5/F6 FAIL**: utenti diversi possono leggere E CANCELLARE risorse altrui per Sittings (Bug #2, #3). Bug #4: enumerazione completa utenti via `/api/users`. |
| **G.** Frontend | **PASS** | Vite dev su :5173 HTTP 200; HTML servito; `npm run build` completato (dist 404 KB). Verificato che `/src/main.tsx` viene servito dal dev server. |

## Bug Riscontrati

### Bug #1 — Enum `AgendaDecision` con valori non ovvi e nessuna lista in API docs
- **Severità**: Minore (UX/API discoverability)
- **Scenario**: E2
- **Comando**: `POST /api/sittings/{id}/agenda` con `"decisione":"Pending"`
- **Atteso**: o accettato come default, o messaggio enumerando i valori validi
- **Effettivo**: HTTP 400, body (troncato 500):
  ```json
  {"errors":{"$.decisione":["The JSON value could not be converted to TeLoConsiglio.Api.Dtos.AgendaItemCreateDto. Path: $.decisione | LineNumber: 0 | BytePositionInLine: 82."]}}
  ```
- **Causa ipotesi**: Identica per `ActStatus` (`Bozza/Depositato/Approvato/Respinto`) e `ActType`. Swagger schema generato con `JsonStringEnumConverter` mostra solo i nomi nello schema ma il messaggio d'errore è opaco.
- **File**: `backend/TeLoConsiglio.Domain/Entities/AgendaItem.cs`, `backend/TeLoConsiglio.Domain/Entities/Act.cs`

### Bug #2 — Sittings visibili a qualsiasi utente autenticato
- **Severità**: Maggiore (privacy/data isolation)
- **Scenario**: F5
- **Comando**: `GET /api/sittings/{id}` con token di `beta2@test.io` su seduta creata da `beta1@test.io`
- **Atteso**: 403/404 (le sedute appartengono al consigliere che le crea, oppure dovrebbe esistere un concetto di "comune" condiviso)
- **Effettivo**: HTTP 200, dettaglio completo della seduta + ODG + assegnatari (con email/nome del consigliere)
- **Body troncato 500**: `{"id":"c0f1433f-...","data":"2026-07-15T19:00:00Z","luogo":"Sala Consiglio","titolo":"Seduta ordinaria luglio","agendaItems":[{"id":"e1e472d8-...","ordine":1,"descrizione":"Discussione mozione verde urbano","decisione":"Approvare","motivazione":"Maggioranza favorevole","actId":null,"assignedUsers":[{"userId":"9debafb4-...","email":"beta1@test.io","fullName":"Beta One"}]}]}`
- **Causa ipotesi**: `SittingsController.Get`/`List` non filtra per `CreatedById` né per ruolo. Se il design prevede sedute condivise per comune, manca comunque un filtro per `Comune`.
- **File**: `backend/TeLoConsiglio.Api/Controllers/SittingsController.cs:26-43`

### Bug #3 — Qualsiasi utente può modificare/cancellare seduta o agenda item altrui
- **Severità**: **Bloccante** (integrità dati)
- **Scenario**: F6
- **Comando**: `DELETE /api/sittings/agenda/{itemId}` con token utente B sull'item creato da utente A
- **Atteso**: 403/404
- **Effettivo**: HTTP 204 — item realmente eliminato (verificato con GET seduta successivo: `"agendaItems":[]`)
- **Causa ipotesi**: `DeleteAgendaItem`, `UpdateAgendaItem`, `Delete` (sitting) non controllano ownership. Stesso problema per `AddAgendaItem` e `Create` sitting.
- **File**: `backend/TeLoConsiglio.Api/Controllers/SittingsController.cs:60-129`

### Bug #4 — `GET /api/users` espone l'elenco completo utenti a qualunque consigliere
- **Severità**: Maggiore (privacy / enumeration attack)
- **Scenario**: extra F7
- **Comando**: `GET /api/users` con token consigliere normale
- **Atteso**: 403 (solo Admin) o output filtrato
- **Effettivo**: HTTP 200, dump di tutti gli utenti (email, FullName, Comune, Partito, Roles incluso `Admin`). Body troncato 500:
  ```json
  [{"id":"d60ef9f7-...","email":"admin@teloconsiglio.io","fullName":"Administrator","comune":null,"partito":null,"roles":["Admin"]},{"id":"9debafb4-...","email":"beta1@test.io",...,"roles":["Consigliere"]},{"id":"f098ad12-...","email":"beta2@test.io",...,"roles":["Consigliere"]}]
  ```
- **Causa ipotesi**: `UsersController` ha `[Authorize]` ma non `[Authorize(Roles=Admin)]`.
- **File**: `backend/TeLoConsiglio.Api/Controllers/UsersController.cs:10-30`

### Bug #5 — Endpoint `legal-refs/confirm` e `legal-refs/insert` non riusabili senza AI
- **Severità**: Minore (workflow gap)
- **Scenario**: D4, D5
- **Comando**: `POST /api/acts/{id}/legal-refs/confirm` con ReferenceIds finti
- **Atteso**: dal task — possibilità di confermare riferimenti "finti" forniti dall'utente
- **Effettivo**: 400 `"Nessun riferimento selezionato"` perché i refs devono esistere già in tabella `LegalReferences` (popolata SOLO dal flusso `suggest` che è AI-gated). Non c'è un endpoint per creare manualmente un `LegalReference`.
- **Causa ipotesi**: il design prevede sempre AI come fonte; manca CRUD manuale.
- **File**: `backend/TeLoConsiglio.Api/Controllers/ActsController.cs:247-277`

### Bug #6 — Messaggio d'errore validazione dto fuorviante (`The dto field is required.`)
- **Severità**: Minore (DX)
- **Scenario**: E2 (originale), Extra5, Extra6
- **Comando**: qualunque POST/PUT con un campo enum invalido o body con tipo errato
- **Atteso**: errore di validazione localizzato al campo
- **Effettivo**: oltre all'errore puntuale viene aggiunto un misleading `"dto":["The dto field is required."]` come se il body intero mancasse.
- **Causa ipotesi**: i `record` con `[Required]` su proprietà primitive (es. `Required] ActStatus Status`) fanno fallire la model-binding-as-required validation quando una proprietà del record non si binda; ASP.NET segnala l'intero parametro `dto` come "required" anche se in realtà il body è presente.
- **File**: tutti i `*Dtos.cs`

### Bug #7 — `launchSettings.json` forza la porta 5292, ignorando `ASPNETCORE_URLS`
- **Severità**: Minore (DevX / docs)
- **Scenario**: setup
- **Comando**: `dotnet run` con `ASPNETCORE_URLS=http://localhost:5000`
- **Atteso**: porta 5000 (come da README e da `docker-compose`)
- **Effettivo**: il backend si avvia su `http://localhost:5292` (definita in `Properties/launchSettings.json` profilo `http`)
- **Workaround**: `dotnet run --urls=http://localhost:5000`
- **File**: `backend/TeLoConsiglio.Api/Properties/launchSettings.json`

### Bug #8 — Warning EF Core `QuerySplittingBehavior` ricorrente
- **Severità**: Minore (performance)
- **Scenario**: chiunque richieda `GET /api/acts/{id}` o `GET /api/sittings/{id}`
- **Atteso**: query splittata o splash silenziato
- **Effettivo**: log warning `Compiling a query which loads related collections for more than one collection navigation...` ad ogni richiesta con multi-Include
- **File**: `backend/TeLoConsiglio.Api/Program.cs` (configurazione EF) e i due controller con Include multipli

## Setup notes

1. **Docker non disponibile**: daemon non in esecuzione. Sono passato al path manuale.
2. **Postgres**: cluster Debian preinstallato in stato `down` con stale pidfile; risolto con `pg_ctlcluster 16 main start` e `ALTER USER postgres PASSWORD 'postgres'`. DB `teloconsiglio` già esistente da precedente esecuzione, ho riusato.
3. **Backend**: build con `dotnet build` OK; il primo `dotnet run` ha aperto la porta 5292 invece di 5000 (vedi Bug #7); risolto con `--urls=http://localhost:5000`.
4. **Migrazioni e seed**: eseguiti correttamente al primo avvio; admin di default creato come da log.
5. **Frontend**: `npm run build` OK in 838 ms, bundle 404 KB. `npm run dev` su 5173 OK.
6. **JWT key**: generata random con `openssl rand -hex 32` come richiesto.

## Coverage

Cosa NON sono riuscito a testare:
- **AI flows completi** (`/api/documents/{id}/summarize`, `/api/acts/{id}/ai-draft`, `/api/acts/{id}/legal-refs/suggest`): manca `ANTHROPIC_API_KEY` per design del beta. Verificato il fallback 503.
- **OAuth Google/Microsoft**: non configurato (atteso).
- **Refresh con token revocato per scadenza temporale**: testata solo la revoca per uso singolo.
- **Render UI del frontend**: non c'è un browser headless disponibile in sessione; verificato solo che Vite dev server risponde con HTML e `main.tsx` viene compilato. Il production build passa la pipeline `tsc -b && vite build` senza errori.
- **Upload PDF/DOCX reali**: testato solo `.txt` (l'estensione di `IDocumentTextExtractor` per altri formati non è stata verificata).
- **Endpoint `legal-refs/confirm` con riferimenti reali**: non disponibili senza AI (vedi Bug #5).
- **Email confirmation flow**: non presente nel codice.
- **Concurrency / rate limit**: non valutati.

## Riepilogo numerico
- Scenari eseguiti: 7 (A–G) + 8 controlli aggiuntivi
- PASS: A, B, C, D (parziale per AI), E (dopo enum), G
- FAIL/PARZIALE: F (sicurezza Sittings + Users), D (legal-refs flusso completo non testabile)
- Bug totali: 8 (1 Bloccante, 3 Maggiori, 4 Minori)
