namespace CosmosRetryConsoleApp.Models
{
    public abstract class QP
    {
        public string id { get; set; }
        public string property1 { get; set; }
        public string property2 { get; set; }
    }

    public class QPA : QP
    {
        public string idSupplier { get; set; }
    }

    public class QPB : QP
    {
        // No idSupplier
    }
}