using Wizard.Api.Contracts;
using Wizard.Api.Services;

namespace Wizard.Api.Endpoints;

public static class LobbyEndpoints
{
    public static IEndpointRouteBuilder MapLobbyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/lobbies", async (CreateLobbyRequest request, ILobbyService lobbyService) =>
        {
            try
            {
                var response = await lobbyService.CreateLobbyAsync(request.PlayerName);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new ErrorResponse("CreateFailed", ex.Message));
            }
        });

        app.MapPost("/api/lobbies/{lobbyCode}/join", async (string lobbyCode, JoinLobbyRequest request, ILobbyService lobbyService) =>
        {
            try
            {
                var response = await lobbyService.JoinLobbyAsync(lobbyCode, request.PlayerName);
                return Results.Ok(response);
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
            {
                return Results.BadRequest(new ErrorResponse("JoinFailed", ex.Message));
            }
        });

        app.MapGet("/api/lobbies/{lobbyCode}", async (string lobbyCode, ILobbyService lobbyService) =>
        {
            try
            {
                var lobby = await lobbyService.GetLobbySnapshotAsync(lobbyCode);
                return Results.Ok(lobby);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ErrorResponse("LobbyNotFound", ex.Message));
            }
        });

        return app;
    }
}
