using Crm.Application.Auth;

namespace Crm.Api.Contracts.V1.Auth;

/// <summary>Short-lived access token returned to the signed-in client.</summary>
public sealed record SignInResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    AuthenticatedUser User);

/// <summary>Anti-forgery request token required for cookie-authenticated operations.</summary>
public sealed record CsrfTokenResponse(string RequestToken);

/// <summary>Indicates that the requested operation has been accepted.</summary>
public sealed record AcceptedResponse(string Message);
