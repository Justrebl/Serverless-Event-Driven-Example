using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading.Tasks;

using Container = Microsoft.Azure.Cosmos.Container;

class Program
{
    static async Task Main(string[] args)
    {


        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var cosmosConfig = config.GetSection("CosmosDB").Get<CosmosRetryConsoleApp.Config.CosmosDBConfig>()!;
        var identityConfig = config.GetSection("Identity").Get<CosmosRetryConsoleApp.Config.IdentityConfig>()!;

        CosmosClient client = new CosmosClient(
            accountEndpoint: cosmosConfig.EndpointUri,
            tokenCredential: new ClientSecretCredential(identityConfig.TenantId, identityConfig.ClientId, identityConfig.ClientKey),
            clientOptions: new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway, // or Direct
                MaxRetryAttemptsOnRateLimitedRequests = 10, // Number of retries on 429 Too Many Requests (default 9)
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30), // Maximum wait time for cumulative retries (default 30 seconds)
                ConsistencyLevel = ConsistencyLevel.Session // or Eventual, Strong, BoundedStaleness, etc.
            });

        try
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(cosmosConfig.DatabaseId);
            Container container = await database.CreateContainerIfNotExistsAsync(cosmosConfig.ContainerId, "/id");

            var testItem = new { id = Guid.NewGuid().ToString(), name = "RetryTest", timestamp = DateTime.UtcNow };
            var response = await container.CreateItemAsync(testItem, new PartitionKey(testItem.id));
            Console.WriteLine($"Item created. RU consumed: {response.RequestCharge}");

            double totalRU = response.RequestCharge;

            // Update the document
            testItem = new { id = testItem.id, name = "RetryTest-Updated", timestamp = DateTime.UtcNow };
            response = await container.UpsertItemAsync(testItem, new PartitionKey(testItem.id));
            totalRU += response.RequestCharge;
            Console.WriteLine($"Item updated. RU consumed: {response.RequestCharge}");

            // Simulate throttling by running many requests in parallel
            int retryCount = 0;
            int totalRequests = 1000;
            int chunkSize = 100; // Adjust chunk size as needed
            List<Func<Task>> tasks = new List<Func<Task>>();
            double parallelRU = 0;
            object countLock = new object();



            var throttler = new SemaphoreSlim(chunkSize);
            var tasksToRun = Enumerable.Range(0, totalRequests)
               .Select(async i =>
               {
                   await throttler.WaitAsync();
                   ItemResponse<Item> itemResponse = null;

                   try
                   {
                       do
                       {
                           try
                           {
                               itemResponse = await RunCosmosOperationsAsync(container);
                               lock (countLock) { parallelRU += itemResponse.RequestCharge; }
                           }
                           catch (CosmosException ex)
                           {
                               if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                               {
                                   lock (countLock) { retryCount++; }
                                   Console.WriteLine($"429 received. RetryAfter: {ex.RetryAfter}");
                               }
                               else
                               {
                                   Console.WriteLine($"CosmosException: {ex.StatusCode} - {ex.Message}");
                               }
                           }
                       } while (itemResponse == null);
                   }
                   finally
                   {
                       throttler.Release();

                   }
               }).ToList();

            await Task.WhenAll(tasksToRun);

            Console.WriteLine($"Total retries due to 429: {retryCount}");
            Console.WriteLine($"Total RU consumed (single + update): {totalRU}");
            Console.WriteLine($"Total RU consumed (parallel inserts): {parallelRU}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task<ItemResponse<Item>> RunCosmosOperationsAsync(Container container)
    {
        var item = new Item() { Id = Guid.NewGuid().ToString(), Name = "RetryTest", Timestamp = DateTime.UtcNow };
        var resp = await container.CreateItemAsync(item, new PartitionKey(item.Id));
        return resp;
    }
}