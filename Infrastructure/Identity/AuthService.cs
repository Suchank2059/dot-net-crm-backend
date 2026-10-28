using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Crm.Application.Auth;
using Crm.Domain.Auth;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    CrmDbContext dbContext,
    IEmailSender emailSender,
    IOptions<JwtSettings> jwtSettings,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    private static readonly ApplicationUser TimingUser = new()
    {
        Id = "timing-check",
        UserName = "timing-check"
    };
    private static readonly PasswordHasher<ApplicationUser> TimingPasswordHasher = new();
    private static readonly string TimingPasswordHash =
        TimingPasswordHasher.HashPassword(
            TimingUser,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    private readonly JwtSettings _jwt = jwtSettings.Value;

    public async Task<RegistrationOutcome> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return new RegistrationOutcome([]);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            LockoutEnabled = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var duplicate = result.Errors.Any(error =>
                error.Code is "DuplicateEmail" or "DuplicateUserName");
            return new RegistrationOutcome(
                duplicate ? [] : result.Errors.Select(error => error.Description).ToArray());
        }

        await SendEmailConfirmationAsync(user, cancellationToken);
        return new RegistrationOutcome([]);
    }

    public async Task<IssuedTokens?> SignInAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            _ = TimingPasswordHasher.VerifyHashedPassword(
                TimingUser,
                TimingPasswordHash,
                password);
            return null;
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            password,
            lockoutOnFailure: true);

        return signInResult.Succeeded ? await IssueTokensAsync(user, null) : null;
    }

    public async Task<IssuedTokens?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(refreshToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var current = await dbContext.RefreshSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);

        if (current is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (current.RevokedAt is not null)
        {
            await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogWarning(
                "Refresh token reuse detected for user {UserId} and family {FamilyId}",
                current.UserId,
                current.FamilyId);
            return null;
        }

        if (current.ExpiresAt <= now || current.AbsoluteExpiresAt <= now)
        {
            await dbContext.RefreshSessions
                .Where(session => session.Id == current.Id && session.RevokedAt == null)
                .ExecuteUpdateAsync(
                    updates => updates.SetProperty(session => session.RevokedAt, now),
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var claimed = await dbContext.RefreshSessions
            .Where(session =>
                session.Id == current.Id &&
                session.RevokedAt == null &&
                session.ExpiresAt > now &&
                session.AbsoluteExpiresAt > now)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(session => session.RevokedAt, now),
                cancellationToken);

        if (claimed == 0)
        {
            await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogWarning(
                "Concurrent refresh or token reuse detected for user {UserId} and family {FamilyId}",
                current.UserId,
                current.FamilyId);
            return null;
        }

        var user = await userManager.FindByIdAsync(current.UserId);
        if (user is null || !user.EmailConfirmed)
        {
            await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var replacement = CreateRefreshSession(
            user.Id,
            current.FamilyId,
            current.AbsoluteExpiresAt,
            now);

        await dbContext.RefreshSessions.AddAsync(replacement.Session, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.RefreshSessions
            .Where(session => session.Id == current.Id)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    session => session.ReplacedBySessionId,
                    replacement.Session.Id),
                cancellationToken);

        var tokens = CreateAccessToken(user, replacement.RawToken, replacement.Session.ExpiresAt);
        await transaction.CommitAsync(cancellationToken);
        return tokens;
    }

    public async Task SignOutAsync(
        string? refreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashToken(refreshToken);
        await dbContext.RefreshSessions
            .Where(session => session.TokenHash == tokenHash && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    session => session.RevokedAt,
                    DateTimeOffset.UtcNow),
                cancellationToken);
    }

    public async Task RevokeAllSessionsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await dbContext.RefreshSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    session => session.RevokedAt,
                    DateTimeOffset.UtcNow),
                cancellationToken);
    }

    public async Task<bool> VerifyEmailAsync(
        string userId,
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.EmailConfirmed)
        {
            return false;
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task ResendVerificationAsync(
        string email,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        await SendEmailConfirmationAsync(user, cancellationToken);
    }

    public async Task ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = BuildLink(
            configuration["Email:PasswordResetUrl"],
            ("userId", user.Id),
            ("token", token));
        await emailSender.SendAsync(
            user.Email!,
            "Reset your CRM password",
            $"Use this link to reset your password. It expires in one hour:\n\n{resetUrl}\n\n" +
            "If you did not request this, you can ignore this message.",
            cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(
        string userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return false;
        }

        await RevokeAllSessionsAsync(user.Id, cancellationToken);
        return true;
    }

    public async Task<AuthenticatedUser?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        return user is null || user.Email is null
            ? null
            : new AuthenticatedUser(user.Id, user.Email);
    }

    private async Task SendEmailConfirmationAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var verificationUrl = BuildLink(
            configuration["Email:EmailVerificationUrl"],
            ("userId", user.Id),
            ("token", token));
        await emailSender.SendAsync(
            user.Email!,
            "Verify your CRM email",
            $"Verify your email address using this link:\n\n{verificationUrl}\n\n" +
            "If you did not create this account, you can ignore this message.",
            cancellationToken);
    }

    private async Task<IssuedTokens> IssueTokensAsync(
        ApplicationUser user,
        DateTimeOffset? absoluteExpiration)
    {
        var now = DateTimeOffset.UtcNow;
        var absoluteExpiresAt = absoluteExpiration ??
            now.AddDays(_jwt.RefreshTokenAbsoluteDays);
        var refreshSession = CreateRefreshSession(
            user.Id,
            Guid.NewGuid(),
            absoluteExpiresAt,
            now);

        await dbContext.RefreshSessions.AddAsync(refreshSession.Session);
        await dbContext.SaveChangesAsync();

        return CreateAccessToken(
            user,
            refreshSession.RawToken,
            refreshSession.Session.ExpiresAt);
    }

    private (RefreshSession Session, string RawToken) CreateRefreshSession(
        string userId,
        Guid familyId,
        DateTimeOffset absoluteExpiresAt,
        DateTimeOffset now)
    {
        var rawToken = CreateOpaqueToken();
        var expiresAt = Min(now.AddDays(_jwt.RefreshTokenIdleDays), absoluteExpiresAt);
        return (
            new RefreshSession
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId,
                UserId = userId,
                TokenHash = HashToken(rawToken),
                CreatedAt = now,
                ExpiresAt = expiresAt,
                AbsoluteExpiresAt = absoluteExpiresAt
            },
            rawToken);
    }

    private IssuedTokens CreateAccessToken(
        ApplicationUser user,
        string refreshToken,
        DateTimeOffset refreshExpiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        var accessExpiresAt = now.AddMinutes(_jwt.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("email_verified", user.EmailConfirmed ? "true" : "false")
        };
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_jwt.SigningKeyBase64));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExpiresAt.UtcDateTime,
            signingCredentials: credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        return new IssuedTokens(
            accessToken,
            accessExpiresAt,
            refreshToken,
            refreshExpiresAt,
            new AuthenticatedUser(user.Id, user.Email!));
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        await dbContext.RefreshSessions
            .Where(session => session.FamilyId == familyId && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(session => session.RevokedAt, revokedAt),
                cancellationToken);
    }

    private static string BuildLink(
        string? baseUrl,
        params (string Name, string Value)[] parameters)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Host is not ("localhost" or "127.0.0.1")))
        {
            throw new InvalidOperationException(
                "A valid HTTPS email action URL must be configured (HTTP is allowed for localhost development).");
        }

        var query = string.Join(
            "&",
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Value)}"));
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return $"{uri}{separator}{query}";
    }

    private static string CreateOpaqueToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return token.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) =>
        first <= second ? first : second;
}
