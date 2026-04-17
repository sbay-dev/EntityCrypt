# Architecture

## Cryptographic Pipeline

EntityCrypt implements a six-layer cryptographic pipeline that operates within the EF Core model finalization lifecycle. Each layer is independent and can be configured or disabled without affecting the others.

```
┌──────────────────────────────────────────────────────────────┐
│  Layer 1 — Application Interface                             │
│  EF Core DbContext · DbSet<T> · LINQ Queries                │
│  No application code changes required                        │
├──────────────────────────────────────────────────────────────┤
│  Layer 2 — Convention Engine                                 │
│  IModelFinalizingConvention                                  │
│  Applies ValueConverters and schema transforms per-entity    │
│  Respects [Encrypted], [NoEncrypt], [EncryptedTable] attrs   │
├──────────────────────────────────────────────────────────────┤
│  Layer 3 — Value Encryption                                  │
│  AES-256-GCM per column · Authenticated encryption           │
│  Random IV via RandomNumberGenerator.Fill (no reuse)         │
│  HKDF-SHA256 key derivation with domain separation           │
├──────────────────────────────────────────────────────────────┤
│  Layer 4 — Post-Quantum Layer                                │
│  ML-KEM-768 Key Encapsulation (FIPS 203)                     │
│  Hybrid mode: AES-256 ⊕ ML-KEM shared secret via HKDF       │
│  Native .NET 10 System.Security.Cryptography.MLKem           │
├──────────────────────────────────────────────────────────────┤
│  Layer 5 — Schema Obfuscation                                │
│  HMAC-SHA256 deterministic hashing                           │
│  Table names: HMAC(key, "table:" + name) → mc_<hex>          │
│  Column names: HMAC(key, "column:" + table + ":" + col)      │
│  Prevents schema inference attacks                           │
├──────────────────────────────────────────────────────────────┤
│  Layer 6 — Key Management & Integrity                        │
│  Merkle Tree · CSPRNG · IDisposable memory zeroing           │
│  Fingerprint-based vault derivation                          │
│  Proof generation, verification, and consensus               │
└──────────────────────────────────────────────────────────────┘
```

## Design Invariants

These invariants hold across all configurations and are enforced by the implementation:

### 1. No IV Reuse
Every encryption operation generates a fresh nonce via `RandomNumberGenerator.Fill`. The deterministic mode (for searchable fields) uses HMAC-derived nonces — never timestamp or counter-based.

### 2. No Plaintext Key Material at Rest
All derived keys pass through HKDF-SHA256 with domain-separation context strings. Master keys are never used directly for data encryption.

### 3. Zero Trust on the Database
When fully configured (schema obfuscation enabled), the database engine never processes:
- Plaintext column names
- Plaintext table names
- Plaintext data values (except preserved PK/FK values for JOIN semantics)

### 4. Authenticated Encryption
AES-256-GCM provides both confidentiality and integrity. Tampering with ciphertext is detected before any plaintext is returned.

### 5. Deterministic Memory Cleanup
All cryptographic types implement `IDisposable` with explicit memory zeroing of sensitive buffers. Seven classes enforce this pattern.

## Value Encryption Flow

```
                    ┌─────────────────┐
                    │  Plaintext Value │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │  Type Detection  │
                    │  (13+ CLR types) │
                    └────────┬────────┘
                             │
              ┌──────────────┼──────────────┐
              │              │              │
     ┌────────▼───┐  ┌──────▼─────┐  ┌────▼────────┐
     │  Classical  │  │   Hybrid   │  │ PostQuantum  │
     │  AES-256   │  │ AES + MLKEM│  │  ML-KEM-768  │
     └────────┬───┘  └──────┬─────┘  └────┬────────┘
              │              │              │
              └──────────────┼──────────────┘
                             │
                    ┌────────▼────────┐
                    │   Prefix Tag +  │
                    │   IV + Cipher   │
                    │   + Auth Tag    │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │  Database Column │
                    │  (obfuscated)   │
                    └─────────────────┘
```

## Hybrid Encryption Detail

The Hybrid tier provides quantum-resistance while maintaining classical-grade performance:

1. **First pass:** Encrypt plaintext with AES-256-GCM using a random key
2. **ML-KEM Encapsulation:** Generate shared secret via ML-KEM-768 `Encapsulate`
3. **Key Derivation:** Derive a second AES key from ML-KEM shared secret via HKDF-SHA256
4. **Second pass:** Encrypt the first-pass ciphertext with the ML-KEM-derived key
5. **Output:** ML-KEM capsule + double-encrypted ciphertext

Decryption reverses the process using ML-KEM `Decapsulate` to recover the shared secret.

## Key Management Model

```
  Master Key (environment/vault)
       │
       ├── HKDF("entity:" + EntityType) ──► Entity-specific key
       │       │
       │       ├── HKDF("column:" + name) ──► Column-specific key
       │       │
       │       └── HKDF("schema:" + name) ──► Schema obfuscation key
       │
       ├── CSPRNG ──► Per-operation IV (12 bytes for GCM)
       │
       └── Merkle Tree
               ├── Genesis key derivation
               ├── Per-database registration
               ├── Proof generation
               └── Consensus verification
```

## Schema Obfuscation

Schema obfuscation transforms all database identifiers using HMAC-SHA256:

- **Table names:** `HMAC-SHA256(key, "table:" + originalName)` → `mc_<truncated-hex>`
- **Column names:** `HMAC-SHA256(key, "column:" + tableName + ":" + columnName)` → `mc_<truncated-hex>`

This is **deterministic** — the same input always produces the same output — enabling EF Core's model metadata to map between application names and database names. But it is **irreversible** without the key, preventing schema inference attacks.

### PK/FK Handling

- **Column names** of PK/FK are always obfuscated (when schema obfuscation is enabled)
- **Column values** of PK/FK can be preserved (`PreserveRelationships = true`) for JOIN semantics
- An attacker with database access cannot identify which obfuscated columns are primary/foreign keys

## ValueConverter Architecture

EntityCrypt uses EF Core's `ValueConverter<TModel, TProvider>` pipeline to intercept all read/write operations:

```
  Application (C# types)              Database (encrypted strings)
  ═══════════════════════              ═══════════════════════════
  string "Jane Doe"           ──►     "ENC:AES256:iv:cipher:tag"
  int 42                      ──►     "ENC:AES256:iv:cipher:tag"
  decimal 99.95               ──►     "ENC:AES256:iv:cipher:tag"
  DateTime 2025-01-01         ──►     "ENC:AES256:iv:cipher:tag"
  Guid {abc-123}              ──►     "ENC:AES256:iv:cipher:tag"
  bool true                   ──►     "ENC:AES256:iv:cipher:tag"
```

The `ValueConverterFactory` supports 13+ CLR types with both randomized and deterministic encryption modes.

## Searchable Encryption

For fields marked `[Encrypted(Searchable = true)]`:

1. A deterministic nonce is derived: `HMAC-SHA256(key, plaintext)` → truncated to nonce size
2. AES-256-GCM encrypts with this deterministic nonce
3. **Result:** Identical plaintexts produce identical ciphertexts
4. **Trade-off:** Reveals equality (two equal values → equal ciphertexts)
5. **Use case:** Exact-match lookups (`WHERE Email = @param`)

> Searchable encryption should be used sparingly on low-sensitivity equality-lookup fields only.

## Merkle Integrity Layer

The Merkle Key Tree provides:

- **Genesis Key Derivation** — deterministic root key from master key
- **Database Registration** — per-database key isolation
- **Tree Rebuilding** — reconstruct integrity state from persisted data
- **Proof Generation** — cryptographic proof that a specific key exists in the tree
- **Proof Verification** — validate proofs without accessing the full tree
- **Consensus Checking** — multi-party agreement on tree state

This enables integrity verification of the key hierarchy independent of the database.
