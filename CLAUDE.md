# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**CorvaxGoob** — actively developed fork of Goob Station (itself a fork of Space Station 14). A multiplayer game built on the RobustToolbox engine using C# with an Entity-Component-System (ECS) architecture. Official SS14 docs: https://docs.spacestation14.io/

## Build & Run

```bash
# First-time setup (initializes submodules and downloads engine)
python3 RUN_THIS.py

dotnet restore
dotnet build --configuration DebugOpt   # debug build
dotnet build --configuration Release    # release build

# Run locally
./runserver.sh        # or runserver.bat on Windows
./runclient.sh        # or runclient.bat on Windows

# Tests
dotnet test Content.Tests/Content.Tests.csproj

# Packaging
dotnet run --project Content.Packaging server --platform linux-x64
dotnet run --project Content.Packaging client --no-wipe-release
```

SDK: .NET 9.0.100 (pinned in `global.json`). Use `runclient-Tools` / `runserver-Tools` variants for in-game developer tools.

## Architecture

The codebase splits strictly by execution context:

| Project | Purpose |
|---|---|
| `Content.Shared` | Components, events, and systems that run on both client and server |
| `Content.Server` | Server-only systems, database access, admin commands |
| `Content.Client` | Rendering, input, UI |
| `Content.Goobstation.*` | Goob Station feature extensions (Common, Maths, Shared, Server, Client, UIKit) |
| `Content.Corvax.Interfaces.*` | Community extension point interfaces (Server/Client/Shared) |
| `Content.Tests` | Unit tests |
| `Content.YAMLLinter` | Validates game YAML files (runs in CI) |
| `RobustToolbox/` | Engine submodule — do not modify directly |

**ECS pattern**: game logic lives in *Systems* (`EntitySystem` subclasses), state lives in *Components* (`Component` subclasses), and cross-system communication uses *Events*. Systems are registered via IoC; components are serialized via `[DataField]` attributes.

**Dependency direction**: `RobustToolbox → Content.Shared → Content.Server / Content.Client`. Shared code must never depend on Server or Client projects.

Within each project, folders are organized by game domain (e.g. `Access/`, `Atmos/`, `Actions/`), with `Components/`, `Systems/`, and `Events/` subdirectories inside each domain.

## IS14 Project Conventions

### Code placement

All new IS14 code lives under `_IS14/` subdirectories within the appropriate project, mirroring the domain structure used elsewhere:

- `Content.Shared/_IS14/<Domain>/` — namespace `Content.Shared._IS14.<Domain>`
- `Content.Server/_IS14/<Domain>/` — namespace `Content.Server._IS14.<Domain>`
- `Content.Client/_IS14/<Domain>/` — namespace `Content.Client._IS14.<Domain>`

When a change to an **upstream file** (outside `_IS14/`) is unavoidable, wrap it with markers:

```csharp
//IS14-change start
// ... changed lines ...
//IS14-change end
```

This makes upstream diffs easy to locate and minimizes merge conflicts during upstream syncs.

### Prototypes

All new prototypes and YAML data files go in `Resources/Prototypes/_IS14/`.

When an existing upstream prototype needs to be adjusted, **create a child prototype** instead of editing the original:

```yaml
- type: entity
  parent: UpstreamEntityName
  id: IS14UpstreamEntityName
  # only override what's needed
```

Only modify upstream prototypes directly when inheritance cannot achieve the desired result.

### General principle

Minimize the footprint in files outside `_IS14/`. Prefer adding new files over patching existing ones. If upstream logic must be extended, use partial classes, event subscriptions, or child prototypes before resorting to direct edits.

## Namespaces

Use **file-scoped namespaces** (`namespace Foo.Bar;`).

IS14 namespace pattern: `Content.[Layer]._IS14.[Domain]`

Upstream namespace pattern: `Content.[Layer].[Domain]` or `Content.[Layer].[Domain].[SubDomain]`

Each project has a `GlobalUsings.cs` with pre-imported namespaces (`System`, `System.Collections.Generic`, core Robust types). No need to re-import those.

## Naming Conventions (enforced as warnings by .editorconfig)

| Symbol | Style | Example |
|---|---|---|
| Types, namespaces, methods, properties, events, public fields, local functions | PascalCase | `MySystem`, `HandleDamage` |
| Interfaces | `I` + PascalCase | `IAccess` |
| Type parameters | `T` + PascalCase | `TEntity` |
| Private instance/static fields | `_camelCase` (underscore prefix) | `_myField` |
| Parameters, local variables, local constants | camelCase | `entityUid` |
| `const` fields (non-private) and `static readonly` (non-private) | PascalCase | `MaxDamage` |

Max line length: 120 characters. Indentation: 4 spaces. YAML/XML/csproj files: 2 spaces.

Prefer `var` throughout. Expression-bodied members for properties and accessors. Accessibility modifiers required on all non-interface members.

## Pull Requests

PR template (`.github/PULL_REQUEST_TEMPLATE.md`) requires:
- Description, balance/reasoning, technical details, media (if gameplay-visible)
- A changelog block using the `:cl:` marker — required for player-facing changes:

```
:cl:
- add: Added fun!
- fix: Fixed fun!
- tweak: Changed fun!
- remove: Removed fun!
```

Breaking changes (renamed namespaces, changed public APIs, renamed prototypes) must be listed explicitly with migration instructions.

PRs to `master` are automatically closed — target `staging` or `stable` branches.

## CI Workflows

Key automated checks (`.github/workflows/`):
- `build-test-debug.yml` — build + unit tests on every PR/push to protected branches
- `yaml-linter.yml` — validates all game YAML via `Content.YAMLLinter`
- `validate-rsis.yml` / `validate-rgas.yml` — sprite and animation format validation
- `check-crlf.yml` — rejects CRLF line endings
- `reuse-updater.yml` — enforces SPDX license compliance

Submodule updates (`RobustToolbox`) are blocked by CI — never commit submodule pointer changes.
