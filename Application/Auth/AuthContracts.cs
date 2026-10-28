namespace Crm.Application.Auth;

public sealed record AuthenticatedUser(string Id, string Email);

public sealed record RegistrationOutcome(IReadOnlyList<string> Errors);

public sealed record IssuedTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUser User);

public interface IAuthService
{
    Task<RegistrationOutcome> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<IssuedTokens?> SignInAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<IssuedTokens?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task SignOutAsync(string? refreshToken, CancellationToken cancellationToken);

    Task RevokeAllSessionsAsync(string userId, CancellationToken cancellationToken);

    Task<bool> VerifyEmailAsync(
        string userId,
        string token,
        CancellationToken cancellationToken);

    Task ResendVerificationAsync(string email, CancellationToken cancellationToken);

    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken);

    Task<bool> ResetPasswordAsync(
        string userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken);

    Task<AuthenticatedUser?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken);
}

public interface IEmailSender
{
    Task SendAsync(
        string email,
        string subject,
        string plainTextBody,
        CancellationToken cancellationToken);
}
