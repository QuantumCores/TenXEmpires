using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Provides example data for paginated MapTileDto responses.
/// </summary>
public class PagedMapTileDtoExample : IExamplesProvider<PagedResult<MapTileDto>>
{
    public PagedResult<MapTileDto> GetExamples()
    {
        return new PagedResult<MapTileDto>
        {
            Items = new List<MapTileDto>
            {
                new MapTileDto(
                    Id: 1,
                    Row: 0,
                    Col: 0,
                    Terrain: "grassland",
                    ResourceType: "wheat",
                    ResourceAmount: 2),
                new MapTileDto(
                    Id: 2,
                    Row: 0,
                    Col: 1,
                    Terrain: "plains",
                    ResourceType: null,
                    ResourceAmount: 0),
                new MapTileDto(
                    Id: 3,
                    Row: 0,
                    Col: 2,
                    Terrain: "forest",
                    ResourceType: "wood",
                    ResourceAmount: 3)
            },
            Page = 1,
            PageSize = 20,
            Total = 400
        };
    }
}

