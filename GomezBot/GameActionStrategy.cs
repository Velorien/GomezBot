using System.Text.Json;
using OllamaSharp;
using OllamaSharp.Models;

namespace GomezBot;

interface IGameActionStrategy
{
    Task<WinnerSelection> SelectWinner(GameUpdated gameState);

    Task<WhiteCardsSelection> SelectWhiteCards(GameUpdated gameState);
}

record WinnerSelection(int Index, string? Comment);

record WhiteCardsSelection(IReadOnlyCollection<Guid> CardIds, string? Comment);

class RandomGameActionStrategy : IGameActionStrategy
{
    public Task<WinnerSelection> SelectWinner(GameUpdated gameState) =>
        Task.FromResult(new WinnerSelection(gameState.Submissions.ElementAt(Random.Shared.Next() % gameState.Submissions.Count).Id, null));

    public Task<WhiteCardsSelection> SelectWhiteCards(GameUpdated gameState) =>
        Task.FromResult(new WhiteCardsSelection(
            gameState.Hand
                .OrderBy(_ => Random.Shared.Next())
                .Take(gameState.BlackCard.Pick)
                .Select(x => x.Id)
                .ToArray(),
            null));
}

class AiGameActionStrategy(IOllamaApiClient ollamaClient) : IGameActionStrategy
{
    private const string WinnerSelectionPromptTemplate =
        """
        Jesteś graczem w grze karcianej, która polega na wytypowaniu najzabawniejszej kombinacji karty czarnej z propozycjami graczy na kartach białych.
        Podstawiasz tekst z białej karty (lub kart) w wolne pole na karcie czarnej uzyskując kombinację.
        Teraz twoja kolej, żeby wybierać zwycięzcę. Skomentuj ironicznie wybrane karty i wybierz najśmiesznieją.
        Odpowiedz w json o następującej strukturze:
        {{ winner: <id zwycięzcy>, comment: <twój komentarz> }}

        Oto czarna karta:
        {0}

        Oto karty białe:
        {1}
        """;

    private const string WhiteCardsSelectionPromptTemplate =
        """
        Jesteś graczem w grze karcianej, która polega na wytypowaniu najzabawniejszej kombinacji karty czarnej z propozycjami graczy na kartach białych.
        Podstawiasz tekst z białej karty (lub kart) w wolne pole na karcie czarnej uzyskując kombinację.
        Teraz twoja kolej, żeby rzucić białą kartę. Dobierz najlepsze twoim zdaniem karty białe i rzuć śmiesznym komentarzem na temat karty czarnej.
        W swoim zabawnym komentarzu NIE UJAWNIAJ, ani nie nawiązuj do białych kart. Komentarz ma się skupiać wyłącznie na podanym SZABLONIE karty czarnej.
        Czarna karta wskazuje, ile białych kart musisz wybrać.
        Odpowiedz w json o następującej strukturze:
        {{ cardIds: [<id wybranych kart białych>], comment: <twój komentarz na temat karty czarnej> }}

        Oto czarna karta:
        {0}

        Oto twoje karty białe:
        {1}
        """;

    public async Task<WinnerSelection> SelectWinner(GameUpdated gameState)
    {
        var formattedPrompt = string.Format(
            WinnerSelectionPromptTemplate,
            JsonSerializer.Serialize(gameState.BlackCard),
            JsonSerializer.Serialize(gameState.Submissions));

        var result = await GenerateResponse(formattedPrompt);

        try
        {
            var json = JsonDocument.Parse(result);
            var winner = json.RootElement.GetProperty("winner").GetInt32();
            var comment = json.RootElement.GetProperty("comment").GetString();
            return new(winner, comment);
        }
        catch (JsonException)
        {
            return new(0, null);
        }
    }

    public async Task<WhiteCardsSelection> SelectWhiteCards(GameUpdated gameState)
    {
        var formattedPrompt = string.Format(
            WhiteCardsSelectionPromptTemplate,
            JsonSerializer.Serialize(gameState.BlackCard),
            JsonSerializer.Serialize(gameState.Hand));

        var result = await GenerateResponse(formattedPrompt);

        try
        {
            var json = JsonDocument.Parse(result);
            var cardIds = json.RootElement.GetProperty("cardIds").EnumerateArray().Select(x => x.GetGuid()).ToArray();
            var comment = json.RootElement.GetProperty("comment").GetString();
            return new(cardIds, comment);
        }
        catch (JsonException)
        {
            return new(gameState.Hand.Take(gameState.BlackCard.Pick).Select(x => x.Id).ToArray(), null);
        }
    }

    private async Task<string> GenerateResponse(string prompt)
    {
        var stream = ollamaClient.GenerateAsync(new GenerateRequest
        {
            Prompt = prompt,
            Format = "json",
            Stream = false
        });

        var response = await stream.FirstAsync();
        return response.Response;
    }
}