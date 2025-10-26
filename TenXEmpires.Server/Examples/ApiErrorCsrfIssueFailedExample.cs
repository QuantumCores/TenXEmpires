using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for CSRF_ISSUE_FAILED (500).
/// </summary>
public sealed class ApiErrorCsrfIssueFailedExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "CSRF_ISSUE_FAILED",
        Message: "Unable to issue CSRF token.");
}

