using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for GAME_NOT_FOUND (404).
/// </summary>
public sealed class ApiErrorGameNotFoundExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "GAME_NOT_FOUND",
        Message: "Game not found or you don't have access to it.");
}

