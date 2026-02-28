using System.Text.Json.Serialization;
using Wizard.Api.Endpoints;
using Wizard.Api.Hubs;
using Wizard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<GameFlowOptions>(builder.Configuration.GetSection(GameFlowOptions.SectionName));
builder.Services.AddSingleton<ILobbyService, LobbyService>();

var app = builder.Build();

app.UseCors();

app.MapLobbyEndpoints();

app.MapHub<GameHub>("/hubs/game");

await app.RunAsync();
