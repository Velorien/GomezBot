using GomezBot.SelectionStrategy;

namespace GomezBot;

class Bot(string nick, GameClient client, ISelectionStrategy selectionStrategy)
{
    private readonly TaskCompletionSource tcs = new();
    private RoomsUpdated? rooms;
    private bool whiteCardsSelected, winnerSelected, setReady;

    public async Task Start()
    {
        await client.Connect();
        client.StartListening(OnMessage, OnListeningCompleted);

        await SetNick();
        await JoinRoom();
        await SetReady();

        await tcs.Task;
    }

    private async Task OnMessage(IGameMessage message)
    {
        if (message is GameUpdated gu)
        {
            await Play(gu);
        }
        else if (message is RoomsUpdated ru)
        {
            rooms = ru;
        }
        else if (message is Error)
        {
            tcs.SetResult();
        }
    }

    private void OnListeningCompleted() => tcs.SetResult();

    private Task SetNick() => client.SetNick(nick);

    private async Task JoinRoom()
    {
        while (true)
        {
            await Task.Delay(500);

            var roomToJoin = rooms?.Rooms.FirstOrDefault(x => !x.HasPassword && x.Players < x.Max);

            if (roomToJoin is not null)
            {
                await client.JoinRoom(roomToJoin.Name);
                return;
            }
        }
    }

    private Task SetReady() => client.SetReady();

    private async Task Play(GameUpdated gameState)
    {
        var task = gameState switch
        {
            { Phase: "SELECTING", HasSubmitted: false, Hand.Count: not 0 } when !whiteCardsSelected => PickWhiteCards(gameState),
            { Phase: "JUDGING", IsCzar: true } when !winnerSelected => SelectWinner(gameState),
            { Phase: "SUMMARY" } when !setReady => EndTurn(),
            { Phase: "GAME_OVER" } => EndGame(),
            _ => Task.CompletedTask
        };

        await task;
    }

    private async Task PickWhiteCards(GameUpdated gameState)
    {
        setReady = false;
        whiteCardsSelected = true;
        var (selection, comment) = await selectionStrategy.SelectWhiteCards(gameState);
        await client.SubmitCards(selection);
        if (comment is not null)
        {
            await client.SendChatMessage(comment);
        }
    }

    private async Task SelectWinner(GameUpdated gameState)
    {
        if (gameState.Submissions.Count == gameState.PlayersList.Count - 1)
        {
            setReady = false;
            winnerSelected = true;
            var (winner, comment) = await selectionStrategy.SelectWinner(gameState);
            await client.PickWinner(winner);
            if (comment is not null)
            {
                await client.SendChatMessage(comment);
            }
        }
    }

    private async Task EndTurn()
    {
        setReady = true;
        winnerSelected = false;
        whiteCardsSelected = false;
        await SetReady();
    }
    
    private Task EndGame()
    {
        tcs.SetResult();
        return Task.CompletedTask;
    }
}