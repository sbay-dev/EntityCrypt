<div align="center">

# EntityCrypt

**Transparent Quantum-Resistant Encryption for Entity Framework Core**

[![NuGet](https://img.shields.io/nuget/v/EntityCrypt.EFCore?style=flat-square)](https://www.nuget.org/packages/EntityCrypt.EFCore)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)
[![FIPS 203](https://img.shields.io/badge/FIPS_203-ML--KEM--768-00a86b?style=flat-square)](https://csrc.nist.gov/pubs/fips/203/final)

AES-256-GCM · ML-KEM-768 · Hybrid PQC · Schema Obfuscation · Merkle Integrity

[Documentation](https://sbay-dev.github.io/EntityCrypt) · [NuGet Package](https://www.nuget.org/packages/EntityCrypt.EFCore) · [Compliance Map](COMPLIANCE.md) · [Architecture](ARCHITECTURE.md)

</div>

---

## Overview

EntityCrypt provides **zero-code-change**, column-level and schema-level encryption for Entity Framework Core applications. It operates entirely through EF Core's `IModelFinalizingConvention` and `ValueConverter` pipeline — no raw SQL, no stored procedures, no middleware. Data is encrypted before it reaches the database engine and decrypted transparently on materialization.

```csharp
// That's it. Your data is now encrypted at rest.
options.UseSqlite("Data Source=app.db")
       .UseEntityCrypt(cfg => cfg
           .WithMasterKey(Environment.GetEnvironmentVariable("MASTER_KEY"))
           .WithDefaultLevel(EncryptionLevel.Hybrid)
           .EncryptTableNames()
           .EncryptFieldNames()
       );
```

### What the database sees

```
Table: mc_7f3a9b  (was "Customers")
Column: mc_a1b2   → 1                                    (PK value preserved for JOINs)
Column: mc_d4e5   → ENC:AES256:iv:ciphertext:tag         (encrypted)
Column: mc_g7h8   → ENC:HYBRID:capsule:iv:ciphertext:tag (quantum-resistant)
```

---

## Key Capabilities

| Capability | Description |
|------------|-------------|
| **AES-256-GCM** | FIPS 197 compliant symmetric encryption with authenticated encryption |
| **ML-KEM-768** | NIST FIPS 203 post-quantum key encapsulation (native .NET 10 `MLKem`) |
| **Hybrid Mode** | AES-256 ⊕ ML-KEM shared secret via HKDF — quantum-safe with classical fallback |
| **Schema Obfuscation** | HMAC-SHA256 deterministic hashing of table and column names |
| **Merkle Integrity** | Tree-based proof generation, verification, and consensus checking |
| **Searchable Encryption** | Deterministic nonce mode for equality queries on encrypted fields |
| **Per-Entity Key Isolation** | Cryptographic compartmentalization via entity-specific key derivation |
| **13+ CLR Types** | ValueConverter support for string, int, decimal, DateTime, Guid, bool, and more |
| **IDisposable Cleanup** | Deterministic memory zeroing across all cryptographic types |
| **CSPRNG Throughout** | `RandomNumberGenerator.Fill` for all IVs, keys, and salts — no PRNG |

---

## Encryption Tiers

| Tier | Algorithm | Quantum Resistant | Throughput |
|------|-----------|-------------------|------------|
| `Classical` | AES-256-GCM | No (128-bit PQ security) | ~750,000 ops/sec |
| `Hybrid` | AES-256-GCM ⊕ ML-KEM-768 | **Yes** (CNSA 2.0) | ~13,000 ops/sec |
| `PostQuantum` | ML-KEM-768 (FIPS 203) | **Yes** | ~40,000 keygen/sec |

---

## Quick Start

### Install

```bash
dotnet add package EntityCrypt.EFCore
```

> **Requires** .NET 10.0+ for native `System.Security.Cryptography.MLKem`

### Define Entities

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

### Configure

```csharp
options.UseSqlite("Data Source=app.db")
       .UseEntityCrypt(cfg => cfg
           .WithMasterKey(Environment.GetEnvironmentVariable("ENCRYPTION_MASTER_KEY"))
           .WithDefaultLevel(EncryptionLevel.Classical)
           .PreserveRelationships()
           .EncryptTableNames()
           .EncryptFieldNames()
       );
```

### Use Normally

```csharp
// Encryption and decryption are fully transparent
db.Customers.Add(new Customer { FullName = "Jane Doe", TaxId = "123-45-6789" });
await db.SaveChangesAsync();

var customer = await db.Customers.FindAsync(1);
Console.WriteLine(customer.FullName); // "Jane Doe" — decrypted automatically
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [**Architecture**](ARCHITECTURE.md) | Cryptographic pipeline, layered design, key management model |
| [**Compliance**](COMPLIANCE.md) | Standards mapping — NIST, NCA, GDPR, PCI DSS, HIPAA, ISO |
| [**Security Model**](SECURITY.md) | Threat model, design invariants, operational guidance |
| [**Consumer Integration**](docs/CONSUMER_INTEGRATION.md) | Step-by-step integration guide for downstream projects |
| [**NuGet README**](src/EntityCrypt.EFCore/README.md) | Package-level documentation with full API reference |

---

## Compliance Standards

EntityCrypt is designed to support compliance with the following frameworks. See [COMPLIANCE.md](COMPLIANCE.md) for detailed traceability.

| Framework | Controls |
|-----------|----------|
| **NIST** | FIPS 203 (ML-KEM), FIPS 197 (AES), SP 800-57, SP 800-131A, SP 800-175B |
| **NCA (Saudi Arabia)** | ECC-1, CCC-1, DCC, CTFC, NCS |
| **GDPR / PDPL** | Article 32, Article 25, Saudi PDPL encryption requirements |
| **PCI DSS 4.0** | Requirements 3, 3.5, 6.2, 12 |
| **HIPAA** | §164.312(a), §164.312(c), §164.312(e) |
| **ISO/IEC** | 27001, 27018, 19790, 11770 |
| **SAMA CSF** | Cryptographic controls for financial institutions |
| **SOC 2** | Data protection and audit trail controls |

---

## Supply Chain Security

- **Vulnerability Scanning** — automated on every PR/push, blocks on high/critical CVEs
- **SHA-256 Checksums** — published with every release for package integrity verification
- **SBOM (CycloneDX)** — generated per release for supply chain auditing
- **Static Analysis** — .NET Roslyn Analyzers at `latest-recommended`
- **Pre-publish Security Gate** — mandatory vulnerability scan before NuGet publish
- **Weekly Scheduled Audit** — automated security scan with issue creation

---

## Repository Structure

This is the **public documentation and reference API repository**. It contains the public API surface, architecture documentation, and compliance mapping for EntityCrypt. The full implementation source is maintained in a separate private repository and is available to authorized auditors under agreement.

```
├── ARCHITECTURE.md          # Cryptographic architecture documentation
├── COMPLIANCE.md            # Compliance standards traceability matrix
├── SECURITY.md              # Security model and threat analysis
├── docs/
│   ├── index.html           # Landing page (GitHub Pages)
│   └── *.md                 # Integration and publishing guides
└── src/
    ├── EntityCrypt.Core/    # Core library public API (attributes, models)
    └── EntityCrypt.EFCore/  # EF Core integration public API (config, extensions, attributes)
```

---

<div align="center">

**EntityCrypt** · .NET 10 · AES-256-GCM · ML-KEM-768 · FIPS 203

MIT License

</div>
