# BETA REPORT R5 — Phase 3 (Dashboard, Filtri Sedute, Report PDF, Riproposta Documenti)

**Data:** 2026-06-13
**Branch:** `claude/relaxed-hawking-pO5Tc`
**HEAD:** `c11c355`
**Ambiente:** Postgres locale, backend Development (dotnet run)

---

## Tabella esiti scenari

| Scenario | Descrizione | Esito | Note |
|---|---|---|---|
| A1 | Dashboard: struttura completa | PASS | `recentDocuments`, `nextSitting`, `documentsToAnalyze`, `counters` presenti |
| A2 | Dashboard: recentDocuments max 5 | PASS | Dopo 6 upload ne mostra 5 |
| A3 | Dashboard: actsBozza counter (BE) | PASS | Incrementa correttamente |
| A4 | Dashboard: nextSitting non-null dopo seduta futura (BE) | PASS | Filtro per Comune funziona |
| A5 | Dashboard: documentsToAnalyze con item DaAnalizzare | PASS | Mostra agenda items correttamente |
| A6 | Dashboard: User B (Roma) ≠ User A (Milano) isolamento | PASS | Logica corretta; residui di dati da round precedenti non impattano la logica |
| A-FE | Dashboard: contatori nel frontend mostrano valori corretti | **FAIL** | BUG #1: mismatch nomi campo counter FE↔BE |
| A-FE2 | Dashboard: descrizione item in documentsToAnalyze | **FAIL** | BUG #2: mismatch `agendaItemDescrizione` vs `descrizione` |
| B1 | Filtri sedute: period=Past | PASS | Restituisce solo passate |
| B2 | Filtri sedute: period=Upcoming (via FE 'future') | **FAIL** | BUG #3: FE invia `future`, BE aspetta `Upcoming` → HTTP 400 |
| B3 | Filtri sedute: q full-text su titolo/luogo | PASS | |
| B4 | Filtri sedute: range from/to | PASS | |
| B5 | Filtri sedute: paginazione + X-Total-Count | PASS | |
| C1 | Report PDF: 200, Content-Type application/pdf | PASS | |
| C2 | Report PDF: Content-Disposition con filename | PASS | `attachment; filename=report-seduta-{id}-{yyyyMMdd}.pdf` |
| C3 | Report PDF: file valido (`%PDF-1.7`) | PASS | |
| C4 | Report PDF: IDOR User B → 404 | PASS | |
| C5 | Report PDF: seduta senza ApprovataPerSeduta → 200 con messaggio | PASS | Testo "Nessuna voce approvata per la seduta." |
| D1 | Riproposta: document-suggestions score >= 50 | PASS | Score 50 per substring match |
| D2 | Riproposta: clone-document → documentId valorizzato | PASS | |
| D3 | Riproposta: exact match → score 100 | PASS | |
| D4 | Riproposta: IDOR User B → 404 | PASS | |
| D5 | Riproposta: clone-document IDOR (sourceItem di altro utente) | PASS | 404 correttamente |
| E1 | Admin seed login | PASS | password `Admin!2026` |
| E2 | /api/users espone solo id+displayName | PASS | |
| E3 | 401 senza token su nuovi endpoint | PASS | dashboard, report.pdf, document-suggestions, clone-document |
| E4 | Party manifests GET | PASS | |
| E5 | Profile political update + reset-linea | PASS | |
| E6 | Allegati atti | PASS | |
| E7 | `npm run build` | PASS | 0 errori, 0 warning |
| E8 | `dotnet build` | PASS | 0 errori, 0 warning |

---

## Bug trovati

### BUG #1 — Bloccante
**Dashboard: tutti i contatori mostrano 0 nel frontend**

- **File:** `frontend/src/api/types.ts` (interfaccia `DashboardCounters`), `frontend/src/pages/Dashboard.tsx`
- **Riproduzione:** Login → naviga in `/` → le card "Atti in bozza" e "Sedute future" mostrano 0 anche se esistono dati.
- **Causa:** Il backend restituisce `{ actsBozza, upcomingSittings, pendingInvitations }` ma il frontend accede a `{ attiBozza, seduteFuture, invitatiPending }` → tutti i valori sono `undefined` (reso come 0 o non visualizzato).
- **Verifica BE:** `GET /api/dashboard` → `"counters": { "actsBozza": 2, "upcomingSittings": 8, "pendingInvitations": 0 }`
- **Impatto:** L'intera sezione Statistiche della dashboard mostra dati errati.

### BUG #2 — Bloccante
**Sedute: filtro "Future" restituisce HTTP 400**

- **File:** `frontend/src/api/types.ts` (`SittingsQueryParams.period`), `frontend/src/pages/Sedute.tsx`
- **Riproduzione:** Login → Sedute → tab "Future" → nessuna seduta mostrata (la richiesta fallisce con 400).
- **Causa:** Il frontend invia `period=future` ma il backend accetta solo l'enum `SittingPeriod { All, Past, Upcoming }`. Il valore `future` non corrisponde a nessun membro dell'enum → 400 Bad Request.
- **Verifica:** `curl "http://localhost:5000/api/sittings?period=future"` → `HTTP 400`; `curl "http://localhost:5000/api/sittings?period=Upcoming"` → `HTTP 200`.
- **Impatto:** Il tab "Future" è completamente non funzionante.

### BUG #3 — Maggiore
**Dashboard: descrizione e nome documento negli "ODG da analizzare" sempre vuoti**

- **File:** `frontend/src/api/types.ts` (interfaccia `DocToAnalyze`), `frontend/src/pages/Dashboard.tsx` (riga 166, 168)
- **Riproduzione:** Login → Dashboard → sezione "ODG da analizzare" → le voci mostrano solo il titolo seduta, descrizione e nome documento sono vuoti.
- **Causa:** Il backend restituisce `{ ..., descrizione, documentId }` ma il frontend accede a `item.agendaItemDescrizione` e `item.documentName`, entrambi assenti nella risposta BE.
- **Dettaglio:** La risposta BE per `documentsToAnalyze` ha campi: `sittingId, sittingData, sittingTitolo, agendaItemId, descrizione, documentId`. Il tipo FE `DocToAnalyze` ha: `sittingId, sittingTitolo, agendaItemId, agendaItemDescrizione, documentId, documentName`.
- **Impatto:** L'utente non vede di quale voce ODG si tratta né il documento allegato.

### BUG #4 — Minore
**DocToAnalyze: campo `documentName` nel tipo FE non esiste nella risposta BE; `sittingData` non tipizzato**

- **File:** `frontend/src/api/types.ts` (interfaccia `DocToAnalyze`)
- **Dettaglio:** Il tipo FE include `documentName` che il BE non restituisce nel DTO `DashboardAgendaItemDto`. Viceversa, `sittingData` è presente nella risposta BE ma mancante nel tipo FE (non causa errore runtime ma è un rischio).
- **Impatto:** Minore (conseguenza del BUG #3).

### BUG #5 — Minore (test data)
**Dashboard: scenario "User B → nextSitting null" non riproducibile su database condiviso**

- **File:** N/A (comportamento atteso corretto)
- **Dettaglio:** User B con `comune=Roma` vede la seduta `6fb1d87c` creata da un utente con `Comune=Roma` nei round precedenti. La logica del backend è corretta (mostra sedute del proprio comune), ma l'assenza di reset del DB tra i round impedisce di validare lo scenario "nextSitting null" ex novo.
- **Impatto:** Test isolation issue, non un bug applicativo.

---

## Riepilogo per severità

| # | Severità | Titolo breve | File FE |
|---|---|---|---|
| 1 | Bloccante | Contatori dashboard tutti a 0 | `types.ts:DashboardCounters` |
| 2 | Bloccante | Filtro sedute future → HTTP 400 | `types.ts:SittingsQueryParams.period` |
| 3 | Maggiore | ODG da analizzare: descrizione/documento vuoti | `types.ts:DocToAnalyze` |
| 4 | Minore | `documentName` / `sittingData` inconsistenza tipo | `types.ts:DocToAnalyze` |
| 5 | Minore | Test isolation: nextSitting null non verificabile | N/A |

---

## Funzionalità backend verificate OK

- `GET /api/dashboard` struttura completa, filtri Comune, max 5 recentDocuments
- `GET /api/sittings?period=Past|All|Upcoming&q=&from=&to=&page=&pageSize=` + header `X-Total-Count`
- `GET /api/sittings/{id}/report.pdf` → PDF valido, Content-Disposition, IDOR 404
- `GET /api/sittings/{id}/agenda/{itemId}/document-suggestions` → scoring 50/100, IDOR 404
- `POST /api/sittings/agenda/{itemId}/clone-document` → DocumentId valorizzato, IDOR 404
- Endpoint AI senza GEMINI_API_KEY → 503 (atteso)
- Tutti i build (FE + BE) verdi

