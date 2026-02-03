using System.Net.WebSockets;
using System.Text.Json;

namespace GomezBot;

class GameClient : IDisposable
{
    private readonly byte[] buffer = new byte[1024 * 4];
    private readonly CancellationTokenSource cts = new();
    private readonly MemoryStream memoryStream = new();
    private ClientWebSocket connection = default!;

    private JsonSerializerOptions serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task Connect()
    {
        connection = new ClientWebSocket();
        await connection.ConnectAsync(new Uri("ws://localhost:2137/ws"), cts.Token);
    }

    public void StartListening(Func<IGameMessage, Task> messageReceived, Action connectionClosed)
    {
        _ = Listen(messageReceived, connectionClosed);
    }

    public void Stop() => cts.Cancel();

    public Task SetNick(string nickname) => SendMessage(new { type = MessageTypes.SetNick, nickname }, cts.Token);

    public Task JoinRoom(string room) => SendMessage(new { type = MessageTypes.JoinRoom, name = room, password = string.Empty }, cts.Token);

    public Task SetReady() => SendMessage(new { type = MessageTypes.SetReady }, cts.Token);

    public Task SubmitCards(IReadOnlyCollection<Guid> cardIds) => SendMessage(new { type = MessageTypes.SubmitCards, cards = cardIds }, cts.Token);

    public Task PickWinner(int id) => SendMessage(new { type = MessageTypes.PickWinner, index = id }, cts.Token);

    public Task SendChatMessage(string message) => SendMessage(new { type = MessageTypes.SendChatMessage, message }, cts.Token);

    private async Task Listen(Func<IGameMessage, Task> messageReceived, Action doneListening)
    {
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var message = await ReceiveMessage(cts.Token);
                await messageReceived(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }

        doneListening.Invoke();
    }

    private async Task<IGameMessage> ReceiveMessage(CancellationToken token)
    {
        bool readToEnd;
        var memory = new Memory<byte>(buffer);
        memoryStream.SetLength(0);

        do
        {
            var result = await connection.ReceiveAsync(memory, token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return new Error("Connection closed");
            }

            readToEnd = result.EndOfMessage;
            memoryStream.Write(buffer, 0, result.Count);
        } while (!readToEnd);

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var message = await JsonDocument.ParseAsync(memoryStream, default, token);
        var type = message.RootElement.GetProperty("type").GetString();

        IGameMessage? typedMessage = type switch
        {
            MessageTypes.NickAccepted => new NickAccepted(),
            MessageTypes.Chat => message.Deserialize<Chat>(serializerOptions),
            MessageTypes.Error => message.Deserialize<Error>(serializerOptions),
            MessageTypes.RoomJoined => message.Deserialize<RoomJoined>(serializerOptions),
            MessageTypes.GameUpdated => message.Deserialize<GameUpdated>(serializerOptions),
            MessageTypes.RoomsUpdated => message.Deserialize<RoomsUpdated>(serializerOptions),
            MessageTypes.LobbyPlayers => message.Deserialize<LobbyPlayers>(serializerOptions),
            _ => throw new NotImplementedException(type)
        };

        Console.WriteLine("Received message: {0}/{1}", type, typedMessage!.GetType().Name);
        return typedMessage;
    }

    private async Task SendMessage<T>(T message, CancellationToken token)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message);
        await connection.SendAsync(json, WebSocketMessageType.Text, true, token);
    }

    public void Dispose()
    {
        cts.Dispose();
        connection.Dispose();
        memoryStream.Dispose();
    }
}