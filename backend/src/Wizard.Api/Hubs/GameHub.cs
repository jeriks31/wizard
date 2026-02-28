using Microsoft.AspNetCore.SignalR;
using Wizard.Api.Services;
using Wizard.Game;

namespace Wizard.Api.Hubs;

public sealed class GameHub : Hub
{
    private readonly ILobbyService _lobbyService;

    public GameHub(ILobbyService lobbyService)
    {
        _lobbyService = lobbyService;
    }

    public async Task ConnectToLobby(string lobbyCode, string playerId, string seatToken)
    {
        await ExecuteOrSendError(async () =>
        {
            await _lobbyService.ConnectToLobbyAsync(lobbyCode, playerId, seatToken, Context.ConnectionId);
        });
    }

    public async Task StartGame()
    {
        await ExecuteOrSendError(async () => { await _lobbyService.StartGameAsync(Context.ConnectionId); });
    }

    public async Task ChooseTrump(Suit trumpSuit)
    {
        await ExecuteOrSendError(async () => { await _lobbyService.ChooseTrumpAsync(Context.ConnectionId, trumpSuit); });
    }

    public async Task SubmitBid(int roundNumber, int bid)
    {
        await ExecuteOrSendError(async () => { await _lobbyService.SubmitBidAsync(Context.ConnectionId, roundNumber, bid); });
    }

    public async Task PlayCard(int roundNumber, int trickNumber, string cardId)
    {
        await ExecuteOrSendError(async () => { await _lobbyService.PlayCardAsync(Context.ConnectionId, roundNumber, trickNumber, cardId); });
    }

    public async Task GetState()
    {
        await ExecuteOrSendError(async () => { await _lobbyService.SendCurrentStateToCallerAsync(Context.ConnectionId); });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _lobbyService.HandleDisconnectAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task ExecuteOrSendError(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            await Clients.Caller.SendAsync("ServerError", ex.GetType().Name, ex.Message);
        }
    }
}
