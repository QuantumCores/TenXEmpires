using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Provides example data for MapDto responses.
/// </summary>
public class MapDtoExample : IExamplesProvider<MapDto>
{
    public MapDto GetExamples()
    {
        return new MapDto(
            Id: 1,
            Code: "map-01",
            SchemaVersion: 1,
            Width: 20,
            Height: 30);
    }
}

