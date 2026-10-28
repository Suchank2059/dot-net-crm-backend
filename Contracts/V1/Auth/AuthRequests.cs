using System.ComponentModel.DataAnnotations;

namespace Crm.Api.Contracts.V1.Auth;

/// <summary>Credentials and profile details for creating an account.</summary>
public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MinLength(12), MaxLength(128)]
    public required string Password { get; init; }
}

/// <summary>Credentials used to sign in.</summary>
public sealed record SignInRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MaxLength(128)]
    public required string Password { get; init; }
}

/// <summary>Identity-protected email confirmation token.</summary>
public sealed record VerifyEmailRequest
{
    [Required]
    public required string UserId { get; init; }

    [Required]
    public required string Token { get; init; }
}

/// <summary>Email address for requesting another confirmation message.</summary>
public sealed record ResendVerificationRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }
}

/// <summary>Email address for requesting a password reset link.</summary>
public sealed record ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }
}

/// <summary>Identity-protected password reset token and replacement password.</summary>
public sealed record ResetPasswordRequest
{
    [Required]
    public required string UserId { get; init; }

    [Required]
    public required string Token { get; init; }

    [Required, MinLength(12), MaxLength(128)]
    public required string NewPassword { get; init; }
}
