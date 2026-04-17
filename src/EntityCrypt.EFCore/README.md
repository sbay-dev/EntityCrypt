# EntityCrypt.EFCore

**Transparent Data-at-Rest Encryption for Entity Framework Core**
**.NET 10 · AES-256-GCM · ML-KEM-768 (FIPS 203) · Hybrid PQC**

[![NuGet](https://img.shields.io/nuget/v/EntityCrypt.EFCore)](https://www.nuget.org/packages/EntityCrypt.EFCore)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512bd4)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/sbay-dev/EntityCrypt/blob/main/LICENSE)

EntityCrypt provides **zero-code-change**, column-level and schema-level encryption for Entity Framework Core applications. It operates entirely through EF Core's `IModelFinalizingConvention` and `ValueConverter` pipeline — no raw SQL, no stored procedures, no middleware layer. Data is encrypted before it leaves the application boundary and decrypted on materialization, ensuring the database engine never processes plaintext.

> **Designed for formal compliance auditing.** Every cryptographic claim maps to verifiable source code with line-level references.

---

## Table of Contents

- [Installation](#installation)
- [Cryptographic Architecture](#cryptographic-architecture)
- [Encryption Tiers](#encryption-tiers)
- [Quick Start](#quick-start)
- [Schema Obfuscation](#schema-obfuscation)
- [Relationship Integrity (PK/FK)](#relationship-integrity-pkfk)
- [Searchable Encrypted Fields](#searchable-encrypted-fields)
- [Per-Entity Key Isolation](#per-entity-key-isolation)
- [Performance Characteristics](#performance-characteristics)
- [Compliance & Standards Mapping](#compliance--standards-mapping)
- [Supply Chain Security](#supply-chain-security)
- [Security Best Practices](#security-best-practices)

---

## Installation

```bash
dotnet add package EntityCrypt.EFCore
```

**Prerequisites:** .NET 10.0+ (required for native `System.Security.Cryptography.MLKem` support)

---

## Cryptographic Architecture

EntityCrypt implements a layered cryptographic pipeline:

```
┌─────────────────────────────────────────────────────────┐
│  Application Layer (EF Core DbContext)                  │
├─────────────────────────────────────────────────────────┤
│  Convention Layer — IModelFinalizingConvention           │
│  Applies ValueConverters + schema transforms per-entity │
├─────────────────────────────────────────────────────────┤
│  Value Encryption — AES-256-GCM per column              │
│  Random IV (RandomNumberGenerator.Fill) · HKDF-SHA256   │
├─────────────────────────────────────────────────────────┤
│  PQC Layer — ML-KEM-768 Key Encapsulation (FIPS 203)    │
│  Hybrid: AES-256 ⊕ ML-KEM shared secret via HKDF       │
├─────────────────────────────────────────────────────────┤
│  Schema Obfuscation — HMAC-SHA256 deterministic hashing │
│  Table names + column names → mc_<hash>                 │
├─────────────────────────────────────────────────────────┤
│  Key Management — Merkle Tree · CSPRNG · IDisposable    │
│  Memory zeroing · fingerprint-based vault derivation    │
├─────────────────────────────────────────────────────────┤
│  Integrity Layer — Merkle Tree proof/verify/consensus   │
└─────────────────────────────────────────────────────────┘
```

**Key design invariants:**

- **No IV reuse.** Every encryption operation generates a fresh nonce via `RandomNumberGenerator.Fill` (see `MatryoshkaValueCryptor.cs:77-78`). Deterministic mode uses HMAC-derived nonces for equality search (see `MatryoshkaValueCryptor.cs:127-128`).
- **No key material in plaintext at rest.** All derived keys pass through HKDF-SHA256 with domain-separation context strings.
- **Zero trust on the database.** The DBMS never receives plaintext column names, table names, or data values (when fully configured).

---

## Encryption Tiers

| Tier | Algorithm | Key Size | Quantum Resistance | Throughput |
|------|-----------|----------|--------------------|------------|
| `Classical` | AES-256-GCM | 256-bit | No (128-bit post-quantum) | ~750,000 ops/sec |
| `Hybrid` | AES-256-GCM ⊕ ML-KEM-768 | 256+768-bit | **Yes** (CNSA 2.0 compliant) | ~13,000 ops/sec |
| `PostQuantum` | ML-KEM-768 (FIPS 203) | 768-bit lattice | **Yes** | ~40,000 keygen/sec |

The `Hybrid` tier performs double-layer encryption: AES-256 first, then ML-KEM encapsulation with a second AES-256 pass using the ML-KEM shared secret derived through HKDF.

---

## Quick Start

### 1. Define Entities with Encryption Attributes

```csharp
using EntityCrypt.EFCore.Attributes;

[EncryptedTable(DefaultLevel = EncryptionLevel.Classical)]
public class Customer
{
    public int Id { get; set; }

    [Encrypted]
    public string FullName { get; set; }

    [Encrypted(Level = EncryptionLevel.Hybrid)]
    public string TaxId { get; set; }

    [Encrypted(Searchable = true)]
    public string Email { get; set; }

    [NoEncrypt]
    public DateTime CreatedUtc { get; set; }
}
```

### 2. Configure DbContext

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder options)
{
    options.UseSqlite("Data Source=app.db")
           .UseEntityCrypt(cfg => cfg
               .WithMasterKey(Environment.GetEnvironmentVariable("ENCRYPTION_MASTER_KEY"))
               .WithDefaultLevel(EncryptionLevel.Classical)
               .PreserveRelationships()
               .EncryptTableNames()
               .EncryptFieldNames()
           );
}
```

### 3. Use Normally — Encryption Is Transparent

```csharp
using var db = new AppDbContext();

db.Customers.Add(new Customer
{
    FullName = "Jane Doe",
    TaxId = "123-45-6789",
    Email = "jane@example.com",
    CreatedUtc = DateTime.UtcNow
});
await db.SaveChangesAsync();

// Reads are decrypted automatically
var customer = await db.Customers.FindAsync(1);
Console.WriteLine(customer.FullName); // "Jane Doe"
```

**What the database actually stores:**

```
Table: mc_7f3a9b (was "Customers")
┌──────────┬──────────────────────────────────┬─────────────────────────────────────────────┐
│ mc_a1b2  │ mc_d4e5                          │ mc_g7h8                                     │
│ (Id)     │ (FullName)                       │ (TaxId)                                     │
├──────────┼──────────────────────────────────┼─────────────────────────────────────────────┤
│ 1        │ ENC:AES256:iv:ciphertext:tag     │ ENC:HYBRID:mlkem_capsule:iv:ciphertext:tag  │
└──────────┴──────────────────────────────────┴─────────────────────────────────────────────┘
```

---

## Schema Obfuscation

When `EncryptTableNames()` and `EncryptFieldNames()` are enabled, all identifiers are transformed via HMAC-SHA256 with context-specific domain separation strings:

- Table names: `HMAC-SHA256(key, "table:" + originalName)` → `mc_<truncated-hex>`
- Column names: `HMAC-SHA256(key, "column:" + tableName + ":" + columnName)` → `mc_<truncated-hex>`

This prevents schema inference attacks where an adversary with database access reconstructs the data model from identifier naming conventions — a critical concern for Identity/auth tables where `Id`, `UserId`, `RoleId` expose relational structure.

**PK/FK column names are always obfuscated** (when `EncryptFieldNames` is enabled) regardless of the `PreserveRelationships` setting, which controls only value encryption.

---

## Relationship Integrity (PK/FK)

```csharp
cfg.PreserveRelationships()  // Default: true
```

When enabled, primary key and foreign key **values** remain in plaintext to preserve JOIN semantics and referential integrity constraints. However, PK/FK **column names** are still obfuscated to prevent schema structure leakage.

```csharp
cfg.PreserveRelationships(false)  // Full encryption — JOINs will not work at DB level
```

> **Security note:** Even with `PreserveRelationships(true)`, an attacker cannot identify which columns are primary/foreign keys from names alone — they appear as `mc_xxxx` like all other columns.

---

## Searchable Encrypted Fields

```csharp
[Encrypted(Searchable = true)]
public string Email { get; set; }
```

Searchable fields use a deterministic nonce derived via `HMAC-SHA256(key, plaintext)`, enabling equality comparison at the database level while maintaining semantic security for non-searchable columns.

```csharp
var user = await db.Users
    .Where(u => u.Email == "jane@example.com")
    .FirstOrDefaultAsync();
```

> **Trade-off:** Deterministic encryption reveals equality — two identical plaintexts produce identical ciphertexts. Use only for exact-match lookups on low-cardinality-sensitive data. For high-sensitivity fields (SSN, PAN), prefer non-searchable `[Encrypted]`.

---

## Per-Entity Key Isolation

```csharp
cfg.WithMasterKey(masterKey)
   .WithEntityKey<Customer>("customer-isolation-key")
   .WithEntityKey<PaymentInfo>("payment-isolation-key");
```

Each entity type can derive its encryption keys from a distinct root, providing cryptographic compartmentalization. Compromise of one entity's key material does not expose other entities.

---

## Performance Characteristics

Benchmarked on .NET 10 with `BenchmarkDotNet` (see [`tests/`](https://github.com/sbay-dev/EntityCrypt/tree/main/tests)):

| Operation | Classical (AES-256-GCM) | Hybrid (AES + ML-KEM) |
|-----------|------------------------|-----------------------|
| Encrypt | ~1.3 µs/field | ~30 µs/field |
| Decrypt | ~3.3 µs/field | ~76 µs/field |
| Throughput | ~750,000 ops/sec | ~13,000 ops/sec |
| PQC KeyGen | — | ~49 µs |
| PQC Encapsulate | — | ~25 µs |
| Memory/op | < 1 KB | ~1.5 KB |

---

## Compliance & Standards Mapping

Every compliance claim is traceable to the implementing source file and line range.

| Standard | Requirement | Implementation | Source |
|----------|-------------|----------------|--------|
| **FIPS 203** | Post-Quantum KEM | ML-KEM-768 via `System.Security.Cryptography.MLKem` | [`MatryoshkaPqc.cs:24-96`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaPqc.cs#L24-L96) |
| **FIPS 197** | AES Encryption | AES-256-GCM (primary), AES-256-CBC (legacy) | [`AES256EncryptionProvider.cs:12-157`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.Core/Encryption/AES256EncryptionProvider.cs#L12-L157) / [`MatryoshkaValueCryptor.cs:75-116`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaValueCryptor.cs#L75-L116) |
| **SP 800-57** | Key Management | CSPRNG key generation, HKDF derivation, `IDisposable` zeroing | [`SecureRandomGenerator.cs:22-79`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.Core/Encryption/SecureRandomGenerator.cs#L22-L79) / [`MerkleKeyTree.cs:54-60`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Security/MerkleKeyTree.cs#L54-L60) |
| **SP 800-131A** | Algorithm Transition | Hybrid Classical+PQC mode | [`HybridEncryptionProvider.cs:55-133`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.Core/Encryption/PostQuantum/HybridEncryptionProvider.cs#L55-L133) |
| **GDPR Art.32** | Technical Measures | Transparent encryption at rest | [`MatryoshkaValueCryptor.cs:75-116`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaValueCryptor.cs#L75-L116) |
| **GDPR Art.25** | Privacy by Design | Schema obfuscation, automatic encryption | [`MatryoshkaSchemaConvention.cs:25-57`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaSchemaConvention.cs#L25-L57) / [`SchemaEncryptor.cs:15-39`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaSchemaEncryptor.cs#L15-L39) |
| **PCI DSS 3.5** | PAN Encryption | AES-256 / Hybrid per-column encryption | [`MatryoshkaValueCryptor.cs:75-116`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaValueCryptor.cs#L75-L116) |
| **PCI DSS 6.2** | Secure Software | SBOM (CycloneDX) + SHA-256 checksums | [`security-audit.yml:129-158`](https://github.com/sbay-dev/EntityCrypt/blob/main/.github/workflows/security-audit.yml#L129-L158) |
| **HIPAA §164.312** | ePHI Encryption | AES-256/PQC encryption at rest | [`MatryoshkaValueCryptor.cs:75-116`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaValueCryptor.cs#L75-L116) |
| **HIPAA §164.312(c)** | Integrity Controls | Merkle Tree proof/verification | [`MerkleKeyTree.cs:210-254`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Security/MerkleKeyTree.cs#L210-L254) |
| **ISO 27001 A.10** | Cryptography Controls | Full encryption + key management lifecycle | [`MatryoshkaKeyVault.cs:28-72`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaKeyVault.cs#L28-L72) |
| **ISO 19790** | Cryptographic Modules | .NET BCL cryptography + ML-KEM | [`AES256EncryptionProvider.cs`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.Core/Encryption/AES256EncryptionProvider.cs) / [`MatryoshkaPqc.cs`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaPqc.cs) |
| **NCA ECC-1** | Essential Controls | Multi-layer encryption engine | [`MatryoshkaValueConverterFactory.cs:17-110`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.EFCore/Matryoshka/MatryoshkaValueConverterFactory.cs#L17-L110) |
| **SAMA CSF** | Crypto Controls | Multi-algorithm encryption | [`HybridEncryptionProvider.cs:55-133`](https://github.com/sbay-dev/EntityCrypt/blob/main/src/EntityCrypt.Core/Encryption/PostQuantum/HybridEncryptionProvider.cs#L55-L133) |

---

## Supply Chain Security

### Vulnerability Scanning

```bash
dotnet list package --vulnerable --include-transitive
```

### Package Integrity Verification (SHA-256)

Every release publishes `SHA256SUMS.txt` on the [Releases page](https://github.com/sbay-dev/EntityCrypt/releases):

```powershell
# PowerShell
Get-FileHash EntityCrypt.EFCore.*.nupkg -Algorithm SHA256
```

```bash
# Linux/macOS
sha256sum EntityCrypt.EFCore.*.nupkg
```

### Software Bill of Materials (SBOM)

A [CycloneDX](https://cyclonedx.org/) SBOM is generated automatically with every release and attached to the GitHub Release page. Use it for supply chain auditing and dependency risk assessment.

### CI/CD Security Gates

| Gate | Description |
|------|-------------|
| Vulnerability Scan | Automated on every PR and push — blocks on high/critical CVEs |
| Test Suite | 70+ unit and integration tests with code coverage |
| Static Analysis | .NET Roslyn Analyzers at `latest-recommended` level |
| SBOM | CycloneDX generated per release |
| Checksums | SHA-256 for every published package |
| Pre-publish Gate | Mandatory vulnerability scan before NuGet publish |
| Scheduled Audit | Weekly security scan with automatic issue creation |

---

## Security Best Practices

### Key Management

```csharp
// ✗ Never hardcode keys
cfg.WithMasterKey("hardcoded-key");

// ✓ Use environment variables or secret managers
cfg.WithMasterKey(Environment.GetEnvironmentVariable("ENCRYPTION_MASTER_KEY"));

// ✓ Use Azure Key Vault / AWS KMS / HashiCorp Vault
cfg.WithKeyProvider(new AzureKeyVaultProvider(vaultUri));
```

### Tier Selection Guidelines

| Data Classification | Recommended Tier | Rationale |
|---------------------|-----------------|-----------|
| General PII (name, address) | `Classical` | AES-256-GCM provides sufficient protection; minimal latency |
| Financial (PAN, account numbers) | `Hybrid` | Quantum-resistant; meets CNSA 2.0 guidance |
| National security / long-term secrets | `Hybrid` or `PostQuantum` | Protection against harvest-now-decrypt-later attacks |
| Non-sensitive metadata | `[NoEncrypt]` | Avoid unnecessary cryptographic overhead |

### Operational Recommendations

1. **Rotate master keys periodically** — implement key versioning with HKDF domain separation
2. **Enable schema obfuscation** in production — prevents metadata leakage from column/table naming conventions
3. **Monitor `IDisposable` disposal** — all cryptographic types implement deterministic cleanup with memory zeroing
4. **Review SBOM per release** — verify no transitive dependencies introduce known vulnerabilities
5. **Use `Searchable = true` sparingly** — deterministic encryption reveals equality; limit to exact-match lookup fields

---

<div align="center">

**EntityCrypt.EFCore** · Transparent Quantum-Resistant Encryption for EF Core

[Documentation](https://sbay-dev.github.io/EntityCrypt) · [Source](https://github.com/sbay-dev/EntityCrypt) · [NuGet](https://www.nuget.org/packages/EntityCrypt.EFCore)

MIT License · .NET 10 · AES-256-GCM · ML-KEM-768 · FIPS 203

</div>
