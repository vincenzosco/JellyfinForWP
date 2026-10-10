# JellyfinPlayer per Windows Phone 8.1

Un client Jellyfin per **Windows Phone 8.1** (WinRT XAML), scritto in **VB.NET** e
compilato con **Visual Studio 2013 / MSBuild 12**. Parla con un server Jellyfin sulla tua
rete locale. L'interfaccia è disponibile in **inglese e italiano** e segue la lingua del
dispositivo.

Read this in [English](README.md).

## Funzioni

- **Ricerca del server all'avvio**: una sonda UDP sulla porta 7359 compila l'indirizzo — e
  solo dopo che quell'indirizzo ha risposto come server Jellyfin. L'inserimento manuale
  funziona sempre.
- Accesso tramite `/Users/AuthenticateByName`.
- Home: Continua a guardare, Prossimi episodi e l'elenco delle librerie.
- Griglia della libreria con paginazione, pagina della serie con scelta della stagione,
  dettaglio con Riproduci/Riprendi, Preferito e Visto.
- Ricerca tra film, serie ed episodi.
- Riproduzione: diretta quando il server dichiara il file riproducibile direttamente,
  altrimenti transcodifica in MP4 HTTP progressivo. I progressi vengono inviati al server,
  così il resume funziona.
- Lettore a schermo intero: un pulsante nasconde la barra di stato e i comandi, il video
  riempie lo schermo, la rotazione segue il telefono e lo schermo resta acceso; il tasto
  Indietro esce prima da schermo intero e solo dopo dalla pagina.
- Comandi del lettore: indietro di 10 s / avanti di 30 s, mute, riempi/mostra tutto,
  tempo trascorso e durata totale, indicatore di caricamento, e comandi che si nascondono
  da soli solo mentre il video va (un tocco li riporta).
- Impostazioni: bitrate massimo in streaming, esci.
- Interfaccia in inglese e italiano scelta dalla lingua del dispositivo, inglese come
  predefinito.

## Requisiti

- Visual Studio 2013 con l'SDK di Windows Phone 8.1: MSBuild 12, i target WindowsXaml
  v12.0 e `Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll` nella cartella
  `Common7/IDE/PublicAssemblies` di VS2013.
- Un server Jellyfin raggiungibile dal telefono: stessa rete e, su Windows, firewall che
  consente la porta del server (8096 di default).
- Nessun ripristino pacchetti: la build non richiede pacchetti NuGet.

## Compilazione

Da eseguire dalla radice del repository, in bash (Git Bash su Windows):

```bash
# Debug, x86 (emulatore)
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal

# Release, ARM (telefono)
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Release -p:Platform=ARM -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Entrambe terminano con `Your package has been successfully created.`, in
`JellyfinPlayer/JellyfinPlayer/AppPackages/`. Usa il prefisso `-p:`: Git Bash altera la
forma `/p:`. Per l'installazione usa Visual Studio (**Deploy**) oppure lo script
`Add-AppDevPackage.ps1` dentro la cartella `AppPackages/..._Test`.

## Test

```bash
# compila prima il progetto di test
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj \
  -p:Configuration=Debug -p:Platform=AnyCPU -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal

# poi eseguilo
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings
```

Una esecuzione positiva stampa `N/N test(s) Passed`. Alcuni test parlano con un server
reale: copia `JellyfinPlayer/JellyfinPlayer.Core.Tests/test.settings.local.example` in
`test.settings.local`, inserisci la password e **non committarlo mai**. Senza un server
configurato quei test di integrazione riportano `Inconclusive`.

## Documentazione

- [`agents/README.md`](agents/README.md) — cosa fa l'app e come è organizzata.
- [`agents/BUILD.md`](agents/BUILD.md) — tutti i comandi di build e test.
- [`agents/TESTING.md`](agents/TESTING.md) — test unitari e di integrazione, più la
  checklist manuale sul dispositivo (lingua, logo, autodiscovery, comandi del lettore).
- [`agents/ARCHITECTURE.md`](agents/ARCHITECTURE.md),
  [`agents/CONVENTIONS.md`](agents/CONVENTIONS.md),
  [`agents/JELLYFIN_API.md`](agents/JELLYFIN_API.md),
  [`agents/MAINTENANCE.md`](agents/MAINTENANCE.md) — gli interni.
- [README in inglese](README.md).
