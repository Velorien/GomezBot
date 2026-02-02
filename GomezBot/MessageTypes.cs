namespace GomezBot;

static class MessageTypes
{
    public const string NickAccepted = "NICK_OK";
    public const string RoomsUpdated = "ROOM_LIST";
    public const string RoomJoined = "JOIN_ROOM_OK";
    public const string GameUpdated = "GAME_UPDATE";
    public const string Error = "ERROR";
    public const string Chat = "CHAT";
    
    public const string SetNick = "SET_NICK";
    public const string JoinRoom = "JOIN_ROOM";
    public const string SetReady = "PLAYER_READY";
    public const string SubmitCards = "SUBMIT_CARDS";
    public const string PickWinner = "PICK_WINNER";
    public const string SendChatMessage = "CHAT_MSG";
}