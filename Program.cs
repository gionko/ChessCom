// See https://aka.ms/new-console-template for more information
using ChessCom;

Console.WriteLine("ChessCom Scratch Test - Gionko ;)");

ChessApi.StartRequestProcessor();
var api = new ChessApi.Api("https://api.chess.com/pub/country/EU/players");
ChessApi.Enqueue(api);
api = new ChessApi.Api("https://api.chess.com/pub/country/US/players");
ChessApi.Enqueue(api);
api = new ChessApi.Api("https://api.chess.com/pub/country/IT/players");
ChessApi.Enqueue(api);


while (true)
{
    Console.Clear();
    Console.WriteLine("ChessCom Scratch Test - Gionko ;)");
    Console.WriteLine();

    Console.WriteLine("Richieste non ancora processate: " + Stats.GetQueueCount());
    Console.WriteLine("Richieste processate: " + Stats.GetProcessedCount());
    Console.WriteLine("Too many request: " + Stats.GetTooManyConnectionCount());
    Console.WriteLine("Tempo medio di richiesta HTTP: " + Stats.GetHTTPExecutionTime().ToString("0.##") + "ms");

    Thread.Sleep(500);
}
