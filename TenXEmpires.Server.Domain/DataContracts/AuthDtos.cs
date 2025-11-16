using System;
using System.ComponentModel.DataAnnotations;

namespace TenXEmpires.Server.Domain.DataContracts;

public sealed record LoginRequestDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    bool RememberMe = false);

public sealed record RegisterRequestDto
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [Required, MinLength(8), Compare(nameof(Password))]
    public string Confirm { get; init; } = string.Empty;
}

public sealed record ForgotPasswordRequestDto(
    [Required, EmailAddress] string Email);

public sealed record ResendVerificationRequestDto(
    [EmailAddress] string? Email);

public sealed record ConfirmEmailRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Token);

public sealed record ResetPasswordRequestDto
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string Confirm { get; init; } = string.Empty;
}
