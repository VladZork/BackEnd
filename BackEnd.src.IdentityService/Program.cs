using System.Net.Http.Headers;
using BackEnd.src.IdentityService.Controllers;
using BackEnd.src.IdentityService.interfaces;
using BackEnd.src.IdentityService.Services;
using Microsoft.OpenApi.Models;
using IdentityModel.AspNetCore.OAuth2Introspection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = OAuth2IntrospectionDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OAuth2IntrospectionDefaults.AuthenticationScheme;
    })
    .AddOAuth2Introspection(options =>
    {
        options.Authority =
            $"{builder.Configuration["Keycloak:Authority"]}/realms/{builder.Configuration["Keycloak:Realm"]}";
        options.ClientId = builder.Configuration["Keycloak:ClientId"];
        options.ClientSecret = builder.Configuration["Keycloak:ClientSecret"];

        options.IntrospectionEndpoint =
            $"{builder.Configuration["Keycloak:Authority"]}/realms/{builder.Configuration["Keycloak:Realm"]}/protocol/openid-connect/token/introspect";

        options.EnableCaching = false;
    });

builder.Services.AddScoped<IIdentityService, KeycloakIdentityService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddHttpClient<KeycloakIdentityService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Keycloak:Authority"]);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddOpenApi();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Keycloak Identity Service API",
        Version = "v1",
        Description = "API для взаимодействия с Keycloak Identity Service",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "Введите токен в формате Bearer {your token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});


var app = builder.Build();

app.MapUserApi();
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Service API v1"); });


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.UseHttpsRedirection();


app.Run();