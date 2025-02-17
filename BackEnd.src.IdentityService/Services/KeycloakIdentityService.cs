using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BackEnd.src.IdentityService.Dto.User;
using BackEnd.src.IdentityService.interfaces;
using IdentityModel.Client;
using TokenResponse = BackEnd.src.IdentityService.Dto.Token.TokenResponse;

namespace BackEnd.src.IdentityService.Services;

public class KeycloakIdentityService : IIdentityService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public KeycloakIdentityService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<UserResponse> RegisterAsync(UserRegistrationRequest request, CancellationToken cancellationToken)
{
    // 1. Получаем токен для аутентификации как администратор
    var tokenUrl =
        $"{_configuration["Keycloak:Authority"]}/realms/{_configuration["Keycloak:Realm"]}/protocol/openid-connect/token";
    var clientId = _configuration["Keycloak:ClientId"];
    var clientSecret = _configuration["Keycloak:ClientSecret"];

    var formData = new Dictionary<string, string>
    {
        { "client_id", clientId },
        { "client_secret", clientSecret },
        { "grant_type", "client_credentials" }
    };

    var requestContent = new FormUrlEncodedContent(formData);

    var tokenResponse = await _httpClient.PostAsync(tokenUrl, requestContent, cancellationToken);
    var jsonResponse = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
    var adminToken = JsonSerializer.Deserialize<JsonElement>(jsonResponse).GetProperty("access_token").GetString();

    // 2. Создаем пользователя
    var createUserUrl =
        $"{_configuration["Keycloak:Authority"]}/admin/realms/{_configuration["Keycloak:Realm"]}/users";
    var newUser = new
    {
        username = request.Username,
        email = request.Email,
        firstName = request.FirstName,
        lastName = request.LastName,
        enabled = true,
        credentials = new[] { new { type = "password", value = request.Password, temporary = false } }
    };

    var createUserContent = new StringContent(JsonSerializer.Serialize(newUser), Encoding.UTF8, "application/json");

    var createUserRequest = new HttpRequestMessage(HttpMethod.Post, createUserUrl)
    {
        Content = createUserContent
    };
    createUserRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

    var createUserResponse = await _httpClient.SendAsync(createUserRequest, cancellationToken);

    if (!createUserResponse.IsSuccessStatusCode)
    {
        var errorMessage = await createUserResponse.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Не удалось создать пользователя: {errorMessage}");
    }
    
    var userId = createUserResponse.Headers.Location?.ToString().Split('/').LastOrDefault();

 
    var getRoleUrl = $"{_configuration["Keycloak:Authority"]}/admin/realms/{_configuration["Keycloak:Realm"]}/roles/user";
    var roleRequest = new HttpRequestMessage(HttpMethod.Get, getRoleUrl);
    roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

    var roleResponse = await _httpClient.SendAsync(roleRequest, cancellationToken);
    if (!roleResponse.IsSuccessStatusCode)
    {
        var error = await roleResponse.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Не удалось получить роль 'user': {error}");
    }
    var roleJson = await roleResponse.Content.ReadAsStringAsync(cancellationToken);
    using var doc = JsonDocument.Parse(roleJson);
    var roleId = doc.RootElement.GetProperty("id").GetString();
    var roleName = doc.RootElement.GetProperty("name").GetString();

    
    var roleMappingUrl = $"{_configuration["Keycloak:Authority"]}/admin/realms/{_configuration["Keycloak:Realm"]}/users/{userId}/role-mappings/realm";
    var roleMappingContent = new StringContent(JsonSerializer.Serialize(new[] {
        new { id = roleId, name = roleName }
    }), Encoding.UTF8, "application/json");

    var roleMappingRequest = new HttpRequestMessage(HttpMethod.Post, roleMappingUrl)
    {
        Content = roleMappingContent
    };
    roleMappingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

    var roleMappingResponse = await _httpClient.SendAsync(roleMappingRequest, cancellationToken);
    if (!roleMappingResponse.IsSuccessStatusCode)
    {
        var error = await roleMappingResponse.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Не удалось назначить роль 'user': {error}");
    }
    
    var userResponseResult = new UserResponse
    {
        Username = request.Username,
        Email = request.Email,
        FirstName = request.FirstName,
        LastName = request.LastName,
        Id = userId,
        TokenResponse = await LoginAsync(new UserLoginRequest { Username = request.Username, Password = request.Password }, cancellationToken)
    };

    return userResponseResult;
}

    public async Task<TokenResponse> LoginAsync(UserLoginRequest request, CancellationToken cancellationToken)
    {
        var tokenUrl =
            $"{_configuration["Keycloak:Authority"]}/realms/{_configuration["Keycloak:Realm"]}/protocol/openid-connect/token";

        var clientId = _configuration["Keycloak:ClientId"];
        var clientSecret = _configuration["Keycloak:ClientSecret"];

        var formData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "grant_type", "password" },
            { "username", request.Username },
            { "password", request.Password }
        };

        var requestContent = new FormUrlEncodedContent(formData);
        var response = await _httpClient.PostAsync(tokenUrl, requestContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Считаем текст ошибки от Keycloak, чтобы понять причину
            var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new UnauthorizedAccessException($"Login failed: {errorResponse}");
        }

        // Если успешный ответ, десериализуем токены
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);

        return tokenResponse;
    }


    public async Task LogoutAsync(string accessToken, string refreshToken, CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            RevokeTokenAsync(refreshToken, "refresh_token", cancellationToken),
            RevokeTokenAsync(accessToken, "access_token", cancellationToken)
        );
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string refreshTokenRequest, CancellationToken cancellationToken)
    {
        var url =
            $"{_configuration["Keycloak:Authority"]}/realms/{_configuration["Keycloak:Realm"]}/protocol/openid-connect/token";
        var clientId = _configuration["Keycloak:ClientId"];
        var clientSecret = _configuration["Keycloak:ClientSecret"];

        var formData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "token", refreshTokenRequest },
            { "grant_type", "refresh_token" }
        };
        
        using var requestContent = new FormUrlEncodedContent(formData);
        using var response = await _httpClient.PostAsync(url, requestContent, cancellationToken);
        
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);
        
        return tokenResponse;
    }

    private async Task RevokeTokenAsync(string token, string tokenTypeHint, CancellationToken cancellationToken)
    {
        var url =
            $"{_configuration["Keycloak:Authority"]}/realms/{_configuration["Keycloak:Realm"]}/protocol/openid-connect/revoke";
        var clientId = _configuration["Keycloak:ClientId"];
        var clientSecret = _configuration["Keycloak:ClientSecret"];

        var formData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "token", token },
            { "token_type_hint", tokenTypeHint }
        };

        using var requestContent = new FormUrlEncodedContent(formData);
        using var response = await _httpClient.PostAsync(url, requestContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new UnauthorizedAccessException(
                $"Failed to revoke {tokenTypeHint}. Status: {response.StatusCode}, Body: {errorBody}");
        }
    }
}