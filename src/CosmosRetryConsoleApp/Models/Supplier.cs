namespace CosmosRetryConsoleApp.Models
{
    public abstract class Supplier
    {
        public string id { get; set; }
        public string property1 { get; set; }
        public string property2 { get; set; }
    }

    public class SupplierA : Supplier
    {
        // No QPs list
    }

    public class SupplierB : Supplier
    {
        public System.Collections.Generic.List<string> QPs { get; set; }
    }
}