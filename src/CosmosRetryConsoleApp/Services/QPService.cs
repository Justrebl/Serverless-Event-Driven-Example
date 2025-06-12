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

        public async Task<ItemResponse<T>> UpsertQpAsync<T>(T qp) where T : QP
        {
            return await _containerService.UpsertItemAsync(qp, qp.id);
        }

        public async Task<ItemResponse<T>> CreateQpAsync<T>(T qp) where T : QP
        {
            return await _containerService.CreateItemAsync(qp, qp.id);
        }

        public async Task<ItemResponse<T>> UpdateQpAsync<T>(T qp) where T : QP
        {
            return await _containerService.ReplaceItemAsync(qp, qp.id);
        }

        public async Task<(double ru, long ms)> FindQpByIdAsync<T>(string qpId) where T : QP
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double ru = -1;
            try
            {
                var resp = await _containerService.ReadItemAsync<T>(qpId);
                ru = resp.RequestCharge;
            }
            catch (CosmosException) { }
            sw.Stop();
            return (ru, sw.ElapsedMilliseconds);
        }

        // Create a QP (Model A)
        public async Task<(double ru, long ms, string qpId)> CreateQpAsync<T>(string supplierId, string supplierIdField) where T : QP, new()
        {
            var newQp = new T
            {
                id = Guid.NewGuid().ToString(),
                property1 = "val1",
                property2 = "val2"
            };
            // Set supplierId if property exists (Model A)
            var prop = typeof(T).GetProperty(supplierIdField);
            if (prop != null)
                prop.SetValue(newQp, supplierId);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var resp = await _containerService.CreateItemAsync(newQp, newQp.id);
            sw.Stop();
            return (resp.RequestCharge, sw.ElapsedMilliseconds, newQp.id);
        }

        // Create QP and add to supplier's QPs list (Model B)
        public async Task<(double ru, long ms, string qpId)> CreateQpAndAddToSupplierAsync<TSupplier>(SupplierService supplierService, string supplierId) where TSupplier : SupplierB, new()
        {
            var newQp = new QPB
            {
                id = Guid.NewGuid().ToString(),
                property1 = "val1",
                property2 = "val2"
            };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var createQpResp = await _containerService.CreateItemAsync(newQp, newQp.id);
            // Read supplier, add QP id, update supplier
            var supplierResp = await supplierService.GetSupplierByIdAsync<TSupplier>(supplierId);
            var supplier = supplierResp.Resource;
            if (supplier.QPs == null)
                supplier.QPs = new List<string>();
            supplier.QPs.Add(newQp.id);
            var updateSupplierResp = await supplierService.UpsertSupplierAsync(supplier);
            sw.Stop();
            return (createQpResp.RequestCharge + updateSupplierResp.RequestCharge, sw.ElapsedMilliseconds, newQp.id);
        }

        // Update a QP (Model A)
        public async Task<(double ru, long ms)> UpdateQpAsync<T>(string qpId, string supplierId, string supplierIdField) where T : QP, new()
        {
            var updatedQp = new T
            {
                id = qpId,
                property1 = "val1-updated",
                property2 = "val2-updated"
            };
            var prop = typeof(T).GetProperty(supplierIdField);
            if (prop != null)
                prop.SetValue(updatedQp, supplierId);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var resp = await _containerService.ReplaceItemAsync(updatedQp, qpId);
            sw.Stop();
            return (resp.RequestCharge, sw.ElapsedMilliseconds);
        }

        // Update a QP (Model B)
        public async Task<(double ru, long ms)> UpdateQpBAsync(string qpId)
        {
            var updatedQp = new QPB
            {
                id = qpId,
                property1 = "val1-updated",
                property2 = "val2-updated"
            };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var resp = await _containerService.ReplaceItemAsync(updatedQp, qpId);
            sw.Stop();
            return (resp.RequestCharge, sw.ElapsedMilliseconds);
        }
    }
}
