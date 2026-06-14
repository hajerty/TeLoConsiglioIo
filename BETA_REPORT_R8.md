# BETA REPORT R8 — Phase 6: OAuth Google + Microsoft Wiring

**Data:** 2026-06-14  
**Branch:** claude/relaxed-hawking-pO5Tc  
**Commit HEAD:** b7b9e76  
**Stack:** Backend ASP.NET Core :5000 + Postgres 16 (locale) — senza GEMINI_API_KEY, senza Google/Microsoft OAuth creds

---

## Tabella esiti scenari

| Scenario | Descrizione | Esito | Note |
|---|---|---|---|
| A | GET /api/auth/providers → `{email:true, google:false, microsoft:false}` | PASS | Risposta corretta |
| B1 | GET /api/auth/external/google senza creds → 503 | PASS | Messaggio chiaro con istruzioni env vars |
| B2 | GET /api/auth/external/microsoft senza creds → 503 | PASS | Messaggio chiaro |
| B3 | GET /api/auth/external/INVALID → 400 | PASS | `"Provider 'INVALID' non supportato. Valori validi: google, microsoft."` |
| C1 | Login admin seed | PASS | `admin@teloconsiglio.io / Admin!2026` |
| C2 | PUT /me/complete-profile `{comune:"Milano",partito:"PD"}` → 200 | PASS | Campi aggiornati confermati da GET /me |
| C3 | GET /me dopo complete-profile | PASS | comune=Milano, partito=PD |
| C4 | PUT /me/complete-profile `{comune:""}` → 400 | PASS | ValidationProblemDetails con `Comune` required |
| C5 | PUT /me/complete-profile senza auth → 401 | PASS | |
| D1 | Riavvio con `GOOGLE_CLIENT_ID=fake-id GOOGLE_CLIENT_SECRET=fake-secret` → providers `google:true` | PASS | |
| D2 | GET /api/auth/external/google → 302 Location: accounts.google.com | PASS | `Location: https://accounts.google.com/o/oauth2/v2/auth?client_id=fake-id&...` |
| E1 | `npm run build` → 0 errori | PASS | 1876 moduli trasformati, bundle 471 KB |
| E2 | Route `/oauth-callback` presente nel build | PASS | Stringa trovata nel chunk JS e nel router.tsx |
| E3 | Route `/completa-profilo` presente nel build | PASS | Stringa trovata nel chunk JS e nel router.tsx |
| F1 | Login admin / registrazione utente | PASS | |
| F2 | Lista atti/sedute/documenti | PASS | HTTP 200 |
| F3 | IDOR sedute cross-comune | PASS | 404 corretto (user di Napoli non vede sedute di Roma) |
| F4 | /api/admin/encryption-status come Admin | PASS | 200 |
| F5 | /api/users espone solo `{id,displayName}` | PASS | |
| F6 | Inviti POST/GET/DELETE | PASS | Creato, listato, revocato (204) correttamente |
| F7 | import-pdf senza Gemini + PDF valido → 503 | PASS | `"GEMINI_API_KEY non configurata"` |
| F8 | import-pdf con file .exe → 400 | PASS | `"Estensione non consentita"` |

---

## Bug trovati

### BUG-R8-01 — Minore: Disallineamento tipo `ProvidersDto.password` tra frontend e backend

**Severità:** Minore  
**File coinvolti:** `frontend/src/api/types.ts`, `backend/TeLoConsiglio.Api/Dtos/AuthDtos.cs`  
**Passi per riprodurre:**
1. GET `/api/auth/providers` → risponde `{"email":true,"google":false,"microsoft":false}`
2. Il tipo TypeScript nel frontend è `ProvidersDto { password: boolean; google: boolean; microsoft: boolean }`
3. Il campo `email` del backend corrisponde al campo `password` del frontend — il nome è diverso

**Risposta osservata:** Il campo `email: true` del backend viene ricevuto come `undefined` per `providers.password` nel frontend. Il campo `providers.password` risulta `undefined` (falsy).  
**Impatto effettivo:** Nullo sulla UI attuale — il login form email/password è mostrato sempre, non dipende da `providers.password`. I pulsanti OAuth (Google/Microsoft) dipendono solo da `providers.google` e `providers.microsoft` che funzionano correttamente. Tuttavia, se in futuro si volesse nascondere il form password per utenti solo-OAuth, il controllo `providers.password` non funzionerebbe.

---

### Nota su scenario F7/F8 (import-pdf "file invalido")

Lo scenario chiedeva `400 (con file invalido)`; il test con `.txt` restituisce correttamente **503** (non 400) perché `.txt` è un'estensione ammessa dall'`UploadValidator` (`.pdf .txt .md .docx`). Il 400 si ottiene correttamente con file `.exe`. Non è un bug.

---

## Riepilogo qualità

- **21/21 check superati** (inclusi tutti i new e i non-regression)
- **1 bug** trovato, Minore, senza impatto funzionale immediato
- Nessun blocco o regressione
- Frontend build pulito (0 errori TypeScript + Vite)
- OAuth wiring corretto: 503 senza creds, 302 a accounts.google.com con creds fake

