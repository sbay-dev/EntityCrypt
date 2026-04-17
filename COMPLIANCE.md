# Compliance Traceability Matrix

This document maps each compliance standard to the specific EntityCrypt capability that satisfies the requirement. For formal auditing, implementation evidence is available under audit agreement with access to the private source repository.

> **Note:** This library implements cryptographic controls that *support* compliance with the listed standards. Achieving full compliance requires organizational policies, operational procedures, and system-level configurations beyond the scope of any single library.

---

## NIST (National Institute of Standards and Technology)

### FIPS 203 — Module-Lattice-Based Key-Encapsulation Mechanism (ML-KEM)

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| ML-KEM-768 key generation | Key pair generation using .NET 10 native `MLKemAlgorithm.MLKem768` |
| Key encapsulation | `Encapsulate()` produces shared secret + ciphertext capsule |
| Key decapsulation | `Decapsulate()` recovers shared secret from capsule + private key |
| Hybrid key derivation | HKDF-SHA256 combines ML-KEM shared secret with classical key material |

**API Surface:** `EncryptionLevel.PostQuantum`, `EncryptionLevel.Hybrid`

### FIPS 197 — Advanced Encryption Standard (AES)

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| AES-256 encryption | AES-256-GCM authenticated encryption (primary mode) |
| AES-256-CBC (legacy) | CBC mode available via Core encryption provider |
| Key size | 256-bit keys derived via HKDF-SHA256 from master key |
| Block cipher mode | GCM with 12-byte nonce and 16-byte authentication tag |

**API Surface:** `EncryptionLevel.Classical`, `EncryptionLevel.Hybrid`

### SP 800-57 — Recommendation for Key Management

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| Key generation | CSPRNG via `RandomNumberGenerator.GetBytes` / `.Fill` |
| Key derivation | HKDF-SHA256 with domain-separation context strings |
| Key storage | Master key from environment/vault; derived keys never persisted |
| Key destruction | `IDisposable` pattern with explicit memory zeroing (7 classes) |
| Key separation | Per-entity and per-column key derivation via HKDF context |

**API Surface:** `EncryptionOptionsBuilder.WithMasterKey()`, `IEncryptionKeyProvider`

### SP 800-131A Rev.2 — Transitioning Cryptographic Algorithms

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| Algorithm agility | Three-tier system: Classical, Hybrid, PostQuantum |
| Migration path | Per-column encryption level selection; mixed tiers supported |
| PQC readiness | ML-KEM-768 hybrid mode for harvest-now-decrypt-later protection |

**API Surface:** `EncryptionLevel` enum, `[Encrypted(Level = ...)]`

### SP 800-175B — Guideline for Using Cryptographic Standards

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| Approved algorithms | AES-256 (FIPS 197), ML-KEM-768 (FIPS 203) |
| Random number generation | `System.Security.Cryptography.RandomNumberGenerator` (CSPRNG) |
| Authenticated encryption | AES-256-GCM with authentication tag verification |

---

## NCA — National Cybersecurity Authority (Saudi Arabia)

### ECC-1:2018 — Essential Cybersecurity Controls

| Control | EntityCrypt Implementation |
|---------|---------------------------|
| Cryptographic controls | Multi-layer encryption engine with configurable tiers |
| Data protection at rest | Transparent column-level encryption via EF Core pipeline |
| Key management | HKDF-based key hierarchy with CSPRNG generation |

### CCC-1:2019 — Critical Systems Cybersecurity Controls

| Control | EntityCrypt Implementation |
|---------|---------------------------|
| Enhanced encryption | Hybrid AES-256 + ML-KEM-768 for critical data |
| Algorithm strength | Quantum-resistant encryption available for all data tiers |

### DCC — Data Cybersecurity Controls

| Control | EntityCrypt Implementation |
|---------|---------------------------|
| Data classification support | Three encryption levels map to data sensitivity tiers |
| Schema protection | HMAC-SHA256 schema obfuscation prevents metadata leakage |

### CTFC — Cloud Technology Framework Controls

| Control | EntityCrypt Implementation |
|---------|---------------------------|
| Encryption in cloud environments | Database-agnostic; works with any EF Core provider |
| Key isolation | Per-entity key derivation; vault integration via `IEncryptionKeyProvider` |

### NCS — National Cryptography Standards

| Control | EntityCrypt Implementation |
|---------|---------------------------|
| Approved algorithms | AES-256, ML-KEM-768 (NIST approved) |
| Key management compliance | SP 800-57 aligned key lifecycle |

---

## GDPR (EU) / PDPL (Saudi Arabia)

### Article 32 GDPR — Security of Processing

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| Appropriate technical measures | AES-256-GCM encryption for all personal data at rest |
| State of the art | ML-KEM-768 post-quantum cryptography |
| Pseudonymization | Schema obfuscation renders data unidentifiable without keys |

### Article 25 GDPR — Data Protection by Design and by Default

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| By design | `IModelFinalizingConvention` applies encryption automatically during model build |
| By default | `[EncryptedTable]` encrypts all columns by default; opt-out via `[NoEncrypt]` |
| Minimization support | Schema obfuscation hides even the structure of personal data |

### Saudi PDPL — Personal Data Protection Law

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| Encryption of personal data | Full column-level encryption with schema obfuscation |
| Technical safeguards | Multi-layer cryptographic pipeline |

---

## PCI DSS 4.0 — Payment Card Industry Data Security Standard

### Requirement 3 — Protect Stored Account Data

| Sub-requirement | EntityCrypt Implementation |
|-----------------|---------------------------|
| 3.5 — PAN encryption | AES-256-GCM or Hybrid per-column encryption for cardholder data |
| 3.5.1 — Strong cryptography | 256-bit keys, authenticated encryption, CSPRNG |
| 3.6 — Key management | HKDF-based derivation, `IDisposable` cleanup, vault integration |

### Requirement 6 — Develop and Maintain Secure Systems

| Sub-requirement | EntityCrypt Implementation |
|-----------------|---------------------------|
| 6.2 — Secure software | SBOM (CycloneDX) generated per release; SHA-256 checksums published |
| 6.3 — Vulnerability management | Automated scanning in CI/CD; weekly scheduled audits |

---

## HIPAA — Health Insurance Portability and Accountability Act

### §164.312 — Technical Safeguards

| Provision | EntityCrypt Implementation |
|-----------|---------------------------|
| §164.312(a)(2)(iv) — Encryption | AES-256-GCM / Hybrid encryption for ePHI at rest |
| §164.312(e)(2)(ii) — Transmission encryption | Encryption applied before data leaves application boundary |
| §164.312(c)(1) — Integrity controls | Merkle Tree proof generation and verification |
| §164.312(d) — Authentication | HMAC-SHA256 schema verification; GCM authentication tags |

---

## ISO/IEC International Standards

### ISO/IEC 27001 — Information Security Management System (ISMS)

| Control | EntityCrypt Implementation |
|---------|---------------------------|
| A.10 — Cryptography | AES-256-GCM, ML-KEM-768, HKDF key management |
| A.10.1.1 — Cryptographic controls policy | Configurable encryption levels per entity/column |
| A.10.1.2 — Key management | CSPRNG generation, HKDF derivation, `IDisposable` zeroing |

### ISO/IEC 27018 — PII Protection in Public Cloud

| Control | EntityCrypt Implementation |
|---------|---------------------------|
| PII encryption | Transparent encryption for all PII fields |
| Schema protection | HMAC-SHA256 obfuscation prevents PII identification from schema |

### ISO/IEC 19790 — Security Requirements for Cryptographic Modules

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| Approved algorithms | .NET BCL cryptographic primitives (AES, SHA-256, HKDF, ML-KEM) |
| Key management | Secure generation, derivation, and destruction lifecycle |

### ISO/IEC 11770 — Key Management

| Part | EntityCrypt Implementation |
|------|---------------------------|
| Key establishment | HKDF-SHA256 with domain separation |
| Key lifecycle | Generation → Derivation → Use → Destruction (IDisposable) |
| Merkle Tree | Integrity verification of key hierarchy |

---

## Additional Frameworks

### SOC 2 — Service Organization Control

| Criteria | EntityCrypt Implementation |
|----------|---------------------------|
| Data protection | Encryption + schema obfuscation |
| Audit trail | CI/CD security gate logs, SBOM per release |
| Change management | Automated vulnerability scanning on every change |

### SAMA CSF — Saudi Arabian Monetary Authority Cyber Security Framework

| Domain | EntityCrypt Implementation |
|--------|---------------------------|
| Cryptographic controls | Multi-algorithm encryption (AES-256, ML-KEM-768, Hybrid) |
| Data protection | Column-level and schema-level encryption |

### CCPA/CPRA — California Consumer Privacy Act

| Requirement | EntityCrypt Implementation |
|-------------|---------------------------|
| Encryption of personal information | AES-256-GCM column-level encryption |
| Pseudonymization | Schema obfuscation renders data non-identifiable |

---

## Audit Access

For formal compliance auditing requiring line-level source code evidence, authorized auditors may request access to the private implementation repository under a non-disclosure agreement. Contact the package owner via the [NuGet package page](https://www.nuget.org/packages/EntityCrypt.EFCore).
