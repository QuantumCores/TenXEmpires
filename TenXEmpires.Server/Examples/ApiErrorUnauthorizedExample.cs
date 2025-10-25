using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for UNAUTHORIZED (401).
/// </summary>
public sealed class ApiErrorUnauthorizedExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "UNAUTHORIZED",
        Message: "User ID claim not found. User must be authenticated.");
}

