namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents application settings (e.g., analytics salt)
/// </summary>
public class Setting
{
    public long Id { get; set; }
    
    /// <summary>
    /// Salt used for hashing user IDs in analytics
    /// </summary>
    public byte[] AnalyticsSalt { get; set; } = Array.Empty<byte>();
    
    public int SaltVersion { get; set; }
    
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

