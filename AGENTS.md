# LibKdbx — AI Agent Instructions

This file is intentionally thin. The canonical conventions live in
**[CONTRIBUTING.md](CONTRIBUTING.md)** — read it first. It covers the
project overview, build/test entry points, code style, and the full set
of conventions derived from the codebase.

## Project overview

C# library for reading, writing, creating, and merging KeePass (`.kdbx`)
databases. Targets .NET 10. Supports KDBX 3.1 and 4.1. Single external
dependency: BouncyCastle.Cryptography (ChaCha20/Salsa20 inner stream
ciphers). Flat `LibKdbx` namespace; folder structure is internal
organization only.

## Build & test

```bash
make              # restore → build (Release) → test → pack
make test         # test only
make pack         # full pipeline, output to ./nupkgs/
```

## Orientation

1. Read [CONTRIBUTING.md](CONTRIBUTING.md) for conventions and workflow.
2. Formatting is owned by CSharpier — run `dotnet csharpier format .` before committing. Do not manually format.
3. Tests are xUnit v3 + Shouldly. One test class per source type, mirroring the source path under `LibKdbx.Tests/`.
4. After changing code, run `make test`.
