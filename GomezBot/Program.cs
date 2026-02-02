using GomezBot;
using OllamaSharp;

var ollamaClient = new OllamaApiClient("http://localhost:11434", "SpeakLeash/bielik-11b-v3.0-instruct:Q4_K_M");

while (true)
{
    try
    {
        using var client = new GameClient();
        var bot = new Bot("Zemog", client, new AiGameActionStrategy(ollamaClient));
        await bot.Play();
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
    
    await Task.Delay(1000);
    Console.WriteLine("Retrying connection");
}