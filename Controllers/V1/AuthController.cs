using System.Security.Claims;
using Asp.Versioning;
using Crm.Api.Contracts.V1.Auth;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting;

namespace Crm.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
[Authorize]
public sealed class AuthController(
    IAuthService authService,
    IAntiforgery antiforgery,
    IWebHostEnvironment environment) : ControllerBase
{
    private string RefreshCookieName => environment.IsDevelopment()
        ? "crm-refresh"
        : "__Secure-crm-refresh";
    private const string RefreshCookiePath = "/api/v1/auth";

    /// <summary>Creates an account and sends an email confirmation link.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("sign-up")]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SignUp(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await authService.RegisterAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (outcome.Errors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["password"] = outcome.Errors.ToArray()
            }));
        }

        return Accepted(new AcceptedResponse(
            "If the address can be registered, a confirmation message will be sent."));
    }

    /// <summary>Authenticates a user and issues an access token and refresh cookie.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("sign-in")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SignIn(
        SignInRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await authService.SignInAsync(
            request.Email,
            request.Password,
            cancellationToken);
        if (tokens is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Sign-in failed",
                Detail = "The credentials are invalid or the account is unavailable."
            });
        }

        SetRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        return Ok(ToResponse(tokens));
    }

    /// <summary>Rotates the refresh token and returns a new short-lived access token.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    [HttpPost("refresh-token")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            ClearRefreshCookie();
            return Unauthorized();
        }

        var tokens = await authService.RefreshAsync(refreshToken, cancellationToken);
        if (tokens is null)
        {
            ClearRefreshCookie();
            return Unauthorized();
        }

        SetRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        return Ok(ToResponse(tokens));
    }

    /// <summary>Confirms an account email using its Identity-protected token.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("verify-email")]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var verified = await authService.VerifyEmailAsync(
            request.UserId,
            request.Token,
            cancellationToken);
        return verified
            ? Ok(new AcceptedResponse("Email address confirmed."))
            : BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Email confirmation failed",
                Detail = "The confirmation link is invalid, expired, or already used."
            });
    }

    /// <summary>Sends another confirmation email without disclosing account existence.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("resend-verification")]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendVerification(
        ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        await authService.ResendVerificationAsync(request.Email, cancellationToken);
        return Accepted(new AcceptedResponse(
            "If the address needs confirmation, a message will be sent."));
    }

    /// <summary>Sends a password reset link without disclosing account existence.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await authService.ForgotPasswordAsync(request.Email, cancellationToken);
        return Accepted(new AcceptedResponse(
            "If an eligible account exists, password reset instructions will be sent."));
    }

    /// <summary>Changes a password using its Identity-protected reset token.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var reset = await authService.ResetPasswordAsync(
            request.UserId,
            request.Token,
            request.NewPassword,
            cancellationToken);
        return reset
            ? Ok(new AcceptedResponse("Password has been changed."))
            : BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Password reset failed",
                Detail = "The reset link is invalid, expired, or the password does not meet requirements."
            });
    }

    /// <summary>Revokes the current refresh session and clears its cookie.</summary>
    [ValidateAntiForgeryToken]
    [HttpPost("sign-out")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
        await authService.SignOutAsync(refreshToken, cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>Revokes every refresh session belonging to the signed-in user.</summary>
    [HttpPost("revoke-all-sessions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await authService.RevokeAllSessionsAsync(userId, cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>Returns the current authenticated account.</summary>
    [HttpGet("me")]
    [ProducesResponseType<AuthenticatedUser>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await authService.GetUserAsync(userId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    /// <summary>Issues the anti-forgery request token required for refresh and sign-out.</summary>
    [AllowAnonymous]
    [HttpGet("csrf-token")]
    [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
    public IActionResult GetCsrfToken()
    {
        Response.Headers.CacheControl = "no-store";
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken!));
    }

    private SignInResponse ToResponse(IssuedTokens tokens) =>
        new(tokens.AccessToken, "Bearer", tokens.AccessTokenExpiresAt, tokens.User);

    private void SetRefreshCookie(string token, DateTimeOffset expiresAt)
    {
        Response.Cookies.Append(
            RefreshCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !environment.IsDevelopment() || Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = RefreshCookiePath,
                Expires = expiresAt,
                IsEssential = true
            });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(
            RefreshCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !environment.IsDevelopment() || Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = RefreshCookiePath,
                IsEssential = true
            });
    }
}
