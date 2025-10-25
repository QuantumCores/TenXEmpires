using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger example for SaveCreatedDto response (manual save creation)
/// </summary>
public class SaveCreatedDtoExample : IExamplesProvider<SaveCreatedDto>
{
    public SaveCreatedDto GetExamples()
    {
        return new SaveCreatedDto(
            Id: 301,
            Slot: 1,
            TurnNo: 8,
            CreatedAt: DateTimeOffset.Parse("2025-10-25T12:00:00Z"),
            Name: "Before assault on capital");
    }
}


