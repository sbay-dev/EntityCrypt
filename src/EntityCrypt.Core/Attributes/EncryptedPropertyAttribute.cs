using System;

namespace EntityCrypt.Core.Attributes;

/// <summary>
/// يشير إلى أن الخاصية يجب تشفيرها
/// يطبق على Properties في Entity Models
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class EncryptedPropertyAttribute : Attribute
{
    /// <summary>
    /// هل هذه الخاصية حساسة (تشفير مزدوج)
    /// البيانات الحساسة جداً مثل كلمات المرور وأرقام البطاقات
    /// </summary>
    public bool IsSensitive { get; set; } = false;

    /// <summary>
    /// هل يتم إنشاء index على القيمة المشفرة
    /// مفيد للبحث السريع
    /// </summary>
    public bool CreateIndex { get; set; } = false;

    /// <summary>
    /// هل يتم تخزين hash للقيمة (للبحث بدون فك التشفير)
    /// </summary>
    public bool StoreHash { get; set; } = false;

    /// <summary>
    /// طول Hash المخزن (بالأحرف)
    /// </summary>
    public int HashLength { get; set; } = 12;

    /// <summary>
    /// هل يمكن البحث في هذه الخاصية
    /// </summary>
    public bool Searchable { get; set; } = false;

    /// <summary>
    /// معلومات إضافية عن الخاصية
    /// </summary>
    public string? Description { get; set; }
}
