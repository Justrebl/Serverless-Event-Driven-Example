using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CosmosRetryConsoleApp.Sequences
{
    public static class ThrottlingSequence
    {
        public const int TOTAL_PARALLEL_REQUESTS = 1000;
        public const int CHUNK_SIZE = 100; // Adjust chunk size as needed
        public const int MAX_RETRIES = 10; // Maximum number of retries for throttled requests
        public const int MAX_WAIT_TIME_SECONDS = 30; // Maximum wait time for retries

        public static async Task<(double ruSingleCreateCount, double ruSingleUpdateCount, int failedRetryCount, double parallelRUCount)> RunAsync(Database database, string containerName)
        {
            Container container = await database.CreateContainerIfNotExistsAsync(containerName, "/id");

            // Default Item 
            Item item = new Item()
            {
                Id = Guid.NewGuid().ToString(),
                Name = "RetryTest",
                Timestamp = DateTime.UtcNow
            };

            // Execute single item write (Create and then Upsert) operation with retry logic
            var (ruSingleCreateCount, ruSingleUpdateCount) = await CountSingleUpsertOperationRUAsync(container, item);

            // Execute parallel inserts with retry logic and logging failed throttled request due to too many retries
            var (failedRetryCount, parallelRUCount) = await SimulateThrottlingAsync(container, item, CHUNK_SIZE, TOTAL_PARALLEL_REQUESTS, MAX_RETRIES);

            // Output the simulation results
            Console.WriteLine($"Total requests failed even after retries due to {MAX_RETRIES} 429: {failedRetryCount}");
            Console.WriteLine($"Total RU consumed (create): {ruSingleCreateCount}");
            Console.WriteLine($"Total RU consumed (update): {ruSingleUpdateCount}");
            Console.WriteLine($"Total RU consumed ({TOTAL_PARALLEL_REQUESTS} parallel inserts): {parallelRUCount}");
            return (ruSingleCreateCount, ruSingleUpdateCount, failedRetryCount, parallelRUCount);
        }

        private static async Task<(double ruSingleCreateCount, double ruSingleUpdateCount)> CountSingleUpsertOperationRUAsync(Container container, Item item)
        {
            double ruSingleCreateCount = 0;
            double ruSingleUpdateCount = 0;
            ItemResponse<Item> response;
            try
            {
                item.Id = Guid.NewGuid().ToString(); // Ensure unique ID for each item 
                response = await CreateItemInCosmosAsync(container, item);
                ruSingleCreateCount = response.RequestCharge;
                Console.WriteLine($"Item created. RU consumed: {response.RequestCharge}");
                response = await UpdateItemInCosmosAsync(container, response.Resource);
                ruSingleUpdateCount = response.RequestCharge;
                Console.WriteLine($"Item updated. RU consumed: {response.RequestCharge}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
            return (ruSingleCreateCount, ruSingleUpdateCount);
        }

        private static async Task<(int failedRetryCount, double parallelRUCount)> SimulateThrottlingAsync(Container container, Item item, int chunkSize, int totalParallelRequests, int maxRetries)
        {
            var throttler = new SemaphoreSlim(chunkSize);
            object countLock = new object(); // Lock for thread-safe access to counters
            int failedRetryCount = 0;
            double parallelRUCount = 0;
            var tasksToRun = Enumerable.Range(0, totalParallelRequests)
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
                               item.Id = Guid.NewGuid().ToString(); // Ensure unique ID for each item 
                               itemResponse = await CreateItemInCosmosAsync(container, item);
                               lock (countLock) { parallelRUCount += itemResponse.RequestCharge; }
                           }
                           catch (CosmosException ex)
                           {
                               if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                               {
                                   lock (countLock) { failedRetryCount++; }
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
            return (failedRetryCount, parallelRUCount);
        }

        private static async Task<ItemResponse<Item>> CreateItemInCosmosAsync(Container container, Item item)
        {
            var resp = await container.CreateItemAsync(item, new PartitionKey(item.Id));
            return resp;
        }

        private static async Task<ItemResponse<Item>> UpdateItemInCosmosAsync(Container container, Item item)
        {
            item.Name = "RetryTest-Updated";
            item.Timestamp = DateTime.UtcNow;
            var resp = await container.UpsertItemAsync(item, new PartitionKey(item.Id));
            return resp;
        }
    }
}
