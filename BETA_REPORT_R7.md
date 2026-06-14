# BETA REPORT Round 7 — Phase 5: Email inviti + Import PDF convocazione

**Data:** 2026-06-14  
**Branch:** claude/relaxed-hawking-pO5Tc  
**HEAD:** cd58b14  
**Env:** Postgres locale, backend Development, no SMTP_HOST, no GEMINI_API_KEY

---

## Tabella scenari

| Scenario | Esito | Note |
|---|---|---|
| A. POST /api/invitations come Admin → 200 con token/url/emailSent | PASS | `emailSent: true` |
| A. ConsoleEmailSender logga to/subject in backend.log | PASS | Stringa `[EMAIL-CONSOLE] To: test@example.com | Subject: Invito a TeLoConsiglio.io` presente |
| A. Startup warning "console fallback" in log | PASS | `Email service: console fallback. Configura SMTP_HOST per email reali.` |
| B. POST /api/invitations come Consigliere → 403 | PASS | HTTP 403 confermato |
| B. POST con email malformata → 400 | PASS | HTTP 400 con `{"Email":["The Email field is not a valid e-mail address."]}` |
| C. import-pdf SENZA file → 400 | PASS | HTTP 400 `file field is required` |
| C. import-pdf con file .exe come Admin (no GEMINI) → atteso 400, osservato 503 | FAIL | **BUG-R7-01** — vedi dettaglio |
| C. import-pdf come Consigliere con .txt → 403 | PASS | HTTP 403 confermato |
| C. import-pdf come Admin con .txt senza GEMINI_API_KEY → 503 | PASS | HTTP 503 con `"GEMINI_API_KEY non configurata"` |
| C. import-pdf con GEMINI_API_KEY → SKIP | SKIP | KEY non presente in env |
| D. `npm run build` → 0 errori TS | PASS | 1874 moduli, build in 768ms |
| D. dist/ contiene sw.js e manifest.webmanifest | PASS | Entrambi presenti |
| E. Login admin seed | PASS | HTTP 200 |
| E. GET /api/auth/me | PASS | HTTP 200 |
| E. Lista atti/documenti/sedute | PASS | HTTP 200 tutti |
| E. IDOR su sedute (Consigliere → seduta Admin) | PASS | HTTP 404 |
| E. /api/users (Consigliere) → solo {id, displayName} | PASS | Nessun campo extra |
| E. GET /api/admin/encryption-status come Admin | PASS | HTTP 200 |
| E. `dotnet build` | PASS | 0 Warning, 0 Error |

---

## Bug rilevati

### BUG-R7-01 — Minore: import-pdf risponde 503 anziché 400 per estensioni non consentite quando AI non configurata

**Severità:** Minore  
**Passi per riprodurre:**
1. Avviare backend senza `GEMINI_API_KEY`
2. Login come Admin
3. `POST /api/sittings/import-pdf` con file `.exe` come `multipart/form-data`
4. Risposta attesa: HTTP 400 "Estensione non consentita"
5. Risposta osservata: HTTP 503 "GEMINI_API_KEY non configurata. Feature AI non disponibile."

**Causa:** Nel metodo `ImportPdf` di `SittingsController.cs` (riga 513), il controllo `!_ai.IsConfigured` è posizionato **prima** dei controlli sul file (riga 516: `file == null` e riga 519: `UploadValidator.Validate`). Questo significa che qualsiasi richiesta con GEMINI non configurata riceve 503, indipendentemente dalla validità del file caricato.

**Impatto:** Un utente malintenzionato ottiene informazioni sulla configurazione AI anche quando invia file non validi; impedisce il test in locale delle validazioni upload senza chiave AI.

**File coinvolto:** `backend/TeLoConsiglio.Api/Controllers/SittingsController.cs` — metodo `ImportPdf` — la riga `!_ai.IsConfigured` va spostata dopo le validazioni file.

---

## Nessun bug bloccante rilevato

Tutti gli scenari critici (email fallback, policy autorizzazione inviti, policy import-pdf, IDOR sedute, build) risultano PASS. L'unico bug è classificato Minore.
