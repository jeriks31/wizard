using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Wizard.Api.Contracts;
using Wizard.Game;

namespace Wizard.Api.IntegrationTests;

public sealed class WizardApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WizardApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task StartGame_RejectsWhenNotEnoughPlayers()
    {
        using var client = _factory.CreateClient();
        var hostJoin = await CreateLobbyAsync(client, "Host");
        await using var hostHub = await ConnectHubAsync(hostJoin);

        var errors = new ConcurrentQueue<(string Code, string Message)>();
        hostHub.On<string, string>("ServerError", (code, message) => errors.Enqueue((code, message)));

        await hostHub.InvokeAsync("StartGame");
        await Task.Delay(150);

        Assert.Contains(errors, e => e.Message.Contains("3-6 players", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InvalidReconnectToken_IsRejected()
    {
        using var client = _factory.CreateClient();
        var hostJoin = await CreateLobbyAsync(client, "Host");

        await using var connection = BuildHubConnection();
        await connection.StartAsync();

        var errors = new ConcurrentQueue<(string Code, string Message)>();
        connection.On<string, string>("ServerError", (code, message) => errors.Enqueue((code, message)));

        await connection.InvokeAsync("ConnectToLobby", hostJoin.LobbyCode, hostJoin.PlayerId, "bad-token");
        await Task.Delay(150);

        Assert.Contains(errors, e => e.Message.Contains("Invalid reconnect token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StateUpdated_RedactsOtherPlayersHands()
    {
        using var client = _factory.CreateClient();
        var hostJoin = await CreateLobbyAsync(client, "Alice");
        var bJoin = await JoinLobbyAsync(client, hostJoin.LobbyCode, "Bob");
        var cJoin = await JoinLobbyAsync(client, hostJoin.LobbyCode, "Cara");

        await using var hostHub = await ConnectHubAsync(hostJoin);
        await using var bHub = await ConnectHubAsync(bJoin);
        await using var cHub = await ConnectHubAsync(cJoin);

        var hostUpdates = ListenForState(hostHub);
        var bUpdates = ListenForState(bHub);
        var cUpdates = ListenForState(cHub);
        hostUpdates.Drain();
        bUpdates.Drain();
        cUpdates.Drain();

        await hostHub.InvokeAsync("StartGame");

        var hostEnvelope = await hostUpdates.ReadUntilAsync(x => x.State.Status is "Bidding" or "ChoosingTrump");
        if (hostEnvelope.State.Round?.RequiresDealerTrumpSelection == true)
        {
            var dealer = hostEnvelope.State.Players.Single(x => x.SeatIndex == hostEnvelope.State.Round.DealerSeatIndex);
            var dealerHub = dealer.PlayerId == hostJoin.PlayerId
                ? hostHub
                : dealer.PlayerId == bJoin.PlayerId
                    ? bHub
                    : cHub;
            await dealerHub.InvokeAsync("ChooseTrump", Suit.Spades);
            hostEnvelope = await hostUpdates.ReadUntilAsync(x => x.State.Status == "Bidding");
        }

        Assert.Equal(LobbyStatus.Bidding.ToString(), hostEnvelope.State.Status);
        var hostSelf = hostEnvelope.State.Players.Single(x => x.IsYou);
        Assert.NotNull(hostSelf.Hand);
        Assert.NotEmpty(hostSelf.Hand!);

        var hostOthers = hostEnvelope.State.Players.Where(x => !x.IsYou).ToArray();
        Assert.All(hostOthers, other => Assert.Null(other.Hand));
        Assert.All(hostOthers, other => Assert.True(other.HandCount > 0));

        var bobEnvelope = await bUpdates.ReadUntilAsync(x => x.State.Status == "Bidding");
        var bobSelf = bobEnvelope.State.Players.Single(x => x.IsYou);
        Assert.NotNull(bobSelf.Hand);
        Assert.All(bobEnvelope.State.Players.Where(x => !x.IsYou), other => Assert.Null(other.Hand));

        var caraEnvelope = await cUpdates.ReadUntilAsync(x => x.State.Status == "Bidding");
        var caraSelf = caraEnvelope.State.Players.Single(x => x.IsYou);
        Assert.NotNull(caraSelf.Hand);
        Assert.All(caraEnvelope.State.Players.Where(x => !x.IsYou), other => Assert.Null(other.Hand));
    }

    [Fact]
    public async Task StateUpdated_RevisionsIncreaseAcrossAcceptedCommands()
    {
        using var client = _factory.CreateClient();
        var hostJoin = await CreateLobbyAsync(client, "Host");
        var bJoin = await JoinLobbyAsync(client, hostJoin.LobbyCode, "Bob");
        var cJoin = await JoinLobbyAsync(client, hostJoin.LobbyCode, "Cara");
        await using var hostHub = await ConnectHubAsync(hostJoin);
        var hostUpdates = ListenForState(hostHub);
        await using var bHub = await ConnectHubAsync(bJoin);
        await using var cHub = await ConnectHubAsync(cJoin);
        hostUpdates.Drain();

        await hostHub.InvokeAsync("StartGame");
        var first = await hostUpdates.ReadWithTimeoutAsync();
        var second = await hostUpdates.ReadWithTimeoutAsync();

        Assert.True(second.Revision > first.Revision);
    }

    private ChannelReader<StateUpdatedEnvelope> ListenForState(HubConnection connection)
    {
        var channel = Channel.CreateUnbounded<StateUpdatedEnvelope>();
        connection.On<JsonElement>("StateUpdated", payload =>
        {
            var envelope = JsonSerializer.Deserialize<StateUpdatedEnvelope>(payload.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });
            if (envelope is not null)
            {
                channel.Writer.TryWrite(envelope);
            }
        });
        return channel.Reader;
    }

    private async Task<JoinLobbyResponse> CreateLobbyAsync(HttpClient client, string playerName)
    {
        var response = await client.PostAsJsonAsync("/api/lobbies", new CreateLobbyRequest(playerName));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JoinLobbyResponse>())!;
    }

    private async Task<JoinLobbyResponse> JoinLobbyAsync(HttpClient client, string lobbyCode, string playerName)
    {
        var response = await client.PostAsJsonAsync($"/api/lobbies/{lobbyCode}/join", new JoinLobbyRequest(playerName));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JoinLobbyResponse>())!;
    }

    private async Task<HubConnection> ConnectHubAsync(JoinLobbyResponse join)
    {
        var connection = BuildHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("ConnectToLobby", join.LobbyCode, join.PlayerId, join.SeatToken);
        return connection;
    }

    private HubConnection BuildHubConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, "/hubs/game"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();
    }
}

internal static class ChannelReaderExtensions
{
    public static async Task<T> ReadWithTimeoutAsync<T>(this ChannelReader<T> reader, int timeoutMs = 4_000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        return await reader.ReadAsync(cts.Token);
    }

    public static async Task<T> ReadUntilAsync<T>(this ChannelReader<T> reader, Func<T, bool> predicate, int timeoutMs = 4_000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        while (true)
        {
            var value = await reader.ReadAsync(cts.Token);
            if (predicate(value))
            {
                return value;
            }
        }
    }

    public static void Drain<T>(this ChannelReader<T> reader)
    {
        while (reader.TryRead(out _))
        {
        }
    }
}
