namespace GomezBot;

class Bot(string nick, GameClient client, IGameActionStrategy gameActionStrategy)
{
    private readonly TaskCompletionSource gameEnd = new();
    private bool roomJoined, whiteCardsSelected, winnerSelected, setReady;

    public async Task Play()
    {
        await client.Connect();
        client.StartListening(OnMessage, OnListeningCompleted);

        await SetNick();

        await gameEnd.Task;
    }

    private async Task OnMessage(IGameMessage? message)
    {
        if (message is GameUpdated gu)
        {
            await Play(gu);
        }
        else if (message is RoomListUpdated rlu)
        {
            await JoinRoom(rlu);
        }
        else if (message is RoomJoined)
        {
            roomJoined = true;
        }
        else if (message is Error)
        {
            await EndGame();
        }
    }

    private Task OnListeningCompleted() => EndGame();

    private Task SetNick() => client.SetNick(nick);

    private async Task JoinRoom(RoomListUpdated roomList)
    {
        if (roomJoined) return;

        var roomToJoin = roomList.Rooms.FirstOrDefault(x => !x.HasPassword && x.Players < x.Max);

        if (roomToJoin is not null)
        {
            await client.JoinRoom(roomToJoin.Name);
        }
    }

    private Task SetReady() => client.SetReady();

    private async Task Play(GameUpdated gameState)
    {
        var task = gameState switch
        {
            { Phase: "SELECTING", IsCzar: false, HasSubmitted: false, Hand.Count: not 0 } when !whiteCardsSelected => PickWhiteCards(gameState),
            { Phase: "JUDGING", IsCzar: true } when !winnerSelected => SelectWinner(gameState),
            { Phase: "SUMMARY" } when !setReady => EndTurn(gameState),
            { Phase: "GAME_OVER" } => EndGame(),
            _ => Task.CompletedTask
        };

        await task;
    }

    private async Task PickWhiteCards(GameUpdated gameState)
    {
        setReady = false;
        whiteCardsSelected = true;
        var (selection, comment) = await gameActionStrategy.SelectWhiteCards(gameState);
        await client.SubmitCards(selection);
        if (comment.Message is not null)
        {
            await client.SendChatMessage(comment.Message);
        }
    }

    private async Task SelectWinner(GameUpdated gameState)
    {
        if (gameState.Submissions.Count == gameState.PlayersList.Count - 1)
        {
            setReady = false;
            winnerSelected = true;
            var (winner, comment) = await gameActionStrategy.SelectWinner(gameState);
            await client.PickWinner(winner);
            if (comment is not null)
            {
                await client.SendChatMessage(comment);
            }
        }
    }

    private async Task EndTurn(GameUpdated gameState)
    {
        setReady = true;
        winnerSelected = false;
        whiteCardsSelected = false;
        var comment = await gameActionStrategy.GetEndTurnComment(gameState, nick);
        if (comment.Message is not null)
        {
            await client.SendChatMessage(comment.Message);
        }

        await SetReady();
    }

    private Task EndGame()
    {
        if (!gameEnd.Task.IsCompleted) gameEnd.SetResult();
        return Task.CompletedTask;
    }
}