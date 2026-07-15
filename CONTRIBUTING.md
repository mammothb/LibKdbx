# Contributing to LibKdbx

C# library for reading/writing KeePass (`.kdbx`) databases. .NET 10, KDBX 3.1/4.1.
Single external dependency: BouncyCastle.Cryptography.

## Build & test

```bash
make              # restore → build (Release) → test → pack
make test         # test only
make pack         # full pipeline, output to ./nupkgs/
make coverage-report  # test + coverage HTML
```

Tools are local via `dotnet-tools.json`. Run `dotnet tool restore` after checkout.

## Pre-commit

```bash
prek install   # one-time setup
prek run       # manual run
```

Enforces: no large files, no case/Windows conflicts, no merge markers, valid
TOML/YAML, consistent line endings, trailing whitespace stripped, CSharpier
formatting, Roslynator analysis.

## Project structure

```
LibKdbx/           # Library — flat LibKdbx namespace
├── Core/          # Enums, primitives, format helpers
├── Crypto/        # KDF, ciphers, protected stream
├── Kdbx/          # Binary format reader/writer/header
├── Keys/          # Composite key derivation
├── Models/        # Public API (Database, Entry, Group, Merger)
└── Search/        # EntrySearcher, PlaceholderResolver

LibKdbx.Tests/     # xUnit v3 + Shouldly (mirrors source structure)
```

## Code conventions

**[`.editorconfig`](.editorconfig)** is authoritative. **CSharpier** owns
formatting — no manual style decisions. **Roslynator** (warning level)
catches quality issues.

Rules not obvious from tooling:

- **File-scoped namespaces** only — `namespace LibKdbx;` (no braces).
- **No `var`** — explicit types everywhere. `List<Entry> results = [];`
- **Primary constructors** for classes that take dependencies.
- **Private fields** `_camelCase`; static fields `s_camelCase`.
- **Flat public namespace** — folder structure is internal organization.
- **Section separators** in large files: `// ── Section Name ──`
- **XML doc comments** on all public types and members.
- **No new NuGet dependencies** without explicit approval.

## Testing conventions

- xUnit v3, Shouldly assertions.
- One test class per source type, mirroring source path.
- Test names: `Method_Scenario_ExpectedBehavior` (underscores).
- Assertions: `ShouldBe()`, `ShouldNotBeNull()`, `ShouldThrow<T>()`.

## Pull requests

1. Branch, make changes, add tests
2. `make test` + `prek run`
3. Open PR against `master`

CI runs build + test + coverage on every push and PR.

## Releases

1. Push to `master`
2. Create a [GitHub Release](https://github.com/mammothb/LibKdbx/releases/new)
   with a semver tag (e.g. `0.4.0`)
3. CI builds, tests, packs, pushes to NuGet

The tag **is** the version.

## License

GPL-3.0.
