using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for retrieving static lookup data (game rules, definitions, maps).
/// </summary>
public interface ILookupService
{
    /// <summary>
    /// Gets all unit definitions (static game rules data).
    /// </summary>
    /// <returns>A read-only list of unit definitions.</returns>
    Task<IReadOnlyList<UnitDefinitionDto>> GetUnitDefinitionsAsync();

    /// <summary>
    /// Gets the ETag for the current unit definitions data.
    /// </summary>
    /// <returns>An ETag string that represents the current state of unit definitions.</returns>
    Task<string> GetUnitDefinitionsETagAsync();
}

