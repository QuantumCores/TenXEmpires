using System;

namespace TenXEmpires.Server.Domain.DataContracts;

/// <summary>
/// Standard API error response payload with a short code and human-readable message.
/// </summary>
public sealed record ApiErrorDto(
    string Code,
    string Message);

