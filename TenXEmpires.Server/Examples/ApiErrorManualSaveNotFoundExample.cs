using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for SAVE_NOT_FOUND (404) when deleting a manual save.
/// </summary>
public sealed class ApiErrorManualSaveNotFoundExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "SAVE_NOT_FOUND",
        Message: "Manual save not found in the specified slot.");
}

