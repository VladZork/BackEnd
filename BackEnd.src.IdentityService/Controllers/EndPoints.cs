using BackEnd.src.IdentityService.Dto.User;
using BackEnd.src.IdentityService.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;

namespace BackEnd.src.IdentityService.Controllers;

public static class EndPoints
{
    public static void MapUserApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/user/register",  async (HttpContext httpContext, UserRegistrationRequest request, IIdentityService identityService,CancellationToken cancellationToken) =>
        {
            return await identityService.RegisterAsync(request, cancellationToken);
        });

        endpoints.MapPost("/api/user/login", async(HttpContext httpContext, UserLoginRequest loginRequest, IIdentityService identityService,CancellationToken cancellationToken) =>
        {
            return await identityService.LoginAsync(loginRequest, cancellationToken);
        });
        
        endpoints.MapPost("/api/user/refresh", [Authorize] async (HttpContext httpContext,string refreshToken,IIdentityService identityService,CancellationToken cancellationToken) =>
        {
           return await identityService.RefreshTokenAsync(refreshToken, cancellationToken);
        });
        
        endpoints.MapPost("/api/user/logout", [Authorize] async (HttpContext httpContext,string accessToken,string refreshToken,IIdentityService identityService,CancellationToken cancellationToken) =>
        {
            await identityService.LogoutAsync(accessToken,refreshToken, cancellationToken);
        });
    }
}