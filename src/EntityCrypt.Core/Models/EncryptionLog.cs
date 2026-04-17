using System;

namespace EntityCrypt.Core.Models;

/// <summary>
/// سجل شفاف لكل عملية تشفير/فك تشفير
/// يوفر شفافية كاملة دون التأثير على الأداء
/// </summary>
public sealed record EncryptionLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required EncryptionOperation Operation { get; init; }
    public required string LayerName { get; init; }
    public required string EntityType { get; init; }
    public int PropertyCount { get; init; }
    public TimeSpan Duration { get; init; }
    public bool Success { get; init; } = true;
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// معلومات إضافية للتتبع
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// معلومات البيئة التنفيذية
    /// </summary>
    public ExecutionEnvironment Environment { get; init; } = ExecutionEnvironment.Detect();
}

/// <summary>
/// أنواع العمليات المتاحة
/// </summary>
public enum EncryptionOperation
{
    Encrypt,
    Decrypt,
    CreateLayer,
    LoadSession,
    SaveSession,
    Synchronize
}

/// <summary>
/// معلومات البيئة التنفيذية
/// </summary>
public sealed record ExecutionEnvironment
{
    public required string Platform { get; init; }
    public required string Runtime { get; init; }
    public bool IsWasm { get; init; }
    public bool IsLinux { get; init; }
    public bool IsWindows { get; init; }
    public bool IsDormant { get; init; }

    public static ExecutionEnvironment Detect()
    {
        var isWasm = OperatingSystem.IsBrowser();
        var isLinux = OperatingSystem.IsLinux();
        var isWindows = OperatingSystem.IsWindows();

        return new ExecutionEnvironment
        {
            Platform = isWasm ? "Browser-WASM" 
                      : isLinux ? "Linux" 
                      : isWindows ? "Windows" 
                      : "Unknown",
            Runtime = Environment.Version.ToString(),
            IsWasm = isWasm,
            IsLinux = isLinux,
            IsWindows = isWindows,
            IsDormant = !Environment.UserInteractive || isWasm
        };
    }
}
