namespace CosmosRetryConsoleApp.Config
{
    public class CosmosDBConfig
    {
        public required string EndpointUri { get; set; }
        public required string DatabaseId { get; set; }
        public required string ContainerId { get; set; }
        public required string PrimaryKey { get; set; }
    }
}
