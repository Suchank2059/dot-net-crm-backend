using Crm.Api.Common.Api;
using Crm.Api.Modules.Authentication.DTOs;
using Crm.Api.Modules.Authentication.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Modules.Authentication.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController(
    IAuthenticationService authenticationService
) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        SignUpRequestDto request)
    {
        await authenticationService.RegisterAsync(request);

        return ApiResponses.Created<object>(
            data: null,
            message: "Registration successful"
        );
    }
}