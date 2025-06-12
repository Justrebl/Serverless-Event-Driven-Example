using CosmosRetryConsoleApp.Services;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using CosmosRetryConsoleApp.Config;
using CosmosRetryConsoleApp.Sequences;

// Left in Program.cs for clarity and context
public static class AppInitializer
{
    public static (CosmosDBConfig cosmosConfig, IdentityConfig identityConfig) InitConfigsAsync()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var cosmosConfig = config.GetSection("CosmosDB").Get<CosmosDBConfig>()!;
        var identityConfig = config.GetSection("Identity").Get<IdentityConfig>()!;

        return (cosmosConfig, identityConfig);
    }

    public static async Task<Database> InitDatabaseClientAsync(CosmosDBConfig cosmosConfig, IdentityConfig identityConfig)
    {
        var client = new CosmosClient(
            accountEndpoint: cosmosConfig.EndpointUri,
            tokenCredential: new ClientSecretCredential(identityConfig.TenantId, identityConfig.ClientId, identityConfig.ClientKey),
            clientOptions: new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway, // or Direct
                MaxRetryAttemptsOnRateLimitedRequests = Const.DefaultMaxRetries,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(Const.DefaultMaxWaitTimeSeconds),
                // ConsistencyLevel = ConsistencyLevel.Session // or Eventual, Strong, BoundedStaleness, etc.
            });

        var dbResponse = await client.CreateDatabaseIfNotExistsAsync(cosmosConfig.DatabaseId);
        return dbResponse.Database;
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // Initialize configurations and Cosmos DB client using AppInitializer
        var (cosmosConfig, identityConfig) = AppInitializer.InitConfigsAsync();

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