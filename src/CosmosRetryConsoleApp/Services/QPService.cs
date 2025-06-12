using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmosRetryConsoleApp.Models;

namespace CosmosRetryConsoleApp.Services
{
    public class QPService
    {
        private readonly ContainerService _containerService;

        public QPService(ContainerService containerService)
        {
            _containerService = containerService;
        }

        #region Generic QP Methods
        public async Task<ItemResponse<T>> UpsertQpAsync<T>(T qp) where T : QP
        {
            return await _containerService.UpsertItemAsync(qp, qp.id);
        }

        public async Task<(double ru, long ms, T qp)> FindQpByIdAsync<T>(string qpId) where T : QP
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double ru = -1;
            T resource = null;
            try
            {
                var resp = await _containerService.ReadItemAsync<T>(qpId);
                ru = resp.RequestCharge;
                resource  = resp.Resource;
            }
            catch (CosmosException) { }
            sw.Stop();
            return (ru, sw.ElapsedMilliseconds, resource);
        }

        #endregion
        #region Model A Methods

        // Create a QP (Model A)
        public async Task<(double ru, long ms, QPA qp)> CreateQpAsync(QPA newQp)
        {
            newQp.id = Guid.NewGuid().ToString(); // Ensure unique ID for QP

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var resp = await _containerService.CreateItemAsync(newQp, newQp.id);
            sw.Stop();
            return (resp.RequestCharge, sw.ElapsedMilliseconds, resp.Resource);
        }

        // Find all QPs for a supplier (Model A)
        public async Task<(double ru, long ms, List<string> qpIds)> FindQpsBySupplierIdAsync(string supplierId)
        {
            
            double ru = 0;
            var qpIds = new List<string>();

            QueryDefinition query = new QueryDefinition($"SELECT * FROM c WHERE c.idSupplier = @supplierId").WithParameter("@supplierId", supplierId);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            FeedIterator<QPA> iterator = await _containerService.QueryItemsAsync<QPA>(query);
            while (iterator.HasMoreResults)
            {
                FeedResponse<QPA> response = await iterator.ReadNextAsync();
                ru += response.RequestCharge;
                foreach (var doc in response)
                {
                    try { qpIds.Add(doc.id); } catch { }
                }
            }
            sw.Stop();

            return (ru, sw.ElapsedMilliseconds, qpIds);
        }

        // Update a QP (Model A)
        public async Task<(double ru, long ms)> UpdateQpAsync(QPA qp)
        {
            qp.property1 = "val1-updated";
            qp.property2 = "val2-updated";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var resp = await _containerService.UpsertItemAsync(qp, qp.id);
            sw.Stop();

            return (resp.RequestCharge, sw.ElapsedMilliseconds);
        }
        #endregion

        #region Model B Methods
        // Create QP and add to supplier's QPs list (Model B)
        public async Task<(double ru, long ms, QPB qpId)> CreateQpAndAddToSupplierAsync(SupplierService supplierService, QPB qpb, string supplierId)
        {
            qpb.id = Guid.NewGuid().ToString(); // Ensure unique ID for QP

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var createQpResp = await _containerService.CreateItemAsync(qpb, qpb.id);

            // Read supplier, add QP id, update supplier
            var supplierResp = await supplierService.GetSupplierByIdAsync<SupplierB>(supplierId);
            var supplier = supplierResp.Resource;

            supplier.QPs.Add(qpb.id);
            var updateSupplierResp = await supplierService.UpsertSupplierAsync(supplier);
            sw.Stop();

            return (createQpResp.RequestCharge + updateSupplierResp.RequestCharge, sw.ElapsedMilliseconds, createQpResp.Resource);
        }

        // Update a QP (Model B)
        public async Task<(double ru, long ms)> UpdateQpBAsync(QPB qp)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var resp = await _containerService.UpsertItemAsync(qp, qp.id);
            sw.Stop();

            return (resp.RequestCharge, sw.ElapsedMilliseconds);
        }

        #endregion
    }
}
