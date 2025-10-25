using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Example error payload for INTERNAL_ERROR (500).
/// </summary>
public sealed class ApiErrorInternalExample : IExamplesProvider<ApiErrorDto>
{
    public ApiErrorDto GetExamples() => new(
        Code: "INTERNAL_ERROR",
        Message: "An error occurred while deleting the manual save.");
}

