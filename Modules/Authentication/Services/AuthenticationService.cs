using Crm.Api.Modules.Authentication.DTOs;
using Crm.Api.Modules.Authentication.Entities;
using Crm.Api.Modules.Authentication.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Crm.Api.Modules.Authentication.Services;

public sealed class AuthenticationService(
    UserManager<ApplicationUser> userManager
) : IAuthenticationService
{
    public async Task RegisterAsync(SignUpRequestDto request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                            string.Join(", ", result.Errors.Select(e => e.Description))
                        );
        }
    }
}