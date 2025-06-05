using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {

        var config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

        string endpointUri = config.GetValue("CosmosDB:EndpointUri", String.Empty);
        string databaseId = config.GetValue("CosmosDB:DatabaseId", String.Empty);
        string containerId = config.GetValue("CosmosDB:ContainerId", String.Empty);
        string primaryKey = config.GetValue("CosmosDB:PrimaryKey", String.Empty);
        string tenantId = config.GetValue("Identity:TenantId", String.Empty);
        string clientId = config.GetValue("Identity:ClientId", String.Empty);
        string clientKey = config.GetValue("Identity:ClientKey", String.Empty);
        var verTokenCredential = new ClientSecretCredential(tenantId, clientId, clientKey);


        CosmosClientOptions options = new CosmosClientOptions
        {
            // ConnectionMode = ConnectionMode.Gateway, // ou Direct
            // MaxRetryAttemptsOnRateLimitedRequests = 5,
            // MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10)
        };

        CosmosClient client = new CosmosClient(endpointUri, verTokenCredential, options);

        try
        {
            Microsoft.Azure.Cosmos.Database database = await client.CreateDatabaseIfNotExistsAsync(databaseId);
            Microsoft.Azure.Cosmos.Container container = await database.CreateContainerIfNotExistsAsync(containerId, "/id");

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
            // Task[] tasks = new Task[totalRequests];
            List<Func<Task>> tasks = new List<Func<Task>>();
            double parallelRU = 0;

            for (int i = 0; i < totalRequests; i++)
            {
                tasks.Add(async () =>
                {
                    await RunCosmosOperationsAsync(container, ref parallelRU, retryCount);
                });
                var throttler = new SemaphoreSlim(chunkSize);
                var tasksToRun = tasks.Select(async task =>
                {
                    await throttler.WaitAsync();
                    try
                    {
                        await task();
                    }
                    finally
                    {
                        throttler.Release();
                    }
                });
                await Task.WhenAll(tasksToRun);
                // tasks[i] = Task.Run(async () =>
                // {
                //     try
                //     {
                //         var item = new { id = Guid.NewGuid().ToString(), name = "RetryTest", timestamp = DateTime.UtcNow };
                //         var resp = await container.CreateItemAsync(item, new PartitionKey(item.id));
                //         lock (ruLock) { parallelRU += resp.RequestCharge; }
                //     }
                //     catch (CosmosException ex)
                //     {
                //         if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                //         {
                //             retryCount++;
                //             Console.WriteLine($"429 received. RetryAfter: {ex.RetryAfter}");
                //         }
                //         else
                //         {
                //             Console.WriteLine($"CosmosException: {ex.StatusCode} - {ex.Message}");
                //         }
                //     }
                // });
            }
            // await Task.WhenAll(tasks);
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

    private async Task RunCosmosOperationsAsync(Microsoft.Azure.Cosmos.Container container, ref double parallelRU, int retryCount)
    {
        object ruLock = new object();
        try
        {
            var item = new { id = Guid.NewGuid().ToString(), name = "RetryTest", timestamp = DateTime.UtcNow };
            var resp = await container.CreateItemAsync(item, new PartitionKey(item.id));
            lock (ruLock) { parallelRU += resp.RequestCharge; }
        }
        catch (CosmosException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                retryCount++;
                Console.WriteLine($"429 received. RetryAfter: {ex.RetryAfter}");
            }
            else
            {
                Console.WriteLine($"CosmosException: {ex.StatusCode} - {ex.Message}");
            }
        }

    }
}