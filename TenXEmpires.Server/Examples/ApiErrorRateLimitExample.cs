using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for RATE_LIMIT_EXCEEDED (429).
/// </summary>
public sealed class ApiErrorRateLimitExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "RATE_LIMIT_EXCEEDED",
        Message: "Too many requests. Please try again later.");
}

