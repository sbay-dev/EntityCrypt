using EntityCrypt.Core.Encryption;
using EntityCrypt.EFCore.Attributes;

namespace EntityCrypt.EFCore.Configuration;

/// <summary>
/// Encryption configuration options
/// </summary>
public sealed class EncryptionOptions
{
    /// <summary>Default encryption level</summary>
    public EncryptionLevel DefaultLevel { get; set; } = EncryptionLevel.Classical;
    
    /// <summary>Master encryption key</summary>
    public string MasterKey { get; set; } = string.Empty;
    
    /// <summary>هل يتم تشفير أسماء الجداول؟</summary>
    public bool EncryptTableNames { get; set; } = true;
    
    /// <summary>هل يتم تشفير أسماء الحقول؟</summary>
    public bool EncryptFieldNames { get; set; } = true;
    
    /// <summary>هل يتم الحفاظ على العلاقات (PK/FK)؟</summary>
    public bool PreserveRelationships { get; set; } = true;
    
    /// <summary>هل يتم تشفير المفاتيح الأساسية؟</summary>
    public bool EncryptPrimaryKeys { get; set; } = false;
    
    /// <summary>Entities excluded from encryption</summary>
    public HashSet<Type> ExcludedEntities { get; } = new();
    
    /// <summary>Properties excluded from encryption (format: EntityType.PropertyName)</summary>
    public HashSet<string> ExcludedProperties { get; } = new();
    
    /// <summary>Custom encryption keys for specific entity types</summary>
    public Dictionary<Type, string> EntityKeys { get; } = new();
    
    /// <summary>Encryption key provider</summary>
    public IEncryptionKeyProvider? KeyProvider { get; set; }
}

/// <summary>
/// واجهة Encryption key provider
/// </summary>
public interface IEncryptionKeyProvider
{
    /// <summary>Resolves encryption key for a specific entity type</summary>
    string GetKeyForEntity(Type entityType);
    
    /// <summary>Resolves encryption key for a specific property</summary>
    string GetKeyForProperty(Type entityType, string propertyName);
    
    /// <summary>Returns the master encryption key</summary>
    string GetMasterKey();
    
    /// <summary>Returns ML-KEM-768 key pair if available</summary>
    (byte[] PublicKey, byte[] PrivateKey)? GetPqcKeyPair();
}

/// <summary>
/// Default key provider implementation
/// </summary>
public sealed class DefaultKeyProvider : IEncryptionKeyProvider
{
    private readonly string _masterKey;
    private readonly Dictionary<Type, string> _entityKeys;
    private (byte[] PublicKey, byte[] PrivateKey)? _pqcKeyPair;

    public DefaultKeyProvider(string masterKey, Dictionary<Type, string>? entityKeys = null)
    {
        _masterKey = masterKey;
        _entityKeys = entityKeys ?? new();
    }

    public string GetKeyForEntity(Type entityType)
    {
        return _entityKeys.TryGetValue(entityType, out var key) ? key : _masterKey;
    }

    public string GetKeyForProperty(Type entityType, string propertyName)
    {
        return GetKeyForEntity(entityType);
    }

    public string GetMasterKey() => _masterKey;

    public (byte[] PublicKey, byte[] PrivateKey)? GetPqcKeyPair() => _pqcKeyPair;

    public void SetPqcKeyPair(byte[] publicKey, byte[] privateKey)
    {
        _pqcKeyPair = (publicKey, privateKey);
    }
}

/// <summary>
/// بناء Encryption configuration options (Fluent API)
/// </summary>
public sealed class EncryptionOptionsBuilder
{
    private readonly EncryptionOptions _options = new();

    public EncryptionOptionsBuilder WithMasterKey(string masterKey)
    {
        _options.MasterKey = masterKey;
        return this;
    }

    public EncryptionOptionsBuilder WithDefaultLevel(EncryptionLevel level)
    {
        _options.DefaultLevel = level;
        return this;
    }

    public EncryptionOptionsBuilder EncryptTableNames(bool encrypt = true)
    {
        _options.EncryptTableNames = encrypt;
        return this;
    }

    public EncryptionOptionsBuilder EncryptFieldNames(bool encrypt = true)
    {
        _options.EncryptFieldNames = encrypt;
        return this;
    }

    public EncryptionOptionsBuilder PreserveRelationships(bool preserve = true)
    {
        _options.PreserveRelationships = preserve;
        return this;
    }

    public EncryptionOptionsBuilder EncryptPrimaryKeys(bool encrypt = true)
    {
        _options.EncryptPrimaryKeys = encrypt;
        return this;
    }

    public EncryptionOptionsBuilder ExcludeEntity<TEntity>() where TEntity : class
    {
        _options.ExcludedEntities.Add(typeof(TEntity));
        return this;
    }

    public EncryptionOptionsBuilder ExcludeProperty<TEntity>(string propertyName) where TEntity : class
    {
        _options.ExcludedProperties.Add($"{typeof(TEntity).Name}.{propertyName}");
        return this;
    }

    public EncryptionOptionsBuilder WithEntityKey<TEntity>(string key) where TEntity : class
    {
        _options.EntityKeys[typeof(TEntity)] = key;
        return this;
    }

    public EncryptionOptionsBuilder WithKeyProvider(IEncryptionKeyProvider provider)
    {
        _options.KeyProvider = provider;
        return this;
    }

    public EncryptionOptions Build()
    {
        if (string.IsNullOrEmpty(_options.MasterKey))
        {
            throw new InvalidOperationException("Master key is required");
        }

        _options.KeyProvider ??= new DefaultKeyProvider(_options.MasterKey, _options.EntityKeys);

        return _options;
    }
}
