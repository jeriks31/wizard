using System.Threading.Channels;
using Wizard.Game;

namespace Wizard.Api.Services;

internal sealed class LobbyActor
{
    private readonly Channel<QueuedWork> _queue = Channel.CreateUnbounded<QueuedWork>();
    private readonly CancellationTokenSource _cts = new();

    public LobbyActor(LobbyState state)
    {
        State = state;
        _ = Task.Run(ProcessLoopAsync);
    }

    public LobbyState State { get; }

    public async Task<T> RunAsync<T>(Func<LobbyState, T> action)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = _queue.Writer.TryWrite(new QueuedWork(s => action(s), tcs));
        if (!accepted)
        {
            throw new InvalidOperationException("Failed to enqueue lobby command.");
        }

        var result = await tcs.Task;
        return result is null ? default! : (T)result;
    }

    private async Task ProcessLoopAsync()
    {
        await foreach (var work in _queue.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                var result = work.Action(State);
                work.Completion.SetResult(result);
            }
            catch (Exception ex)
            {
                work.Completion.SetException(ex);
            }
        }
    }

    private sealed record QueuedWork(
        Func<LobbyState, object?> Action,
        TaskCompletionSource<object?> Completion);
}
