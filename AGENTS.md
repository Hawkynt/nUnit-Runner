# Agent guide — nUnit-Runner

Working agreement for **all** coding agents and human contributors working in
this repository. These rules are not optional. The full house spec lives in
the `Hawkynt/project-template` repo (`STANDARD.md`); this file is the
per-repo distillation.

## What this is

A **universal NUnit test runner** spanning .NET Framework 2.0 through
.NET 9.0+ (`UniversalRunner`, 16 target frameworks, isolated AppDomains on
classic Framework). Solution `nUnit-Runner.slnx`; tests in
`UniversalRunner.Tests` (net48/net8/net9).

## Commits

- **Group changes semantically/logically** — one concern per commit.
- **Every subject line starts with a prefix**: `+` added · `-` removed ·
  `*` changed · `#` bug fixed · `!` critical todo.
- Never start a subject with "fix"/"bugfix"/"changed"/"modified".
- **No AI traces anywhere**: no `Co-Authored-By` AI lines, no "Generated
  with" footers, no agent mentions in messages, comments, or authorship.

## The loop (always, in this order)

1. **Before committing**: `dotnet build nUnit-Runner.slnx -c Release`
   (the full TFM spread needs Windows) and
   `dotnet test UniversalRunner.Tests -c Release` until green. Update the
   README's framework table when TFMs change.
2. **Commit** (rules above) and **push**.
3. **Wait for CI** (Windows = full spread, ubuntu = modern TFMs); on `main`
   a green CI triggers the nightly (prerelease + GFS prune). Fix and loop
   until everything is green.

Stable releases are **manual** (`gh workflow run release.yml`) — never cut
one unless explicitly asked.

## Code conventions

- Latest C# features where the oldest TFM allows — polyfill the rest via
  `FrameworkExtensions.Backports` (already the pattern for net20–net45
  Task/async support); guard newer APIs behind TFM conditions.
- AppDomain/process isolation behavior is the product: changes there get
  tests on BOTH classic Framework and modern .NET.
- Never drop a supported TFM silently — that's a breaking `*` change with a
  README update.

## README & repo conventions

- Standard frame: title → badges → one-line `>` blockquote; fixed emoji
  mapping for the standard sections (`## 🚀 Usage`, `## 🛠️ Build`,
  `## ❤️ Support`, `## 📜 License`); repo-specific sections keep their
  consistent topical emojis.
- License is LGPL-3.0-or-later; the `## ❤️ Support` section and
  `.github/FUNDING.yml` stay intact.
