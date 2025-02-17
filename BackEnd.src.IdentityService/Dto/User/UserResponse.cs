using BackEnd.src.IdentityService.Dto.Token;

namespace BackEnd.src.IdentityService.Dto.User;

public class UserResponse
{
    public string Id { get; set; } 
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    public TokenResponse TokenResponse { get; set; }
}