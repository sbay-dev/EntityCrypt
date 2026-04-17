using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EntityCrypt.Core.Models;

/// <summary>
/// البيان الشامل لنظام التشفير
/// يحتوي على جميع المعلومات اللازمة لفك التشفير
/// قابل للتصدير كمنتج مرخص
/// </summary>
public sealed record EncryptionManifest
{
    /// <summary>
    /// معرف فريد لهذا البيان
    /// </summary>
    public Guid ManifestId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// مفتاح Instance الرئيسي
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InstanceKey { get; init; }

    /// <summary>
    /// تاريخ إنشاء البيان
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// إصدار المكتبة
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// جميع الطبقات بالترتيب
    /// </summary>
    public List<EncryptionLayer> Layers { get; init; } = new();

    /// <summary>
    /// خريطة الجداول: الأصلي -> المشفر
    /// </summary>
    public Dictionary<string, string> TableMapping { get; init; } = new();

    /// <summary>
    /// معلومات المالك (للمنتجات المرخصة)
    /// </summary>
    public string? OwnerIdentifier { get; init; }

    /// <summary>
    /// التوقيع الرقمي للتحقق من الأصالة
    /// </summary>
    public string? Signature { get; init; }

    /// <summary>
    /// معلومات الترخيص
    /// </summary>
    public LicenseInfo? License { get; init; }

    /// <summary>
    /// تصدير البيان كـ JSON
    /// </summary>
    public string ExportToJson(bool includeSecrets = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        if (!includeSecrets)
        {
            // إخفاء المفاتيح الحساسة
            var safeManifest = this with
            {
                InstanceKey = null,
                Layers = Layers.Select(l => l with
                {
                    FieldEncryptionKey = "***REDACTED***",
                    Seed = "***REDACTED***"
                }).ToList()
            };
            return JsonSerializer.Serialize(safeManifest, options);
        }

        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// استيراد من JSON
    /// </summary>
    public static EncryptionManifest? ImportFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Deserialize<EncryptionManifest>(json, options);
    }
}

/// <summary>
/// معلومات الترخيص للمنتجات المهيكلة
/// </summary>
public sealed record LicenseInfo
{
    public required string LicenseType { get; init; }
    public required string LicensedTo { get; init; }
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; init; }
    public Dictionary<string, string> Terms { get; init; } = new();
}
