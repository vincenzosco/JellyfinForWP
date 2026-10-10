# Logo, EN/IT localisation and HTML-instead-of-JSON handling

Date: 2026-10-10
Status: agreed with the requester (chat request + emulator run log)

## Request (verbatim, Italian)

> vicino alla scritta jellyfin, metti il logo a sinistra, inoltre rendi l'app in
> lingua inglese ed italiano, a seconda di che cosa è messo il device cambia,
> stessa cosa per il repo, default en e anche readme in ita, inoltre con la prima
> prova ottengo html e questi sono i log

Four deliverables:

1. Put the logo immediately **left of the "Jellyfin" wordmark**.
2. App must be **English + Italian**, selected by the device language.
3. Repository: **English default**, plus an **Italian README**.
4. First run on the emulator: **the app shows HTML** — fix it. The run log is the
   evidence below.

## Evidence: emulator run log

```
'JellyfinPlayer.exe' ... Loaded 'Windows.winmd'
... Loaded 'SYSTEM.RUNTIME.SERIALIZATION.JSON.NI.DLL'
... Loaded 'SYSTEM.SERVICEMODEL.WEB.NI.DLL'
A first chance exception of type 'System.Runtime.Serialization.SerializationException'
  occurred in mscorlib.ni.dll
A first chance exception of type 'System.Runtime.Serialization.SerializationException'
  occurred in SYSTEM.SERVICEMODEL.WEB.NI.DLL
A first chance exception of type 'System.Runtime.Serialization.SerializationException'
  occurred in mscorlib.ni.dll
```

No unhandled exception: the app caught it and the run continued. The JSON
deserializer rejected the body, and the screen showed the response body.

## Diagnosis (verified by reading the code)

Two distinct paths, both in the connect flow:

1. **Non-2xx response with an HTML body → the raw HTML is printed on screen.**
   `Shared/HttpTransport.vb` `SendAsync` does
   `Throw New JellyfinException(body, CInt(response.StatusCode))` — the whole
   response body becomes the exception message. `MainPage.ConnectButton_Click`
   has `Catch ex As JellyfinException → StatusText.Text = ex.Message`, so a
   404/500 HTML page from a wrong address is rendered verbatim in the status
   label. This is the reported "ottengo html".

2. **2xx response that is not JSON → `SerializationException`.**
   The transport returns the body, `Shared/JellyfinApiClient.vb` calls
   `Json.Deserialize(Of …)(body)` (12 call sites) and
   `DataContractJsonSerializer` throws `SerializationException`, which is never
   wrapped. It lands in `Catch ex As Exception → StatusText.Text = ex.Message`,
   an unhelpful .NET message. This produces the first-chance
   `SerializationException` lines in the log.

**Why an HTML body arrives at all:** the app cannot tell. `MainPage.DiscoverAsync`
fills the address field with `found(0)` from UDP 7359 discovery, and
`ServerDiscoveryParser` only checks the datagram shape
(`{"Address","Id","Name"}`) — it never confirms the address is a Jellyfin
server. Any other device answering on 7359 puts its own address in the field,
and every following request fetches that device's HTML. Manual entry of a
non-Jellyfin address has the same effect.

Nothing in the app distinguishes "not JSON", "HTTP 404", "timeout" and
"unreachable", so the user cannot tell what to fix.

## Acceptance criteria

- **A.** No path shows a raw response body or a raw .NET exception message. Every
  failure carries a classification (HTTP failure / not JSON / timeout /
  unreachable / unknown), a short message, and the HTTP status where one exists.
- **B.** Automatic discovery only fills the address field with an address that
  passes `JellyfinApiClient.ValidateConnectionAsync`; otherwise it says so and
  leaves manual entry.
- **C.** The Jellyfin logo appears immediately left of the "Jellyfin" wordmark on
  the login page and in the home header.
- **D.** Every user-visible string exists in English and Italian: identical key
  sets, no empty values, identical `{n}` placeholders per key, every `x:Uid` in
  XAML and every key used from code resolved. The device language selects the
  language; English is the default and the fallback.
- **E.** The repository root has `README.md` (English, default) and
  `README.it.md` (Italian), cross-linked.
- **F.** `Debug|x86` and `Release|ARM` builds succeed; the MSTest suite is green
  (baseline before this work: 59 tests, 52 unit + 7 integration).

## Out of scope

- Verifying the emulator UI (language switch, logo placement, discovery) at
  runtime: no emulator is available here. Manual checklist goes in
  `agents/TESTING.md`.
- Anything else about playback, transcoding or server state.
