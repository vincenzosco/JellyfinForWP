<p align="center">
  <img src="JellyfinPlayer/JellyfinPlayer/Assets/BrandMark.png" width="128" alt="Logo di JellyfinPlayer">
</p>

<h1 align="center">JellyfinPlayer</h1>

<p align="center">
  <strong>Esplora e riproduci la tua libreria Jellyfin su un dispositivo Windows Phone 8.1.</strong><br>
  L'app è scritta in VB.NET e usa XAML e il lettore multimediale integrato nel telefono.
</p>

<p align="center">
  <a href="#installazione">Installazione</a> ·
  <a href="#usare-lapp">Usare l'app</a> ·
  <a href="#risoluzione-dei-problemi">Risoluzione dei problemi</a> ·
  <a href="agents/README.md">Guida per sviluppatori</a> ·
  <a href="README.md">English</a>
</p>

JellyfinPlayer si collega a un server Jellyfin da un dispositivo Windows Phone 8.1. È un'app Windows Phone Silverlight 8.1 nativa (`.xap`), non un'app Windows Phone Store/WinRT. Serve un server Jellyfin raggiungibile in rete; l'app non ne include uno.

JellyfinPlayer è distribuito con la [licenza MIT](LICENSE).

## Cosa fa

- Cerca server Jellyfin nella rete locale tramite UDP sulla porta `7359`. Puoi anche inserire manualmente l'indirizzo del server.
- Accede tramite `/Users/AuthenticateByName` e può conservare più account sullo stesso telefono.
- Mostra Continua a guardare, Prossimi episodi, librerie, contenuti paginati, stagioni, dettagli e risultati di ricerca per film, serie ed episodi.
- Riproduce direttamente quando il server offre una sorgente compatibile. Altrimenti richiede una transcodifica MP4 progressiva. L'avanzamento viene inviato al server.
- Se la riproduzione diretta non riesce, prova una volta la transcodifica. Se fallisce anche quella, il lettore può proporre il flusso diretto come alternativa.
- Include la riproduzione a tutta pagina (tray di sistema nascosto, orientamento orizzontale consentito), salto indietro di 10 secondi e avanti di 30, mute, adatta/riempi e controlli di avanzamento.
- Permette di scegliere la traccia audio e i sottotitoli dal lettore; una traccia audio non predefinita o un sottotitolo scelto vengono rimuxati o incorporati dal server, alla stessa posizione.
- Prosegue con l'episodio successivo quando uno finisce.
- Ordina una libreria per nome o per data di aggiunta.
- L'interfaccia è disponibile in inglese e italiano e segue la lingua del dispositivo.

**Adatto a:** esplorare e riprodurre una libreria Jellyfin da un vecchio dispositivo Windows Phone 8.1.

**Non adatto a:** Live TV, download o sincronizzazione offline, gestione delle raccolte o file di sottotitoli esterni. I sottotitoli già noti al server sono selezionabili e vengono incorporati nella transcodifica. Vedi [le limitazioni note](agents/MAINTENANCE.md#known-limitations).

## Requisiti

- Un dispositivo o emulatore Windows Phone 8.1.
- Visual Studio 2013 con Windows Phone 8.1 SDK per compilare e distribuire il sorgente.
- Un server Jellyfin raggiungibile dal telefono. La porta HTTP predefinita è `8096`.

Windows Phone 8.1 è una piattaforma datata. Il progetto è destinato a quella piattaforma e non è un'app attuale per Windows, Android o iOS.

## Installazione

Il repository non rimanda a una scheda verificata nello store né a un download di release. Per compilare e distribuire l'app serve un PC Windows con Visual Studio 2013 e Windows Phone 8.1 SDK.

Apri `JellyfinPlayer/JellyfinPlayer.sln` in Visual Studio 2013, seleziona l'emulatore `x86` o il dispositivo `ARM`, quindi compila e distribuisci il progetto `JellyfinPlayer`.

Per compilare da Git Bash con MSBuild 12:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Per una build Release ARM:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Release -p:Platform=ARM -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Il pacchetto Silverlight generato (`.xap`) si trova in `JellyfinPlayer/JellyfinPlayer/bin/<piattaforma>/Debug/`, ad esempio `JellyfinPlayer_Debug_x86.xap`. Distribuiscilo da Visual Studio oppure con uno strumento per i pacchetti `.xap`.

> In Git Bash usa `-p:` per le proprietà MSBuild. La forma `/p:` può essere convertita in un percorso e interrompere il comando.

## Collegare il server

1. Avvia l'app sul telefono.
2. Se la ricerca Jellyfin funziona sulla rete locale, l'indirizzo viene compilato automaticamente. Altrimenti, inserisci l'URL del server, ad esempio `http://192.168.1.20:8096`.
3. Accedi con il nome utente e la password Jellyfin.
4. Apri una libreria, scegli un contenuto e seleziona Riproduci o Riprendi.

Il telefono e il server devono potersi raggiungere in rete. La ricerca usa broadcast UDP, che potrebbe non attraversare subnet o VLAN; se non trova il server, puoi inserirne l'indirizzo manualmente.

## Usare l'app

- **Home:** continua a guardare, prossimi episodi e librerie.
- **Libreria:** scorri la griglia paginata.
- **Serie:** scegli una stagione per vedere gli episodi.
- **Dettaglio:** riproduci o riprendi e attiva/disattiva Preferito o Visto.
- **Account:** passa tra gli accessi salvati senza ridigitare ogni volta le password.
- **Lettore:** usa i controlli per spostarti nel video, silenziare l'audio, cambiare adatta/riempi o attivare lo schermo intero. Il tasto Indietro del telefono esce dallo schermo intero prima di lasciare la pagina.
- **Impostazioni:** modifica il bitrate massimo dello streaming o esci.

## Risoluzione dei problemi

<details>
<summary>L'app non trova il server Jellyfin</summary>

La ricerca invia un broadcast UDP sulla porta `7359`; la rete può bloccarlo o impedirgli di attraversare subnet/VLAN. Inserisci manualmente l'indirizzo del server e verifica che il telefono raggiunga la porta HTTP di Jellyfin, di solito `8096`.
</details>

<details>
<summary>Jellyfin non accetta l'indirizzo del server</summary>

Inserisci l'URL base, ad esempio `http://192.168.1.20:8096`, non l'indirizzo di una libreria o di un endpoint API. Il server deve essere raggiungibile dal telefono. Per HTTPS serve un certificato attendibile da Windows Phone; l'app non considera attendibili i certificati autofirmati.
</details>

<details>
<summary>Un video non parte</summary>

Il lettore integrato nel telefono supporta un insieme limitato di formati. JellyfinPlayer chiede al server un flusso diretto o una transcodifica MP4 progressiva e, dopo un errore del flusso diretto, prova una volta la transcodifica. Se falliscono entrambe le opzioni, controlla la configurazione di riproduzione/transcodifica del server e prova l'alternativa di riproduzione diretta, se proposta.
</details>

<details>
<summary>La build fallisce in Git Bash su una proprietà MSBuild</summary>

Usa `-p:Configuration=Debug`, non `/p:Configuration=Debug`. Git Bash può riscrivere gli argomenti che iniziano con `/` come percorsi del filesystem.
</details>

## Compilare e testare

Compila il progetto di test della logica condivisa con la toolchain VS2013, poi avvia i test unitari con MSTest v1:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj \
  -p:Configuration=Debug -p:Platform=AnyCPU -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal

MSYS_NO_PATHCONV=1 "/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings \
  /category:"!Integration" /noresults
```

`MSYS_NO_PATHCONV=1` impedisce a Git Bash di riscrivere gli argomenti MSTest che iniziano con `/`. Il comando qui sopra esclude i test d'integrazione; per eseguirli servono le impostazioni del server locale descritte nelle [istruzioni di test](agents/TESTING.md).

## Struttura del progetto

I servizi della piattaforma e le pagine XAML sono in `JellyfinPlayer/JellyfinPlayer/`. Il client API Jellyfin e la logica indipendente dalla piattaforma sono in `JellyfinPlayer/Shared/`, collegata sia all'app per il telefono sia al progetto di test desktop.

Altri dettagli:

- [Guida per sviluppatori](agents/README.md)
- [Architettura](agents/ARCHITECTURE.md)
- [Comandi di build](agents/BUILD.md)
- [Test e checklist sul dispositivo](agents/TESTING.md)
- [Uso delle API Jellyfin](agents/JELLYFIN_API.md)
- [Manutenzione e limitazioni](agents/MAINTENANCE.md)
- [Convenzioni di codice](agents/CONVENTIONS.md)

## Licenza

MIT — vedi [LICENSE](LICENSE). Puoi usare, modificare e ridistribuire l'app,
anche per scopi commerciali, purché l'avviso di copyright e il testo della
licenza restino insieme al software. Il software è fornito senza garanzie.
