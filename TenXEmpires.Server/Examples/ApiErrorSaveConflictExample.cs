using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for SAVE_CONFLICT (409).
/// </summary>
public sealed class ApiErrorSaveConflictExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "SAVE_CONFLICT",
        Message: "Could not create manual save due to a conflict. Please retry.");
}

