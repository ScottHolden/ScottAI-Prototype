using Microsoft.Extensions.Hosting;

namespace ScottAIPrototype;
public class ScottAIService(ScottAI scottAi) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await scottAi.RunAsync(stoppingToken);
}