using System.Text.Json;
using OllamaSharp;
using OllamaSharp.Models;

namespace GomezBot;

interface IGameActionStrategy
{
    Task<WinnerSelection> SelectWinner(GameUpdated gameState);

    Task<WhiteCardsSelection> SelectWhiteCards(GameUpdated gameState);

    Task<Anecdote> GetEndTurnComment(GameUpdated gameState, string name);
}

record WinnerSelection(int Index, string? Comment);

record WhiteCardsSelection(IReadOnlyCollection<string> CardIds, Anecdote Anecdote);

record Anecdote(string? Message);

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
            new Anecdote(null)));

    public Task<Anecdote> GetEndTurnComment(GameUpdated gameState, string name) => Task.FromResult(new Anecdote(null));
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
        Teraz twoja kolej, żeby rzucić białą kartę. Wybierz najlepsze twoim zdaniem karty białe.
        Czarna karta wskazuje, ile białych kart musisz wybrać.
        Odpowiedz w json o następującej strukturze:
        {{ cardIds: [<id wybranych kart białych>] }}

        Oto czarna karta:
        {0}

        Oto twoje karty białe:
        {1}
        """;

    private const string AnecdoteTemplate =
        """
        Napisz jednozdaniową anegdotę ze świata gry Gothic w formie żartu.
        Odpowiedz w formacie json: {{ message: <twoja anegdota/żart> }}
        Niech nawiązuje do następującej karty:
        
        {0}
        
        Przypomnę Ci też, to to jest ten cały Gothic. To klasyczna gra komputerowa i są w niej takie rzeczy: 
        
        Postaci ze świata gry Gothic:
        
        Bezimienny Bohater – protagonista serii
        Xardas – nekromanta, były Mag Ognia
        Król Rhobar II – władca Myrtany (poza Barierą, ale kluczowy fabularnie)
        
        Stary Obóz
        
        Gomez – przywódca Starego Obozu
        Diego – cień, pierwszy przewodnik bohatera
        Milten – Mag Ognia
        Thorus – strażnik obozu
        Saturas – Mag Wody (początkowo związany ze Starym Obozem)
        
        Nowy Obóz
        
        Lares – członek Strażników Wody
        Lee – dowódca najemników
        Gorn – wojownik, przyjaciel bohatera
        Cronos – Mag Wody

        Obóz Bractwa (Śniący)
        
        Y’Berion – guru Bractwa
        Cor Kalom – alchemik, fanatyk Śniącego
        Lester – nowicjusz, przyjaciel bohatera
        
        Główne obszary gry Gothic
        
        Kolonia karna (Bariera) – cały świat Gothic 1
        Stary Obóz – centrum handlu i władzy
        Nowy Obóz – obóz buntowników
        Obóz Bractwa – sekta Śniącego
        Inne ważne miejsca
        Stara Kopalnia
        Wolna Kopalnia
        Kopalnia Orków
        Świątynia Śniącego
        Las Trolli
        Góry i kaniony Kolonii
        Zatopiona Wieża Xardasa
        
        Stwory i potwory

        Zwierzęta i bestie

        Ścierwojad
        Kretoszczur
        Wilk
        Cieniostwór
        Troll
        Błotny wąż
        Potwory
        Ork (wojownik, szaman, elitarny)
        Demon
        Harpiа
        Topielec
        Szkielet
        Zombie
        
        Bossowie / unikalne

        Śniący
        Demony przywoływane w Świątyni
        Elitarni orkowie strażniczy
        """;

    private const string EndTurnCommentTemplate =
        """
        Nazywasz się {2}. Gramy razem w grę karcianą, która polega na wytypowaniu najzabawniejszej kombinacji karty czarnej z propozycjami graczy na kartach białych.
        Podstawiasz tekst z białej karty (lub kart) w wolne pole na karcie czarnej uzyskując kombinację.
        Oto zwycięska kombinacja oraz autor:
        
        {1}
        
        """ + AnecdoteTemplate;

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
        string? anecdote = null;

        if (Random.Shared.NextDouble() < 0.25)
        {
            anecdote = await GenerateResponse(AnecdoteTemplate);
        }

        try
        {
            var json = JsonDocument.Parse(result);
            var typedAnectdote = anecdote is null ? new Anecdote(null): JsonSerializer.Deserialize<Anecdote>(anecdote);
            
            var cardIds = json.RootElement.GetProperty("cardIds").EnumerateArray().Select(x => x.GetString()).ToArray();
            return new(cardIds, typedAnectdote);
        }
        catch (JsonException)
        {
            return new(gameState.Hand.Take(gameState.BlackCard.Pick).Select(x => x.Id).ToArray(), new Anecdote(null));
        }
    }

    public async Task<Anecdote> GetEndTurnComment(GameUpdated gameState, string name)
    {
        var formattedPrompt = string.Format(
            EndTurnCommentTemplate,
            gameState.BlackCard,
            gameState.Submissions.FirstOrDefault(x => x.IsWinner is true),
            name);
        
        try
        {
            var result = await GenerateResponse(formattedPrompt);
            return JsonSerializer.Deserialize<Anecdote>(result) ?? new Anecdote(null);
        }
        catch
        {
            return new Anecdote(null);
        }
    }

    private async Task<string> GenerateResponse(string prompt, string format = "json")
    {
        var stream = ollamaClient.GenerateAsync(new GenerateRequest
        {
            Prompt = prompt,
            Format = format,
            Stream = false
        });

        var response = await stream.FirstAsync() as GenerateDoneResponseStream;
        Console.WriteLine($"Processed {response!.PromptEvalCount} tokens");
        return response.Response;
    }
}