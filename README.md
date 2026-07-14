# ClaudeCosts

Eine Windows-Tray-App (C# / WPF), die die Funktionalität von
[ccusage](https://github.com/ccusage/ccusage) in einem übersichtlichen Fenster bündelt.
Im Tray steht dauerhaft der **$-Betrag** des gewählten Zeitraums; ein Klick öffnet das
Dashboard mit Verlauf und Modell-Aufschlüsselung.

> Der angezeigte Betrag ist der **berechnete API-Listenpreis** (wie ccusage) – nicht der
> real abgerechnete Betrag. Bei einem Max-/Pro-Abo ist es der theoretische API-Gegenwert.

## Funktionen

- **Tray-Icon** zeigt die Kosten des aktuellen Zeitraums (Standard: Monat).
- **Zeitraum umschaltbar**: Tag / Woche / Monat / Gesamt (Fenster oder Tray-Menü) — gilt zugleich für den Tray-Wert.
- **Dashboard**: Verlauf (Balken) + Aufschlüsselung nach Modell (Opus / Sonnet / Haiku / Fable).
- **Live-Aktualisierung**: ein `FileSystemWatcher` erkennt neue Nutzung, plus periodischer Refresh (60 s).
- **Autostart mit Windows** (umschaltbar, `HKCU\…\Run`).
- **Offline**: Preise liegen in einer editierbaren `pricing.json` – kein Netzwerk nötig.

## Datenquelle

Gelesen werden die lokalen Claude-Code-Transkripte unter
`%USERPROFILE%\.claude\projects\**\*.jsonl` (bzw. `CLAUDE_CONFIG_DIR` / `~/.config/claude`).
Da die Logs keinen Preis enthalten, werden die Kosten aus Tokens × Modellpreis berechnet
(inkl. Cache-Write 5 min = 1,25×, 1 h = 2×, Cache-Read = 0,1× des Input-Preises).
De-Duplizierung erfolgt pro `message.id`+`requestId` (der vollständige Turn mit dem höchsten
`output_tokens` gewinnt) — identisch zur ccusage-Methodik.

## Bauen & Starten

```bash
dotnet build ClaudeCosts.sln
dotnet run --project src/ClaudeCosts.App          # startet in den Tray
dotnet run --project src/ClaudeCosts.App -- --show # startet und öffnet das Fenster
dotnet test                                        # Core-Unit-Tests
```

In Rider einfach `ClaudeCosts.sln` öffnen und `ClaudeCosts.App` starten.

## Projektstruktur

| Projekt | Zweck |
|---|---|
| `src/ClaudeCosts.Core` | Parsing, Preise, Kosten, Aggregation (reine Logik, `net8.0`, testbar) |
| `src/ClaudeCosts.App` | WPF-UI + Tray (`net10.0-windows`, H.NotifyIcon.Wpf) |
| `tests/ClaudeCosts.Core.Tests` | xUnit-Tests für Kosten, De-Dup und Aggregation |

## Konfiguration

Liegt unter `%APPDATA%\ClaudeCosts`:

- `settings.json` – gewählter Zeitraum, Wochenstart, Refresh-Intervall, Fensterposition.
- `pricing.json` – Modellpreise (Prefix-Match, längster Prefix gewinnt). Neue Modelle hier
  ergänzen, ohne neu zu kompilieren (Tray-Menü → „Preise bearbeiten …“).
