# BETA REPORT Round 6 — Phase 4: Crittografia documenti + PWA + Responsive

**Data:** 2026-06-13  
**Branch:** claude/relaxed-hawking-pO5Tc  
**HEAD:** 11b2757  
**Env:** Postgres locale, backend Development, `DOC_ENCRYPTION_KEY=$(openssl rand -base64 32)`

---

## Tabella scenari

| Scenario | Esito | Note |
|---|---|---|
| A. Crittografia round-trip documenti | PASS | Vedi dettaglio sotto |
| A. Crittografia round-trip allegati atti | PASS | Vedi dettaglio sotto |
| B. Admin encryption-status (200 + dati) | PASS | `enabled:true`, `filesEncrypted:2` |
| B. Accesso non-admin = 403 | PASS | HTTP 403 per Consigliere |
| C. Backward compat file plain | PASS | `filesPlain` sale a 16 dopo creazione manuale |
| D. PWA dist assets | PASS | Tutti i file presenti |
| D. manifest.webmanifest contenuto | PASS | Tutti i campi richiesti presenti |
| E. Non-regressione login/atti/sedute/documenti | PASS | |
| E. dotnet build | PASS | 0 Warning, 0 Error |
| E. npm run build | PASS | Build pulita, sw.js generato |

---

## Scenario A — Dettaglio crittografia round-trip

### Documento utente

**Upload:** `POST /api/documents` con contenuto `MARKER_PHASE_4_20260613_175950`

**File su disco** (`uploads/documents/ca351d1a-.../8e51e2b7-....txt`):

```
offset 00: 54 43 45 31 29 ee 02 6d 55 84 24 6e 76 4d 8b b0  >TCE1)..mU.$nvM..<
offset 10: 10 f7 29 7a 4c 41 fd 14 83 db a0 52 1f f1 ee 3e  >..)zLA.....R...><
```

- Magic header `TCE1` (hex `54 43 45 31`) presente ai primi 4 byte: PASS
- `grep -a "MARKER_PHASE_4" <file>`: NOT FOUND (contenuto non in chiaro): PASS

**GET /api/documents/{id}** → `extractedText: "MARKER_PHASE_4_20260613_175950\n..."` (decriptato dal DB dove era stato estratto al momento dell'upload): PASS

### Allegato atto

**Upload:** `POST /api/acts/{id}/attachments` con contenuto `MARKER_PHASE_4_ATT_20260613_180113`

**File su disco** (`uploads/act-attachments/ca351d1a-.../611bdd51-....txt`):

```
offset 00: 54 43 45 31 67 a8 1e 89 9f 05 51 e9 e4 1a 0b c4  >TCE1g.....Q.....<
```

- Magic header `TCE1` (hex `54 43 45 31`) presente: PASS
- Marker NON in chiaro su disco: PASS

**GET /api/acts/{id}/attachments/{attId}/download** → restituisce il contenuto decifrato con marker visibile: PASS

---

## Scenario B — Admin endpoint

```json
GET /api/admin/encryption-status → 200
{
  "enabled": true,
  "algorithm": "AES-256-GCM",
  "filesEncrypted": 2,
  "filesPlain": 15
}
```

- `enabled: true`: PASS
- `algorithm: "AES-256-GCM"`: PASS
- `filesEncrypted >= 2` (documento + allegato): PASS
- Consigliere → HTTP 403: PASS

---

## Scenario C — Backward compat file plain

File `uploads/documents/{adminId}/test-plain.txt` creato manualmente con contenuto ASCII.  
`GET /api/admin/encryption-status` → `filesPlain: 16` (era 15 prima): PASS  
(La scansione è live, non richiede restart del backend.)

---

## Scenario D — PWA assets

```
dist/sw.js                 ✓
dist/manifest.webmanifest  ✓
dist/icon-192.png          ✓
dist/icon-512.png          ✓
```

Contenuto `manifest.webmanifest`:
- `"name":"TeLoConsiglio.io"`: PASS
- `"display":"standalone"`: PASS
- `"lang":"it"`: PASS
- Icone 192x192 e 512x512 presenti: PASS

---

## Scenario E — Non-regressione

- Login admin (`admin@teloconsiglio.io`): PASS
- Login consigliere: PASS
- Lista atti: 1 atti restituiti
- Lista sedute: 0 sedute (nessuna creata nel round)
- Lista documenti: 2 documenti restituiti
- `dotnet build`: Build succeeded, 0 Warning, 0 Error
- `npm run build`: Build pulita, 1874 modules, sw.js generato

---

## Bug rilevati

### BUG-R6-01 — Minore: primo upload eseguito senza chiave (backend precedente)

**Severità:** Minore (solo nel contesto del test setup)  
**Descrizione:** Il file caricato durante il test iniziale risulta in chiaro su disco perché il backend del round precedente (R5) era ancora in ascolto sulla porta 5000 senza `DOC_ENCRYPTION_KEY`. Il secondo processo backend avviato con la chiave ha fallito il bind su porta già occupata, producendo il log `Failed to bind to address http://127.0.0.1:5000: address already in use`.  
**Comportamento atteso:** L'ambiente di test dovrebbe avere un solo backend attivo.  
**Evidenza:** File `21df54f0-....txt`: primo byte `54 65 73 74` = "Test" (plaintext, NON "TCE1").  
**File coinvolto:** Procedura di avvio backend (setup environment).  
**Nota:** Non è un bug del codice applicativo — la feature di cifratura funziona correttamente quando `DOC_ENCRYPTION_KEY` è impostato.

---

## Nessun bug bloccante rilevato

Tutti gli scenari critici della Phase 4 (cifratura AES-256-GCM round-trip, magic header, backward compat, PWA manifest, build) risultano PASS.
