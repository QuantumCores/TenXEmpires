using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Provides example data for UnitDefinitionDto collection responses.
/// </summary>
public class UnitDefinitionsDtoExample : IExamplesProvider<ItemsResult<UnitDefinitionDto>>
{
    public ItemsResult<UnitDefinitionDto> GetExamples()
    {
        return new ItemsResult<UnitDefinitionDto>
        {
            Items = new List<UnitDefinitionDto>
            {
                new UnitDefinitionDto(
                    Id: 1,
                    Code: "warrior",
                    IsRanged: false,
                    Attack: 20,
                    Defence: 10,
                    RangeMin: 0,
                    RangeMax: 0,
                    MovePoints: 2,
                    Health: 100),
                new UnitDefinitionDto(
                    Id: 2,
                    Code: "archer",
                    IsRanged: true,
                    Attack: 15,
                    Defence: 5,
                    RangeMin: 2,
                    RangeMax: 3,
                    MovePoints: 2,
                    Health: 80),
                new UnitDefinitionDto(
                    Id: 3,
                    Code: "cavalry",
                    IsRanged: false,
                    Attack: 25,
                    Defence: 8,
                    RangeMin: 0,
                    RangeMax: 0,
                    MovePoints: 4,
                    Health: 90)
            }
        };
    }
}

