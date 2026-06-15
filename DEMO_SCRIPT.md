# TeLoConsiglio.io — Script Demo Promozionale

**Durata target:** 2:30 – 3:00 minuti  
**Strumenti consigliati:** Loom / OBS + Descript per il voice-over  
**Risoluzione:** 1280×800 (finestra browser) o 1920×1080 schermo intero  
**Font overlay / caption:** Montserrat Bold 36pt bianco con ombra  
**Musica di sottofondo:** corporate/uplifting, 80 BPM, volume -18 dB  

---

## SCENA 1 — APERTURA (0:00 – 0:15)

**Schermo:** Pagina `/landing` — hero section  
**Animazione:** Fade-in dal bianco → landing page completamente visibile  
**Caption overlay:**  
> *"Sei un consigliere comunale. Ogni settimana: delibere da leggere, atti da redigere, sedute da preparare."*

**Azione:** Mouse scorre lentamente verso il basso mostrando le feature card  
**Voice-over:**  
> "Il lavoro in consiglio comunale richiede tempo, attenzione e tantissima carta. TeLoConsiglio.io nasce per cambiare tutto questo."

---

## SCENA 2 — LOGIN E ONBOARDING (0:15 – 0:35)

**Schermo:** Pagina `/login`  
**Azione:** Click su "Accedi con Google" → OAuth flow (simulato)  
**Caption:** *"Accesso in 1 clic con Google o Microsoft"*

**Schermo:** Dashboard dopo login — la modale di onboarding appare automaticamente  
**Azione:** Clicca sui 3 step iniziali della guida interattiva (Benvenuto → Dashboard → Profilo)  
**Voice-over:**  
> "La guida interattiva ti accompagna fin dal primo accesso, step dopo step."

**Azione:** Chiudi la guida con "Inizia"

---

## SCENA 3 — PROFILO POLITICO + AI (0:35 – 0:55)

**Schermo:** `/profilo`  
**Caption:** *"Il tuo profilo politico guida l'intelligenza artificiale"*  
**Azione:**  
1. Click su "Importa dal manifesto del partito" → seleziona "PD"  
2. La linea politica si popola automaticamente  
3. Mostra il badge "Importato dal partito"  
4. Aggiungi 2 chip in "Argomenti forti": *ambiente, scuole*

**Voice-over:**  
> "Configuri la tua linea politica una volta sola. L'AI la usa per redigere ogni atto in modo coerente con il tuo programma."

---

## SCENA 4 — CREA UN ATTO CON AI (0:55 – 1:20)

**Schermo:** `/atti` → click su "Nuovo atto" → seleziona "Mozione"  
**Caption:** *"Crea una mozione in meno di 2 minuti"*  
**Azione:**  
1. Compila il form: Titolo = "Mozione per la riqualificazione del parco centrale"  
2. Oggetto = "Impegno dell'amministrazione nella riqualificazione del parco"  
3. Click su **"Bozza AI"** — appare lo spinner  
4. Dopo 3-4 secondi: il testo si popola nell'editor  
5. Zoom leggero sull'editor per mostrare il testo strutturato  

**Voice-over:**  
> "Descrivi l'oggetto dell'atto. L'AI genera la bozza completa in pochi secondi, rispettando la tua linea politica."

**Azione:** Click su "Suggerisci norme" → appaiono i riferimenti normativi  
**Caption overlay:** *"Riferimenti normativi automatici"*

---

## SCENA 5 — SEDUTA E ODG (1:20 – 1:45)

**Schermo:** `/sedute`  
**Caption:** *"Gestisci le sedute e l'ordine del giorno"*  
**Azione:**  
1. Click su una seduta esistente  
2. Si apre la pagina di dettaglio — lista dell'ODG  
3. Click su "Importa da PDF" — modale si apre  
4. File PDF selezionato → "Analizza PDF"  
5. Dopo 2 secondi: i punti ODG appaiono nella preview  
6. Click "Importa" — le voci si aggiungono alla seduta  

**Voice-over:**  
> "Carica il PDF dell'ordine del giorno ufficiale e TeLoConsiglio.io lo importa automaticamente, voce per voce."

---

## SCENA 6 — DOCUMENTI E RIASSUNTO AI (1:45 – 2:05)

**Schermo:** `/documenti`  
**Caption:** *"Analizza delibere e verbali con l'AI"*  
**Azione:**  
1. Click su un documento già caricato con badge "riassunto"  
2. Si espande la card con i punti chiave e le criticità  
3. Evidenzia con il mouse la sezione "Criticità"  

**Voice-over:**  
> "Carica delibere e verbali. L'AI genera un riassunto con i punti chiave e le possibili criticità — così puoi prepararti in 60 secondi invece di leggere 30 pagine."

**Caption piccolo in basso:** *"Crittografia AES-256 · Dati privati e sicuri"*

---

## SCENA 7 — COLLABORAZIONE (2:05 – 2:20)

**Schermo:** `/consiglieri`  
**Caption:** *"Lavora con il tuo gruppo"*  
**Azione:**  
1. Mostra la lista dei consiglieri invitati con badge ruolo  
2. Click su "Invita consigliere" → form con email → "Invia invito"  
3. Badge "Email inviata" appare sulla nuova riga  

**Voice-over:**  
> "Invita i consiglieri del tuo gruppo, assegna i ruoli e collaborate sugli stessi atti. Ogni azione viene registrata nell'audit log."

---

## SCENA 8 — CHIUSURA + CTA (2:20 – 2:50)

**Schermo:** Torna alla landing page `/landing` — sezione CTA finale  
**Animazione:** Zoom leggero sul pulsante "Inizia subito"  

**Caption overlay grande:**  
> *"TeLoConsiglio.io"*  
> *"Il tuo assistente AI in consiglio comunale."*

**Voice-over:**  
> "TeLoConsiglio.io è gratuito, sicuro e pronto all'uso. Attiva il tuo account in meno di un minuto e inizia a lavorare meglio."

**Azione:** Click su "Inizia subito" → la pagina di registrazione si apre  

**Fade to white → Logo TeLoConsiglio.io centrato**  
**Caption finale:** `app.teloconsiglioio.it`  

---

## NOTE DI PRODUZIONE

| Elemento | Dettaglio |
|---|---|
| Browser | Chrome, finestra 1280×800, tema chiaro, nessuna estensione visibile |
| Account demo | Usare account `demo@teloconsiglioio.it` con dati fittizi pre-popolati |
| Velocità mouse | Movimento lento e deliberato, 1.5x più lento del normale |
| Pause | 0.5s pausa dopo ogni click importante |
| Zoom | Usa Loom Smart Zoom oppure OBS "follow mouse" 1.5x in sezioni critiche |
| Sottotitoli | Aggiungi closed captions in italiano per l'accessibilità |
| Thumbnail | Frame della Scena 4 (AI genera la bozza) — visual più impattante |

---

## VARIANTE SHORT (30 secondi — per social / Instagram Reels)

| Sec | Contenuto |
|---|---|
| 0–3 | Titolo: "Sei un consigliere comunale?" |
| 3–8 | AI genera bozza mozione (time-lapse accelerato) |
| 8–15 | Import ODG da PDF → lista voci |
| 15–22 | Riassunto AI di una delibera → punti chiave |
| 22–28 | "Gratis. Sicuro. In italiano." |
| 28–30 | Logo + URL |

---

## COPY SOCIAL MEDIA

### LinkedIn / Facebook
> **TeLoConsiglio.io** — L'assistente AI per i consiglieri comunali.
> 
> ✅ Bozze di atti in 60 secondi con AI  
> ✅ Import ODG da PDF automatico  
> ✅ Riassunto di delibere e verbali  
> ✅ Collaborazione con il tuo gruppo  
> ✅ Crittografia AES-256 · Dati sicuri  
> 
> 🇮🇹 Pensato per i comuni italiani.  
> Attiva gratis → [link]

### X / Twitter
> Sei consigliere comunale?  
> 
> 👉 Bozza una mozione in 60 secondi con AI  
> 👉 Importa l'ODG da PDF in 1 clic  
> 👉 Analizza delibere automaticamente  
> 
> TeLoConsiglio.io — gratuito 🇮🇹  
> [link]
