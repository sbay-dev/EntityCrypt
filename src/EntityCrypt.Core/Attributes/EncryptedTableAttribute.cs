using System;

namespace EntityCrypt.Core.Attributes;

/// <summary>
/// يشير إلى أن الجدول يجب تشفيره بالكامل
/// يطبق على Classes التي تمثل Entity Models
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class EncryptedTableAttribute : Attribute
{
    /// <summary>
    /// رقم الطبقة في التسلسل الهرمي (1 = الأولى)
    /// </summary>
    public int LayerIndex { get; set; } = 1;

    /// <summary>
    /// اسم الطبقة السابقة للربط الهرمي
    /// null للطبقة الأولى
    /// </summary>
    public string? ParentLayerName { get; set; }

    /// <summary>
    /// هل يتم تخزين هيكل العلاقات
    /// </summary>
    public bool StoreRelationships { get; set; } = true;

    /// <summary>
    /// استراتيجية التشفير
    /// </summary>
    public EncryptionStrategy Strategy { get; set; } = EncryptionStrategy.Hierarchical;

    /// <summary>
    /// معلومات إضافية
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// استراتيجيات التشفير المتاحة
/// </summary>
public enum EncryptionStrategy
{
    /// <summary>
    /// تشفير هرمي متعدد الطبقات (الافتراضي)
    /// </summary>
    Hierarchical,
    
    /// <summary>
    /// تشفير مستقل لكل جدول
    /// </summary>
    Standalone,
    
    /// <summary>
    /// تشفير بمفتاح مشترك
    /// </summary>
    Shared
}
