using System;

namespace EntityCrypt.Core.Models;

/// <summary>
/// يمثل طبقة واحدة في التسلسل الهرمي للتشفير
/// كل طبقة تعتمد على hash الطبقة السابقة
/// </summary>
public sealed record EncryptionLayer
{
    /// <summary>
    /// الاسم الأصلي للجدول (غير مشفر)
    /// </summary>
    public required string OriginalTableName { get; init; }

    /// <summary>
    /// الاسم المشفر للجدول في قاعدة البيانات
    /// </summary>
    public required string EncryptedTableName { get; init; }

    /// <summary>
    /// Hash هذه الطبقة (سيستخدم للطبقة التالية)
    /// </summary>
    public required string TableHash { get; init; }

    /// <summary>
    /// Hash الطبقة السابقة (null للطبقة الأولى)
    /// </summary>
    public string? PreviousLayerHash { get; init; }

    /// <summary>
    /// مفتاح تشفير الحقول في هذه الطبقة
    /// </summary>
    public required string FieldEncryptionKey { get; init; }

    /// <summary>
    /// Timestamp إنشاء الطبقة
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// خريطة الحقول: الأصلي -> المشفر
    /// </summary>
    public Dictionary<string, string> FieldMapping { get; init; } = new();

    /// <summary>
    /// رقم الطبقة في التسلسل الهرمي (1 = الأولى)
    /// </summary>
    public int LayerIndex { get; init; }

    /// <summary>
    /// البذرة المستخدمة لإنشاء هذه الطبقة
    /// </summary>
    public required string Seed { get; init; }

    /// <summary>
    /// معلومات إضافية عن الطبقة
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
