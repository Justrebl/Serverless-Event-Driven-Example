using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using CosmosRetryConsoleApp.Config;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CosmosRetryConsoleApp.Utils
{
    public static class AppInitializer
    {
        public static async Task<(CosmosDBConfig cosmosConfig, IdentityConfig identityConfig)> InitConfigsAsync()
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
}
