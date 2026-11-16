using ServiceOps.Api.DTOs.Auth;

namespace ServiceOps.Api.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);

    Task<LoginResponse?> RefreshTokenAsync(RefreshTokenRequest request);
}