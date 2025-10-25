using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger request example for CreateManualSaveCommand
/// </summary>
public class CreateManualSaveCommandExample : IExamplesProvider<CreateManualSaveCommand>
{
    public CreateManualSaveCommand GetExamples()
    {
        return new CreateManualSaveCommand(
            Slot: 1,
            Name: "Before assault on capital");
    }
}


