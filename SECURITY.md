# Security Model

## Threat Model

EntityCrypt is designed to protect **data at rest** in scenarios where the database storage layer is untrusted. The following threat model defines what EntityCrypt protects against and what falls outside its scope.

### In Scope

| Threat | Mitigation |
|--------|------------|
| **Database breach** — attacker obtains raw database files or backup | All data values are encrypted; column/table names are obfuscated |
| **Schema inference** — attacker deduces data model from identifiers | HMAC-SHA256 schema obfuscation renders all identifiers opaque |
| **Key relationship discovery** — attacker identifies PK/FK columns | PK/FK column names are obfuscated like all other columns |
| **Harvest-now-decrypt-later** — attacker stores ciphertext for future quantum decryption | Hybrid mode (AES-256 + ML-KEM-768) provides quantum resistance |
| **Ciphertext tampering** — attacker modifies encrypted data | AES-256-GCM authentication tag detects any modification |
| **IV reuse** — nonce reuse enables cryptanalysis | CSPRNG generates fresh IV for every operation; deterministic mode uses HMAC-derived nonces |
| **Key material leakage** — sensitive key data persists in memory | IDisposable pattern with explicit memory zeroing across 7 classes |
| **Supply chain attack** — compromised dependency | SBOM per release, SHA-256 checksums, automated vulnerability scanning |

### Out of Scope

| Threat | Rationale |
|--------|-----------|
| **Application-layer compromise** — attacker controls the running application | If the application is compromised, the attacker has access to decrypted data in memory |
| **Side-channel attacks** — timing, power analysis | Mitigated by .NET BCL implementations, not by EntityCrypt directly |
| **Key management infrastructure** — vault compromise, HSM attacks | EntityCrypt provides `IEncryptionKeyProvider` interface; infrastructure security is the operator's responsibility |
| **SQL injection** — application-level query manipulation | EntityCrypt operates within EF Core; SQL injection is an application/framework concern |
| **Transport encryption** — data in transit | TLS/mTLS configuration is an infrastructure concern |

---

## Cryptographic Design Invariants

### 1. No Plaintext Processing by Database Engine

When fully configured (schema obfuscation + field encryption), the database engine processes only:
- Obfuscated table names (`mc_<hex>`)
- Obfuscated column names (`mc_<hex>`)
- Encrypted data values (`ENC:AES256:...`)
- Optionally: plaintext PK/FK values (when `PreserveRelationships = true`)

### 2. Key Derivation Hierarchy

```
Master Key (never used directly for encryption)
    │
    └─── HKDF-SHA256("entity:" + type) ──► Entity Key
              │
              ├─── HKDF-SHA256("column:" + name) ──► Column Key
              │
              └─── HKDF-SHA256("schema:" + name) ──► Schema Key
```

Each derivation step uses a unique context string for domain separation. Compromise of a derived key does not reveal the master key or sibling derived keys.

### 3. Algorithm Composition in Hybrid Mode

```
Plaintext
    │
    ├── [1] AES-256-GCM(random_key₁) ──► Ciphertext₁
    │
    ├── [2] ML-KEM-768.Encapsulate(pk) ──► (SharedSecret, Capsule)
    │
    ├── [3] HKDF-SHA256(SharedSecret) ──► random_key₂
    │
    └── [4] AES-256-GCM(random_key₂, Ciphertext₁) ──► Ciphertext₂
    
Output: Capsule ‖ Ciphertext₂
```

Both encryption layers must be broken to recover plaintext. An attacker needs both the AES key AND the ML-KEM private key.

### 4. CSPRNG Exclusivity

All random values are generated via `System.Security.Cryptography.RandomNumberGenerator`:
- Encryption keys: `RandomNumberGenerator.GetBytes(32)`
- Initialization vectors: `RandomNumberGenerator.Fill(span)` (12 bytes for GCM)
- Salts: `RandomNumberGenerator.GetBytes(configurable)`
- No `System.Random`, no `DateTime`-seeded generators, no predictable sources

### 5. Authenticated Encryption

AES-256-GCM provides:
- **Confidentiality** — plaintext is hidden
- **Integrity** — any modification to ciphertext is detected
- **Authentication** — only holders of the key can produce valid ciphertext

The 16-byte GCM authentication tag is verified before any decryption output is produced. Failed verification throws a `CryptographicException`.

---

## Searchable Encryption Security Analysis

Fields marked `[Encrypted(Searchable = true)]` use deterministic encryption:

| Property | Value |
|----------|-------|
| **Nonce derivation** | `HMAC-SHA256(key, plaintext)` truncated to 12 bytes |
| **Determinism** | Same plaintext → same ciphertext (required for equality queries) |
| **Frequency analysis** | Possible — attacker can observe value distribution |
| **Chosen-plaintext** | Attacker can verify guesses by comparing ciphertexts |

**Guidance:**
- Use searchable encryption **only** for exact-match lookup fields
- Avoid on high-sensitivity data (SSN, PAN, medical IDs)
- Suitable for: email lookups, username searches, phone number verification
- Not suitable for: free-text fields, rare identifiers, enumerable values

---

## Operational Security Recommendations

### Key Management

1. **Never hardcode master keys** — use environment variables, Azure Key Vault, AWS KMS, or HashiCorp Vault
2. **Rotate keys periodically** — implement key versioning via `IEncryptionKeyProvider`
3. **Separate keys per environment** — development, staging, and production must use distinct keys
4. **Audit key access** — log all key retrieval operations in your key management system

### Configuration

1. **Enable schema obfuscation in production** — prevents metadata leakage
2. **Use Hybrid mode for long-term secrets** — protects against quantum computing threats
3. **Minimize searchable fields** — each searchable field is a trade-off between functionality and security
4. **Review `PreserveRelationships` setting** — understand that preserved PK/FK values are plaintext in the database

### Monitoring

1. **Monitor for decryption failures** — `CryptographicException` may indicate tampering
2. **Track encryption performance** — sudden latency changes may indicate key/configuration issues
3. **Review SBOM per release** — verify no new transitive dependencies introduce vulnerabilities
4. **Run `dotnet list package --vulnerable`** regularly in consuming projects

### Incident Response

1. **Key compromise** — rotate master key immediately; re-encrypt all data with new key
2. **Database breach** — encrypted data is protected; assess if PK/FK values (if preserved) expose sensitive relationships
3. **Algorithm deprecation** — migrate affected columns to a stronger tier using per-column `EncryptionLevel` configuration

---

## Responsible Disclosure

If you discover a security vulnerability in EntityCrypt, please report it responsibly via the [NuGet package page](https://www.nuget.org/packages/EntityCrypt.EFCore) contact information. Do not open public GitHub issues for security vulnerabilities.
