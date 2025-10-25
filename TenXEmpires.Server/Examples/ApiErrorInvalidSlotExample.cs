using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for INVALID_SLOT (400) when deleting a manual save.
/// </summary>
public sealed class ApiErrorInvalidSlotExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "INVALID_SLOT",
        Message: "Slot must be between 1 and 3.");
}

