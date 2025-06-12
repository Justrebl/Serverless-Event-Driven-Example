namespace CosmosRetryConsoleApp.Config
{
    public static class Const
    {
        public const string CosmosDBSection = "CosmosDB";
        public const string IdentitySection = "Identity";

        public const string suppliersContainerIdA  = "SuppliersA";
        public const string suppliersContainerIdB  = "SuppliersB";

        public const string QPContainerIdA = "QP_A";
        public const string QPContainerIdB = "QP_B";

        public const int DefaultMaxRetries = 9; // Maximum number of retries for throttled requests
        public const int DefaultMaxWaitTimeSeconds = 30; // Maximum wait time for retries 
    }
}