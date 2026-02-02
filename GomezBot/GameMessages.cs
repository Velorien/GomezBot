namespace GomezBot;

interface IGameMessage;

record NickAccepted : IGameMessage;

record RoomsUpdated(IReadOnlyCollection<RoomInfo> Rooms) : IGameMessage;

record RoomInfo(string Name, int Players, int Max, bool HasPassword);

record RoomJoined(string Room) : IGameMessage;

record Error(string Message) : IGameMessage;

record Chat(string Message, string Author) : IGameMessage;

record GameUpdated(
    string Phase,
    BlackCard BlackCard,
    IReadOnlyCollection<WhiteCard> Hand,
    bool IsCzar,
    IReadOnlyCollection<Submission> Submissions,
    bool HasSubmitted,
    ReadyStatus ReadyStatus,
    string RoomName,
    IReadOnlyCollection<Player> PlayersList) : IGameMessage;

record BlackCard(string Text, int Pick);

record WhiteCard(Guid Id, string Text);

record Submission(int Id, string FullText);

record ReadyStatus(int Ready, int Total);

record Player(string Nick, int Score, bool IsCzar);