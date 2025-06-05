using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

class Program
{
    private static double _ruSingleCreateCount = 0;
    private static double _ruSingleUpdateCount = 0;
    // Simulate throttling by running many requests in parallel
    private static int _failedRetryCount = 0;
    private const int TOTAL_PARALLEL_REQUESTS = 1000;
    private const int CHUNK_SIZE = 100; // Adjust chunk size as needed
    private const int MAX_RETRIES = 10; // Maximum number of retries for throttled requests
    private const int MAX_WAIT_TIME_SECONDS = 30; // Maximum wait time for retries
    private static double _parallelRUCount = 0;
    private static object _countLock = new object();

    private static async Task<Container> InitContainerClientAsync()
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
                MaxRetryAttemptsOnRateLimitedRequests = MAX_RETRIES, // Number of retries on 429 Too Many Requests (default 9)
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(MAX_WAIT_TIME_SECONDS), // Maximum wait time for cumulative retries (default 30 seconds)
                // ConsistencyLevel = ConsistencyLevel.Session // or Eventual, Strong, BoundedStaleness, etc.
            });

        Database database = await client.CreateDatabaseIfNotExistsAsync(cosmosConfig.DatabaseId);
        return await database.CreateContainerIfNotExistsAsync(cosmosConfig.ContainerId, "/id");
    }

    static async Task Main(string[] args)
    {
        Container container = await InitContainerClientAsync();

        // Default Item 
        Item item = new Item()
        {
            Id = Guid.NewGuid().ToString(),
            Name = "RetryTest",
            Timestamp = DateTime.UtcNow
        };

        // Execute single item write (Create and then Upsert) operation with retry logic
        await CountSingleUpsertOperationRUAsync(container, item);

        // Execute parallel inserts with retry logic and logging failed throttled request due to too many retries 
        await SimulateThrottlingAsync(container, item);

        // Output the simulation results
        Console.WriteLine($"Total requests failed even after retries due to {MAX_RETRIES} 429: {_failedRetryCount}");
        Console.WriteLine($"Total RU consumed (create): {_ruSingleCreateCount}");
        Console.WriteLine($"Total RU consumed (update): {_ruSingleUpdateCount}");
        Console.WriteLine($"Total RU consumed ({TOTAL_PARALLEL_REQUESTS} parallel inserts): {_parallelRUCount}");
    }

    private static async Task CountSingleUpsertOperationRUAsync(Container container, Item item)
    {
        ItemResponse<Item> response = null;
        try
        {
            item.Id = Guid.NewGuid().ToString(); // Ensure unique ID for each item 
            response = await CreateItemInCosmosAsync(container, item);

            _ruSingleCreateCount = response.RequestCharge;
            Console.WriteLine($"Item created. RU consumed: {response.RequestCharge}");

            response = await UpdateItemInCosmosAsync(container, response.Resource);
            _ruSingleUpdateCount = response.RequestCharge;

            Console.WriteLine($"Item updated. RU consumed: {response.RequestCharge}");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    private static async Task SimulateThrottlingAsync(Container container, Item item)
    {
        List<Func<Task>> _tasks = new List<Func<Task>>();

        var throttler = new SemaphoreSlim(CHUNK_SIZE);
        var tasksToRun = Enumerable.Range(0, TOTAL_PARALLEL_REQUESTS)
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
                           lock (_countLock) { _parallelRUCount += itemResponse.RequestCharge; }
                       }
                       catch (CosmosException ex)
                       {
                           if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                           {
                               lock (_countLock) { _failedRetryCount++; }
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