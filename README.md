# Herohold

A local D&D 2024 character sheet and play companion, built with .NET 10 and Blazor Server.

## Run

```sh
./dotnet/run.sh
```

Then open `http://127.0.0.1:8510`. Characters are stored in a SQLite database,
`.tmp/herohold-dotnet-data/herohold.db`, unless `Herohold:DatabasePath` (or `Herohold:DataDirectory`) is set.

Only player-owned facts are stored: build choices, HP, inventory, journal, spent resources, combat
and undo state. Everything the rules can work out (scores, proficiencies, AC, attacks, spells, slots,
descriptions) is recalculated when a character is loaded. The schema is managed by EF Core migrations
in `dotnet/Herohold.Storage/Migrations` and applied automatically on start.

Saves from before the database (one JSON file per character) are imported once with:

```sh
dotnet run --project dotnet/Herohold.Web -- import-json <old saves folder> <database file>
```

A character is imported only if the rules rebuild it exactly as it was saved; otherwise the
differences are listed and it is left out. `--accept-recalculated` imports it with the recalculated
values.

## Rules data

The rules catalogs and the official character sheet are Wizards of the Coast content and are
**not** in this repository. Herohold expects them locally, git-ignored:

| Path | Contents |
| --- | --- |
| `data/raw/5etools/` | Source catalogs (classes, races, spells, items, feats) |
| `data/imported/5etools/` | Normalized catalogs the app loads |
| `data/parity/` | Golden rules snapshots used by `Herohold.Parity.Tests` |
| `templates/pdf/5E_CharacterSheet_Fillable.pdf` | Fillable sheet used for PDF export |

Without them the app does not start and PDF export reports the missing sheet. The solution still
builds; tests that need these files are tagged `[TestCategory("RulesData")]`.

## Test

```sh
dotnet build dotnet/Herohold.slnx
pwsh dotnet/Herohold.Browser.Tests/bin/Debug/net10.0/playwright.ps1 install chromium   # once per machine
dotnet test dotnet/Herohold.slnx
```

Browser tests (`Herohold.Browser.Tests`, Playwright for .NET) start the real app on a free loopback
port with an empty workspace per test and build their fictional characters through the native
workflow.

CI has no rules data, so it runs only the tests that do not need it:

```sh
dotnet test dotnet/Herohold.slnx --filter "TestCategory!=RulesData"
```

Run the full suite locally before merging. A new test that reads the rules data needs the
`RulesData` category, or CI fails on it.

## Layout

| Project | Purpose |
| --- | --- |
| `Herohold.Core` | Character model, calculations, play and combat rules, the stored character record |
| `Herohold.Storage` | SQLite tables, EF Core migrations and the character record store |
| `Herohold.Rules` | Class, species, feat, spell and equipment rules over the catalogs |
| `Herohold.Documents`, `Herohold.PdfWorker` | PDF import/export in an isolated worker process |
| `Herohold.Web` | Blazor UI, workspace session and service registration |
| `Herohold.Parity` | Runs the rules over `dotnet/fixtures` for the golden tests |
| `*.Tests` | MSTest suites (unit, rules, component, HTTP, documents, integration, parity, browser) |

Rules contracts live beside the code (`dotnet/Herohold.Rules/*CONTRACT.md`, `dotnet/Herohold.Core/*.md`).
