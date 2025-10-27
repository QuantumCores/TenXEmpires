using System;
using System.ComponentModel.DataAnnotations;

namespace TenXEmpires.Server.Domain.DataContracts;

public sealed record LoginRequestDto(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password,
    bool RememberMe = false);

public sealed record RegisterRequestDto(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password);

public sealed record ForgotPasswordRequestDto(
    [property: Required, EmailAddress] string Email);

public sealed record ResendVerificationRequestDto(
    [property: EmailAddress] string? Email);
