using CosmosRetryConsoleApp.Services;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using CosmosRetryConsoleApp.Config;
using CosmosRetryConsoleApp.Utils;
using CosmosRetryConsoleApp.Sequences;

class Program
{
    static async Task Main(string[] args)
    {
        // Initialize configurations and Cosmos DB client using AppInitializer
        var (cosmosConfig, identityConfig) = await AppInitializer.InitConfigsAsync();

        // Check for the retry count and wait time configurations in InitDatabaseClientAsync if update is needed.
        var dbClient = await AppInitializer.InitDatabaseClientAsync(cosmosConfig, identityConfig);

        // Scenario : Throttling simulation
        Console.WriteLine("Starting throttling simulation...");
        await ThrottlingSequence.RunAsync(dbClient, cosmosConfig.ContainerId);

        // Scenario : Compare Models
        Console.WriteLine("Starting model comparison...");
        await CompareModelsSequence.RunAsync(dbClient);
    }
}