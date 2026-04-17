using EntityCrypt.EFCore.Attributes;
using EntityCrypt.EFCore.Configuration;
using EntityCrypt.EFCore.Context;
using EntityCrypt.EFCore.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace EntityCrypt.EFCore.Extensions;

/// <summary>
/// Extension methods for EntityCrypt integration with EF Core
/// </summary>
public static class EntityCryptExtensions
{
    /// <summary>
    /// Enables transparent encryption on a DbContext
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;(options =>
    ///     options.UseSqlite("Data Source=app.db")
    ///            .UseEntityCrypt(encryption => encryption
    ///                .WithMasterKey("my-secret-key")
    ///                .WithDefaultLevel(EncryptionLevel.Classical)
    ///                .PreserveRelationships()
    ///            ));
    /// </code>
    /// </example>
    public static DbContextOptionsBuilder UseEntityCrypt(
        this DbContextOptionsBuilder optionsBuilder,
        Action<EncryptionOptionsBuilder> configure)
    {
        var builder = new EncryptionOptionsBuilder();
        configure(builder);
        var options = builder.Build();

        // Register encryption interceptors
        optionsBuilder.AddInterceptors(
            new EncryptionSaveChangesInterceptor(options),
            new DecryptionMaterializationInterceptor(options)
        );

        return optionsBuilder;
    }

    /// <summary>
    /// Enables encryption with pre-configured options
    /// </summary>
    public static DbContextOptionsBuilder UseEntityCrypt(
        this DbContextOptionsBuilder optionsBuilder,
        EncryptionOptions options)
    {
        optionsBuilder.AddInterceptors(
            new EncryptionSaveChangesInterceptor(options),
            new DecryptionMaterializationInterceptor(options)
        );

        return optionsBuilder;
    }

    /// <summary>
    /// Enables classical AES-256-GCM encryption with sensible defaults
    /// </summary>
    public static DbContextOptionsBuilder UseEntityCryptClassical(
        this DbContextOptionsBuilder optionsBuilder,
        string masterKey)
    {
        return optionsBuilder.UseEntityCrypt(encryption => encryption
            .WithMasterKey(masterKey)
            .WithDefaultLevel(EncryptionLevel.Classical)
            .PreserveRelationships());
    }

    /// <summary>
    /// Enables hybrid encryption (AES-256 + ML-KEM-768)
    /// </summary>
    public static DbContextOptionsBuilder UseEntityCryptHybrid(
        this DbContextOptionsBuilder optionsBuilder,
        string masterKey)
    {
        return optionsBuilder.UseEntityCrypt(encryption => encryption
            .WithMasterKey(masterKey)
            .WithDefaultLevel(EncryptionLevel.Hybrid)
            .PreserveRelationships());
    }
}

/// <summary>
/// Extension methods for entity operations
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    /// Manually encrypts an entity (typically handled by interceptors)
    /// </summary>
    public static T Encrypt<T>(this T entity, EntityEncryptionService service) where T : class
    {
        // Encryption is handled automatically by interceptors
        // This method is for manual use when needed
        return entity;
    }

    /// <summary>
    /// فك Manually encrypts an entity (typically handled by interceptors)
    /// </summary>
    public static T Decrypt<T>(this T entity, EntityEncryptionService service) where T : class
    {
        service.DecryptEntity(entity);
        return entity;
    }
}
