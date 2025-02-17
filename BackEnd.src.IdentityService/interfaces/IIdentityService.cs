using BackEnd.src.IdentityService.Dto.User;
using IdentityModel.Client;
using TokenResponse = BackEnd.src.IdentityService.Dto.Token.TokenResponse;

namespace BackEnd.src.IdentityService.interfaces;

public interface IIdentityService
{
    Task<UserResponse> RegisterAsync(UserRegistrationRequest request, CancellationToken cancellationToken);
    Task<TokenResponse> LoginAsync(UserLoginRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(string accessToken, string refreshToken, CancellationToken cancellationToken);
    
    Task<TokenResponse?> RefreshTokenAsync(string refreshTokenRequest, CancellationToken cancellationToken);
}