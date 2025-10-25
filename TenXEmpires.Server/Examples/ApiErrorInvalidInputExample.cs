using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for INVALID_INPUT (400).
/// </summary>
public sealed class ApiErrorInvalidInputExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "INVALID_INPUT",
        Message: "One or more validation errors occurred.");
}

