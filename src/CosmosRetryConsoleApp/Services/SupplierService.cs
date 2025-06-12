using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmosRetryConsoleApp.Models;

namespace CosmosRetryConsoleApp.Services
{
    public class SupplierService{
        public async Task<ItemResponse<T>> GetSupplierByIdAsync<T>(string supplierId) where T : Supplier
        {
            return await _containerService.ReadItemAsync<T>(supplierId);
        }
    
        private readonly ContainerService _containerService;

        public SupplierService(ContainerService containerService)
        {
            _containerService = containerService;
        }

        // Upsert a supplier
        public async Task<ItemResponse<T>> UpsertSupplierAsync<T>(T supplier) where T : Supplier
        {
            return await _containerService.UpsertItemAsync(supplier, supplier.id);
        }

        // Update all suppliers (returns RU, ms, updatedCount)
        public async Task<(double ru, long ms, int updatedCount)> UpdateAllSuppliersAsync<T>() where T : Supplier
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double ru = 0;
            int updatedCount = 0;

            QueryDefinition query = new QueryDefinition("SELECT * FROM c");

            FeedIterator<T> iterator = await _containerService.QueryItemsAsync<T>(query);
            
            while (iterator.HasMoreResults)
            {
                FeedResponse<T> response = await iterator.ReadNextAsync();
                ru += response.RequestCharge;
                foreach (var supplier in response)
                {
                    try
                    {
                        //Replacing the item with itself to trigger an update
                        // This is a no-op but will update the timestamp and RU count
                        var upResp = await _containerService.ReplaceItemAsync(supplier, supplier.id);
                        ru += upResp.RequestCharge;
                        updatedCount++;
                    }                    
                    catch { }
                }
            }
            sw.Stop();
            return (ru, sw.ElapsedMilliseconds, updatedCount);
        }       

        // Find all QPs for a supplier (Model B)
        public async Task<(double ru, long ms, List<string> qpIds)> FindQpsListBySupplierIdAsync(string supplierId)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double ru = 0;
            var qpIds = new List<string>();
            QueryDefinition query = new QueryDefinition("SELECT c.QPs FROM c WHERE c.id = @supplierId").WithParameter("@supplierId", supplierId);
            FeedIterator<SupplierB> iterator = await _containerService.QueryItemsAsync<SupplierB>(query);
            while (iterator.HasMoreResults)
            {
                FeedResponse<SupplierB> response = await iterator.ReadNextAsync();
                ru += response.RequestCharge;
                foreach (var doc in response)
                {
                    if (doc.QPs != null)
                    {
                        qpIds.AddRange(doc.QPs);
                    }
                }
            }
            sw.Stop();
            return (ru, sw.ElapsedMilliseconds, qpIds);
        }
    }
}
