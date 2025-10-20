namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents resources stored in a city
/// </summary>
public class CityResource
{
    public long Id { get; set; }
    
    public long CityId { get; set; }
    
    /// <summary>
    /// Type of resource (e.g., 'food', 'production', 'gold')
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;
    
    /// <summary>
    /// Amount of resource stored
    /// </summary>
    public int Amount { get; set; }
    
    // Navigation properties
    public City City { get; set; } = null!;
}

