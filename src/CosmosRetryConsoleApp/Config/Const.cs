namespace CosmosRetryConsoleApp.Config
{
    public static class Const
    {
        public const string CosmosDBSection = "CosmosDB";
        public const string IdentitySection = "Identity";

        public const string SupplierAIdField = "supplierAId";
        public const string SupplierBIdField = "supplierBId";

        public const string suppliersContainerIdA  = "SuppliersA";
        public const string suppliersContainerIdB  = "SuppliersB";

        public const string QPContainerIdA = "QP_A";
        public const string QPContainerIdB = "QP_B";

        public const string QPIdField = "qpId";
        public const string QPTypeField = "qpType";

        public const int DefaultMaxRetries = 5;
        public const int DefaultMaxWaitTimeSeconds = 30;
    }
}