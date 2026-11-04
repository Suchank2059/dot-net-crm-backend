using Crm.Api.Modules.Authentication.DTOs;

namespace Crm.Api.Modules.Authentication.Interfaces;

public interface IAuthenticationService
{
    Task RegisterAsync(SignUpRequestDto request);
}