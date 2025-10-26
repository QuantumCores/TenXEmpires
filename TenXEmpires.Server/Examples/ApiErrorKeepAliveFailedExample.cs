using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for KEEPALIVE_FAILED (500).
/// </summary>
public sealed class ApiErrorKeepAliveFailedExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "KEEPALIVE_FAILED",
        Message: "Unable to refresh session.");
}

