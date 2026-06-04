# Code Review — TeLoConsiglio (branch `claude/relaxed-hawking-pO5Tc` @ `092a9f3`)

## 1. Riepilogo esecutivo

Lo scaffold del portale è coerente nelle linee generali (separazione layer Domain/Infrastructure/Api, EF Core + Identity + JWT lato backend, React + TanStack Query + zustand persist lato frontend), ma presenta diverse **debolezze di sicurezza** (JWT key di default hardcoded e debole, credenziali admin di default seedate e prefillate nel form di login, refresh token salvati in chiaro e non ruotati, CORS con `AllowCredentials()` insieme a wildcard di metodi/headers) e numerose **inconsistenze di contratto fra backend e frontend** (enum `ActStatus`/`ActType`/`AgendaDecision`/`DocumentType` totalmente diversi, campo `agendaItems` vs `items`, response `references` vs `suggestions`, route `me` mai chiamata correttamente) che faranno fallire la maggior parte delle chiamate appena si esce dai casi felici. Mancano inoltre l'endpoint `/api/auth/refresh` lato client (nessun refresh automatico), il controllo di `Authorize` su `UsersController.List` espone l'intera anagrafica utenti a qualunque consigliere, e la validazione di upload (estensione/MIME/size) e di path è praticamente assente.

## 2. Sicurezza

### 2.1 JWT key debole e hardcoded in repo (Critico)
- `backend/TeLoConsiglio.Api/Program.cs:42` e `appsettings.json:13`: chiave di fallback `"DevOnly_ChangeMe_..."` committata. È usata anche dal `docker-compose.yml:30` come default. Inoltre la chiave è solo 50 caratteri ASCII e in UTF-8 fornisce ~400 bit, ma se l'operatore non la sostituisce è di dominio pubblico ⇒ token forgiabili.
- **Fix**: rifiutare l'avvio in non-Development se `JWT_KEY` non è impostata; rimuovere la chiave dal file `appsettings.json` (lasciare solo Issuer/Audience). Documentare in README il setup obbligatorio.

### 2.2 Credenziali admin di default seedate, sempre, idempotentemente (Critico)
- `backend/TeLoConsiglio.Api/Seed/DataSeeder.cs:19-43`: l'utente `admin@teloconsiglio.io` / `Admin!2026` viene creato ad ogni primo avvio e queste credenziali sono **prefillate nel form di login** (`frontend/src/pages/Login.tsx:8-9`).
- **Fix**: leggere `ADMIN_EMAIL`/`ADMIN_PASSWORD` da env; rifiutare seed in Production se non impostati; rimuovere il prefill dal form di login.

### 2.3 Refresh token salvati in chiaro e non ruotati correttamente (Alto)
- `backend/TeLoConsiglio.Api/Auth/JwtTokenService.cs:56-62` genera token random ma `AuthController.Refresh` (`AuthController.cs:74-83`) lo cerca **per valore in chiaro** (`r => r.Token == dto.RefreshToken`): in caso di leak DB i refresh sono utilizzabili così come sono. Inoltre non c'è cleanup periodico dei token scaduti/revocati.
- Manca anche la "reuse detection": se un refresh già revocato viene riusato non viene invalidata la famiglia di token.
- **Fix**: hashare in DB (SHA-256 del token bytes), confrontare con hash; aggiungere `ParentTokenId`/`ReplacedByTokenId` per detection di replay; job di cleanup.

### 2.4 Endpoint `/api/auth/refresh` non usato dal client (Alto)
- `frontend/src/api/endpoints.ts` non contiene `refresh`; l'interceptor (`client.ts:17-25`) fa `logout()` su 401 senza tentare refresh ⇒ utente buttato fuori ogni 60 minuti.
- **Fix**: implementare `authApi.refresh()` con coda di richieste pending e ritry una volta sola; salvare la nuova coppia in store.

### 2.5 JWT salvato in localStorage in chiaro (Alto)
- `frontend/src/auth/store.ts:14-26` usa `persist` su `localStorage` (default), incluso `token` e `refreshToken`. Qualsiasi XSS (anche da contenuti markdown renderizzati in futuro, o da pacchetti compromessi) esfiltra l'identità.
- **Fix**: spostare almeno il refresh token in cookie HttpOnly+Secure+SameSite=Strict; preferire access token in memoria. Aggiungere CSP nell'host frontend (oggi non c'è).

### 2.6 IDOR: nessun filtro `OwnerId` su Sittings (Alto)
- `backend/TeLoConsiglio.Api/Controllers/SittingsController.cs:26-69`: `List`, `Get`, `Delete` e `AddAgendaItem` non filtrano per `CreatedById`. Qualunque consigliere autenticato vede e cancella sedute altrui (e items annessi).
- Anche `UpdateAgendaItem` e `DeleteAgendaItem` (riga 98-129) lavorano solo per `itemId`, senza verifica owner.
- **Fix**: filtrare per `CreatedById == uid` (o introdurre concetto di "comune/gruppo" condiviso); validare proprietà del sitting per ogni operazione su agenda.

### 2.7 UsersController espone l'intera anagrafica (Alto)
- `backend/TeLoConsiglio.Api/Controllers/UsersController.cs:19-30`: ritorna email, fullName, comune, partito, ruoli di TUTTI gli utenti, accessibile a qualunque `Consigliere`.
- **Fix**: restringere ad `[Authorize(Roles="Admin,CapogruppoConsiliare")]` o filtrare per stesso `Comune`; ridurre i campi (solo `Id`+`FullName`).

### 2.8 Upload: nessuna validazione estensione/MIME, possibile path traversal latente (Alto)
- `DocumentsController.cs:62-85` e `ProfileController.cs:65-87`: il nome generato (`Guid + Path.GetExtension(file.FileName)`) è ok per il filesystem, ma `Path.GetExtension` su input come `file.pdf%00.exe` o estensioni doppie non è validato. Nessun whitelist `.pdf/.docx/.txt/.md`, nessun check MIME, `RequestSizeLimit(50MB)` ma `ExtractedText` viene salvato intero in DB Postgres ⇒ DoS possibile con PDF text-heavy.
- `_extractor.ExtractTextAsync` legge l'intero file in memoria (`UglyToad.PdfPig` apre l'intero documento).
- **Fix**: whitelist estensioni, controllo magic bytes/MIME, sanitizzare/limitare lunghezza `ExtractedText` (es. 1MB), troncare prima di salvare.

### 2.9 CORS troppo permissivo (Medio)
- `Program.cs:128-138`: `AllowCredentials()` + `AllowAnyHeader()` + `AllowAnyMethod()` con origin esplicito è ok formalmente, ma la lista include sempre `localhost:5173/3000` anche in Production. Il dominio frontend in prod va fornito tramite `FRONTEND_URL`, gli altri vanno rimossi.
- **Fix**: in Production lasciare solo `FRONTEND_URL` (`if (env.IsDevelopment()) {...}`).

### 2.10 Password policy debole per consigliere comunale (Medio)
- `Program.cs:27-37`: 8 char minimi, niente lockout configurato (default Identity è 5 tentativi/5 minuti, ma `Lockout.AllowedForNewUsers` non è verificato qui), niente 2FA forzato per Admin.
- **Fix**: alzare a 12 caratteri minimi, abilitare lockout esplicito (`opt.Lockout`), valutare 2FA per Admin.

### 2.11 Esposizione segreti e Swagger sempre attivo (Medio)
- `docker-compose.yml:40` imposta `ENABLE_SWAGGER=true` anche in Production ⇒ schema API + bottoni di test esposti.
- `appsettings.json` ha le password DB hardcoded ed è committato.
- **Fix**: rimuovere Swagger in prod o proteggerlo con auth; spostare `appsettings.json` di default a `Production` privo di secret.

### 2.12 Refresh: response inconsistente con frontend (Medio)
- `AuthResponseDto` lato BE ha `AccessToken/ExpiresAt/RefreshToken/User`; lato frontend `AuthResponse` (`api/types.ts:12-17`) coincide ma `expiresAt` non viene mai usato per schedulare refresh.
- **Fix**: vedi 2.4; usare `expiresAt` per pre-refresh.

## 3. Correttezza

### 3.1 Mismatch enum/contratto FE-BE generalizzato (Critico)
- `ActStatus`: BE `Bozza|Depositato|Approvato|Respinto` (`Act.cs:11-17`) vs FE `Bozza|InRevisione|Pronto|Presentato|Archiviato` (`api/types.ts:26`). Tutti i `PUT /api/acts/{id}` con `status` fallirebbero la deserializzazione, e l'editor non lascia mai scegliere uno status valido lato BE (`AttoEditor.tsx:8`).
- `ActType`: FE aggiunge `Comunicazione`, BE non lo conosce.
- `AgendaDecision`: BE `DaDecidere|Approvare|Respingere|Astenersi`; FE `Indecisa|Favorevole|Contraria|Astenuto` (`SedutaDettaglio.tsx:8`). Create/update agenda items falliranno.
- `DocumentType`: BE `Delibera|Verbale|Documento|Altro`; FE `Documento|Delibera|OrdineDelGiorno|Verbale|Allegato`.
- **Fix**: allineare un solo set di valori (preferibilmente quello FE che è più ricco) e aggiornare entities + migrazione. Aggiungere un test di smoke su `POST /api/acts` e `PUT agenda` per ciascun valore.

### 3.2 SittingDetailDto: `agendaItems` vs `items` (Critico)
- BE: `SittingDetailDto(..., List<AgendaItemDto> AgendaItems)` → JSON camel: `agendaItems` (`SittingDtos.cs:8`).
- FE: legge `detail.data.items` (`SedutaDettaglio.tsx:34,55`, `types.ts:129`). La pagina mostrerà sempre lista vuota.
- **Fix**: rinominare il record a `Items` o aggiornare frontend.

### 3.3 `suggestLegalRefs`: backend ritorna `references`, frontend si aspetta `suggestions` (Alto)
- BE: `SuggestLegalRefsResponse(List<SuggestedLegalRef> References)` (`ActDtos.cs:18`) ⇒ JSON `{ references: [...] }`.
- FE: `api.post<{ suggestions: SuggestedRef[] }>` (`endpoints.ts:82-83`). Inoltre il modale legge gli ID di `legalReferences` dell'atto, NON dei suggerimenti restituiti ⇒ il payload `suggestions` non è nemmeno usato. Funziona "per caso" se si invalida la query.
- **Fix**: uniformare il nome del campo a `references` lato FE, oppure rinominare DTO BE in `Suggestions`.

### 3.4 `InsertLegalRefsRequest.Mode` accetta valori non documentati (Medio)
- BE: commento dice `"appendVisti" | "placeholder"` (`ActDtos.cs:20`), codice confronta con `"placeholder"` ⇒ qualunque altro valore va in append. Frontend invia `'append'` (`endpoints.ts:84`). Tutto ok, ma il commento è fuorviante; manca anche la copia dei `Citation/Description` nel revisionato come campo dedicato.
- **Fix**: enum `InsertMode { Append, Placeholder }` validato.

### 3.5 Logica `[[REF]]` non gestisce più occorrenze né mancanza (Medio)
- `ActsController.cs:260-267`: se `Mode == "placeholder"` e `[[REF]]` non c'è, **fa SOLO append senza avvisare** (il fallback è silenzioso). Se ci sono N `[[REF]]`, `Replace` li sostituisce **tutti** con lo stesso blocco ⇒ duplicazione massiva. Non c'è log/warning.
- **Fix**: usare `Replace` solo della **prima** occorrenza (`Regex.Replace` con count=1), e ritornare 400 o un warning se il placeholder è assente quando richiesto.

### 3.6 `Refresh` non emette nuovo refresh token quindi non c'è rotazione (Medio)
- `AuthController.Refresh` revoca il vecchio token e poi `BuildAuthResponse` ne crea uno nuovo. Ok, ma manca link al token revocato per detection di replay (cfr 2.3) e non c'è limite di concorrenza.
- **Fix**: vedi 2.3.

### 3.7 `ApplicationUser` cancellato lascia orfani (Medio)
- `Act.OwnerId` ha `OnDelete(Restrict)` ⇒ non si può cancellare un utente con atti. Ma `Sitting.CreatedBy` è anch'esso Restrict, mentre `AgendaItemAssignment.User` è Cascade. Niente cleanup di `RefreshTokens` lo è (Cascade). Il design è incoerente: se cancello un utente con atti, l'op fallisce a livello SQL senza messaggio user-friendly.
- **Fix**: definire una strategia (soft-delete utente, o Cascade ovunque con back-reference).

### 3.8 `Sitting.Data` ricevuta senza specificare DateTimeKind in update (Basso)
- `SittingsController.cs:51` fa `SpecifyKind(...,Utc)` su create ma non c'è endpoint update per `Sitting`; ok per ora.

### 3.9 DI: registrazioni complete, ma `IConfiguration` in `AnthropicService` legge anche env separatamente (Basso)
- `AnthropicService.cs:21` fa fallback su `Environment.GetEnvironmentVariable` mentre `Program.cs:16` già fa `AddEnvironmentVariables()`. Ridondante, ma non bug.

### 3.10 Nullability/null-check (Basso)
- `_users.GetUserId(User)!` (`SittingsController.cs:24`, `ActsController.cs:28`, `DocumentsController.cs:30`) usa null-forgiving senza guard ⇒ NRE se token con `sub` mancante. Improbabile ma una `Unauthorized()` esplicita sarebbe più pulita (`ProfileController.cs:28` lo fa).

### 3.11 Async corretto (OK)
- Nessun `.Result`/`.Wait()` trovato; uso di `Thread.Sleep(2000)` nel migrate retry (`Program.cs:160`) è accettabile in fase di startup (sync intenzionale).

## 4. Qualità / bug logici

### 4.1 Nessun timeout configurato sull'HttpClient verso Anthropic (Alto)
- `Program.cs:96` registra `AddHttpClient<IAnthropicService, AnthropicService>()` senza `Timeout`/`PolicyHandler`. Default 100s, niente retry su 429/5xx, niente cancellation linkata alla request HTTP del controller.
- **Fix**: `.ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(120))` e Polly per retry esponenziale su 429/503; passare `HttpContext.RequestAborted` come `ct`.

### 4.2 Nessun limite di dimensione/lunghezza nei prompt AI (Medio)
- `ActsController.SuggestLegalRefs` passa `a.BodyMd` senza troncare (vs. il troncamento a 30000 in `Documents.Summarize`). BodyMd può crescere indefinitamente.
- **Fix**: troncare a un limite documentato (es. 20k char) e segnalarlo.

### 4.3 `ExtractJson` greedy: prende dal **primo** `{` all'**ultimo** `}` (Medio)
- `ActsController.cs:296-302`, `DocumentsController.cs:172-178`. Se il modello prepone testo con `{` di esempio, l'estrazione include spazzatura ⇒ JsonParseException.
- **Fix**: cercare il primo blocco JSON bilanciato; meglio chiedere al modello con prefill `{` o usare lo strumento JSON di Claude.

### 4.4 LegalReferences vengono persistite anche senza conferma user (Medio)
- `SuggestLegalRefs` crea record con `Inserted=false` e li lascia in DB anche se l'utente annulla. Si accumulano e poi `Get` li ritorna tutti.
- **Fix**: o memorizzarli solo on-confirm, o aggiungere cleanup dei "non confermati" più vecchi di X.

### 4.5 `Revisions` salvate ogni save anche se body invariato (Basso)
- `ActsController.Update` confronta `a.BodyMd != dto.BodyMd` (ok), ma il diff testuale è binario "uguale/diverso", quindi anche un trailing newline genera revisione. Anche `InsertLegalRefs` aggiunge sempre una nuova revisione ⇒ storia rumorosa.
- **Fix**: normalizzare (`Trim()`) prima di confrontare; valutare se memorizzare delta.

### 4.6 React Query: invalidations OK ma niente refresh su token expiry (vedi 2.4) (Medio)

### 4.7 `Login.tsx` prefilla credenziali admin reali (Critico — vedi 2.2)

### 4.8 Frontend: route protette ok, ma nessun gate su ruolo (Basso)
- `ProtectedRoute.tsx` controlla solo `token`. Nessun `RoleRoute`. Per ora non ci sono pagine admin-only.
- **Fix**: aggiungere `RoleGate` quando arriveranno pagine Admin.

### 4.9 Frontend `client.ts`: `baseURL` default `http://localhost:5000` anche in build prod (Basso)
- Ok perché l'env è iniettato a build-time via Vite, ma è prudente un check.

## 5. Pulizia

### 5.1 Doppi route alias confondenti (Basso)
- `ActsController` ha `[HttpPost("{id:guid}/generate-draft")] [HttpPost("{id:guid}/ai-draft")]` e tre alias per insert/confirm. Aumentano la superficie e divergono dal contratto unico.
- **Fix**: tenere un solo route e fare il commit di rimozione degli alias.

### 5.2 `IConfiguration` iniettato in `AuthController` ma usato solo per `Providers()` (Basso)
- Si potrebbe leggere una volta in `Program.cs` o un service.

### 5.3 Commenti TODO/dead code (Basso)
- Nessun `TODO` sospeso trovato; bene.
- `// Mode: "appendVisti" | "placeholder"` (ActDtos.cs:20) è fuorviante: il codice non riconosce `appendVisti`.

### 5.4 `IConfiguration["JWT_KEY"] ?? Configuration["Jwt:Key"]` accetta entrambe le notazioni (Basso)
- Funziona ma duplicato in `Program.cs` e `JwtTokenService`. Centralizzare in `JwtSettings` con binding.

### 5.5 File grossi (Basso)
- `ActsController.cs` ~300 righe con prompt AI inline: estrarre i prompt in una classe dedicata `IActPromptBuilder` aiuterebbe leggibilità.

## 6. Top 10 fix prioritari

| # | Severità | Finding | File principale |
|---|----------|---------|----------------|
| 1 | Critico | Mismatch totale enum FE/BE (ActStatus/ActType/AgendaDecision/DocumentType) → metà delle write API rotte | `types.ts`, `Act.cs`, `AgendaItem.cs`, `Document.cs` |
| 2 | Critico | JWT key di default debole, hardcoded in repo e in `docker-compose.yml`; nessun fail-fast | `Program.cs:42`, `appsettings.json:13`, `docker-compose.yml:30` |
| 3 | Critico | Seed admin con credenziali fisse `Admin!2026` prefillate nel form di login | `DataSeeder.cs:19-43`, `Login.tsx:8-9` |
| 4 | Critico | `SittingDetailDto.AgendaItems` vs frontend `items` → pagina seduta sempre vuota | `SittingDtos.cs:8`, `types.ts:129`, `SedutaDettaglio.tsx:34` |
| 5 | Alto | IDOR su Sittings/AgendaItems: list/get/delete senza filtro OwnerId | `SittingsController.cs:26-129` |
| 6 | Alto | `UsersController.List` espone anagrafica completa a tutti i Consiglieri | `UsersController.cs:19-30` |
| 7 | Alto | Refresh token in chiaro a DB, niente rotazione/replay-detection; nessun client refresh automatico → logout ogni 60 min | `AuthController.cs:74-83`, `client.ts:17-25` |
| 8 | Alto | Upload senza whitelist estensione/MIME/size effettivo del testo estratto; ExtractedText scaricato intero in DB | `DocumentsController.cs:62-85`, `ProfileController.cs:65-87` |
| 9 | Alto | HttpClient verso Anthropic senza timeout/retry; nessun handling 429/5xx; `CancellationToken` non propagato | `Program.cs:96`, `AnthropicService.cs:48` |
| 10 | Medio | `suggestLegalRefs` response `references` vs frontend `suggestions`; `[[REF]]` sostituito **tutte** le occorrenze e fallback silenzioso ad append | `ActDtos.cs:18`, `endpoints.ts:82`, `ActsController.cs:260-267` |
