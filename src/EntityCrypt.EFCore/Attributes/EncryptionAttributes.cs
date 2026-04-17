namespace EntityCrypt.EFCore.Attributes;

/// <summary>
/// Encryption level
/// </summary>
public enum EncryptionLevel
{
    /// <summary>No encryption</summary>
    None = 0,
    
    /// <summary>Classical encryption (AES-256-GCM)</summary>
    Classical = 1,
    
    /// <summary>Hybrid encryption (AES-256 + ML-KEM-768)</summary>
    Hybrid = 2,
    
    /// <summary>Post-quantum encryption (ML-KEM-768 only)</summary>
    PostQuantum = 3
}

/// <summary>
/// Marks a property for column-level encryption
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class EncryptedAttribute : Attribute
{
    /// <summary>Encryption level</summary>
    public EncryptionLevel Level { get; set; } = EncryptionLevel.Classical;
    
    /// <summary>هل يتم تشفير اسم الحقل أيضاً؟</summary>
    public bool EncryptFieldName { get; set; } = false;
    
    /// <summary>هل هذا حقل قابل للبحث (يستخدم hash للمقارنة)؟</summary>
    public bool Searchable { get; set; } = false;
}

/// <summary>
/// Marks an entity for full-table encryption
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class EncryptedTableAttribute : Attribute
{
    /// <summary>Encryption level الافتراضي للحقول</summary>
    public EncryptionLevel DefaultLevel { get; set; } = EncryptionLevel.Classical;
    
    /// <summary>هل يتم تشفير اسم الجدول؟</summary>
    public bool EncryptTableName { get; set; } = true;
    
    /// <summary>هل يتم تشفير أسماء الحقول؟</summary>
    public bool EncryptFieldNames { get; set; } = true;
    
    /// <summary>هل يتم تشفير المفاتيح الأساسية (PK)؟</summary>
    public bool EncryptPrimaryKeys { get; set; } = false;
    
    /// <summary>هل يتم تشفير المفاتيح الخارجية (FK)؟</summary>
    public bool EncryptForeignKeys { get; set; } = false;
}

/// <summary>
/// Marks a DbContext for database-wide encryption
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class EncryptedDatabaseAttribute : Attribute
{
    /// <summary>Encryption level الافتراضي</summary>
    public EncryptionLevel DefaultLevel { get; set; } = EncryptionLevel.Classical;
    
    /// <summary>هل يتم تشفير جميع الجداول؟</summary>
    public bool EncryptAllTables { get; set; } = true;
    
    /// <summary>هل يتم تشفير جميع الحقول؟</summary>
    public bool EncryptAllFields { get; set; } = true;
}

/// <summary>
/// Excludes a property from encryption
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class NoEncryptAttribute : Attribute
{
}

/// <summary>
/// Specifies a custom encryption key name for an entity or property
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public sealed class EncryptionKeyAttribute : Attribute
{
    /// <summary>Key name used to resolve the encryption key via IEncryptionKeyProvider</summary>
    public string KeyName { get; }
    
    public EncryptionKeyAttribute(string keyName)
    {
        KeyName = keyName;
    }
}
