# LibKdbx

C# library for reading, writing, creating, and merging [KeePass](https://keepass.info/) (`.kdbx`) databases. Targets .NET 10. Supports KDBX 3.1 and 4.1.

[![NuGet](https://img.shields.io/nuget/v/LibKdbx)](https://www.nuget.org/packages/LibKdbx)
[![CI](https://github.com/mammothb/LibKdbx/actions/workflows/ci.yml/badge.svg)](https://github.com/mammothb/LibKdbx/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/mammothb/LibKdbx/branch/main/graph/badge.svg)](https://codecov.io/gh/mammothb/LibKdbx)

## Features

- **Read & write** KDBX 4.1 and 3.1 databases
- **Create** new databases with passwords and/or key files
- **Full CRUD** for groups and entries
- **Entry history** — automatic snapshots on modification
- **Recycle bin** — soft-delete with recycle-bin-aware deletion
- **KeePassXC-compatible search** — field-specific queries, wildcard/regex, exclusions, `is:`, `has:` operators
- **Placeholder resolution** — `{TITLE}`, `{USERNAME}`, `{S:CustomAttribute}`, etc.
- **Field references** — `{REF:…}` resolution across entries
- **Database merge** — three modes (keep newer, synchronize, keep existing) with history merge
- **Attachments** — binary data per entry
- **Protected fields** — in-memory protection via inner stream encryption
- **Custom data** — plugin-friendly key/value metadata on database, groups, and entries
- **Deleted objects** tracking
- **Group settings** — tri-state flags (enable/disable/inherit), auto-type, searching, tags
- **Sorting** — recursive group sort with recycle-bin-last ordering
- **Async overloads** — `OpenAsync` / `SaveAsync` with cancellation support

### Crypto

| Feature | Supported |
| ------- | --------- |
| KDF | Argon2d, Argon2id, AES-KDF |
| Cipher | ChaCha20, AES-256 |
| Compression | GZip |
| Inner stream | ChaCha20, Salsa20 |

## Installation

```bash
dotnet add package LibKdbx
```

## Quickstart

### Open & read

```csharp
using LibKdbx;

using Database db = Database.Open("passwords.kdbx", "master-password");

Entry? entry = db.FindEntry("GitHub");
Console.WriteLine(entry?.Password);

// Advanced search
EntrySearcher searcher = new(caseSensitive: false);
List<Entry> results = searcher.Search("title:Bank", db.RootGroup!);
```

### Create & save

```csharp
Database db = Database.Create("master-password");
db.Metadata!.Name = "My Vault";

Group work = new() { Name = "Work" };
db.RootGroup!.AddGroup(work);

Entry entry = new()
{
    Title = "GitHub",
    UserName = "alice",
    Password = "s3cr3t!",
    Url = "https://github.com",
    Notes = "Work account"
};
work.AddEntry(entry);

db.SaveAs("my-vault.kdbx");
```

### Update with history

```csharp
using Database db = Database.Open("passwords.kdbx", "master-password");
Entry? entry = db.FindEntry("GitHub");

entry!.Update(e =>
{
    e.Password = "new-password";
    e.Notes = "Updated notes";
});

// entry.History now contains the previous version
db.Save();
```

### Key file

```csharp
// Open with password + key file
using Database db = Database.Open("passwords.kdbx", "master-password", "keyfile.xml");

// Create with key file
Database db2 = Database.Create("master-password", "keyfile.xml");
```

### Merge databases

```csharp
using Database source = Database.Open("peer.kdbx", "password");
using Database target = Database.Open("local.kdbx", "password");

Merger merger = new()
{
    DefaultMode = MergeMode.Synchronize,  // keep source as truth
    DryRun = false
};
merger.Merge(source, target);

target.Save();
```

### Search examples

```csharp
EntrySearcher searcher = new();

// Field-specific
searcher.Search("title:GitHub password:*123", db.RootGroup!);

// Exclusions
searcher.Search("+title:Facebook -tag:work", db.RootGroup!);

// Custom attributes
searcher.Search("_TOTP.Secret:*", db.RootGroup!);

// Expiration
searcher.Search("is:expired-14", db.RootGroup!);

// TOTP
searcher.Search("has:totp", db.RootGroup!);

// UUID
searcher.Search("uuid:abc123...", db.RootGroup!);
```

## Requirements

- .NET 10

## Dependencies

- [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) — ChaCha20 and Salsa20 inner stream ciphers

## Build & test

```bash
# Restore, build, test, pack
make              # or: make pack

# Run tests only
make test

# Coverage report (HTML)
make coverage-report
```

## License

GPL-3.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE) for upstream attribution.

KeePassXC is used as the KDBX format reference implementation.
