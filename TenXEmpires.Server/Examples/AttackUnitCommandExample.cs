using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger example for AttackUnitCommand (POST /games/{id}/actions/attack request body).
/// </summary>
public sealed class AttackUnitCommandExample : IExamplesProvider<AttackUnitCommand>
{
    public AttackUnitCommand GetExamples()
    {
        return new AttackUnitCommand(
            AttackerUnitId: 201,
            TargetUnitId: 305
        );
    }
}

