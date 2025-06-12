using CosmosRetryConsoleApp.Config;
using CosmosRetryConsoleApp.Models;
using CosmosRetryConsoleApp.Services;
using Microsoft.Azure.Cosmos;

namespace CosmosRetryConsoleApp.Sequences
{
    public static class CompareModelsSequence
    {
        public static async Task RunAsync(Database database)
        {
            try
            {
                Console.WriteLine("\n--- START MODEL A (foreign key in QP, containers QP_A/Suppliers_A) ---");

                Container qpContainerA = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Const.QPContainerIdA, "/id"))).Container;
                Container suppliersContainerA = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Const.suppliersContainerIdA, "/id"))).Container;

                var qpAContainerService = new ContainerService(qpContainerA);
                var qpAService = new QPService(qpAContainerService);
                var suppliersAService = new ContainerService(suppliersContainerA);
                var supplierAService = new SupplierService(suppliersAService);

                var supplierA = new SupplierA
                {
                    id = "test-supplier-a-id-1",
                    property1 = "sup1",
                    property2 = "sup2"
                };
                var qpA = new QPA
                {
                    id = "test-qp-a-id-1",
                    property1 = "init1",
                    property2 = "init2",
                    idSupplier = supplierA.id
                };
                var upsertSupplierAResp = await supplierAService.UpsertSupplierAsync(supplierA);
                var upsertQpAResp = await qpAService.UpsertQpAsync(qpA);

                // 1. Find QP by ID (Model A)
                var (ruFindQpA, tFindQpA) = await qpAService.FindQpByIdAsync<QPA>(qpA.id);

                // 2. Find all QPs for supplier (Model A)
                var (ruQpsBySupplierA, tQpsBySupplierA, qpIdsA) = await supplierAService.FindQpsBySupplierAsync<QPA>(supplierA.id, nameof(QPA.idSupplier));

                // 3. Create QP (Model A)
                var (ruCreateQpA, tCreateQpA, newQpAId) = await qpAService.CreateQpAsync<QPA>(supplierA.id, nameof(QPA.idSupplier));

                // 4. Update QP (Model A)
                var (ruUpdateQpA, tUpdateQpA) = await qpAService.UpdateQpAsync<QPA>(newQpAId, supplierA.id, nameof(QPA.idSupplier));

                // 5. Update all suppliers (Model A)
                var (ruUpdateAllSuppliersA, tUpdateAllSuppliersA, updatedCountA) = await supplierAService.UpdateAllSuppliersAsync<SupplierA>();

                // --- MODEL B (list of QP IDs in Supplier) ---
                Console.WriteLine("\n--- START MODEL B (list of QP IDs in Supplier, containers QP_B/Suppliers_B) ---");
                string qpContainerIdB = "QP_B";
                string suppliersContainerIdB = "Suppliers_B";

                // Création systématique des containers (idempotent)
                Container qpContainerB = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(qpContainerIdB, "/id"))).Container;
                Container suppliersContainerB = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(suppliersContainerIdB, "/id"))).Container;

                var qpBContainerService = new ContainerService(qpContainerB);
                var qpBService = new QPService(qpBContainerService);
                var suppliersBService = new ContainerService(suppliersContainerB);
                var supplierBService = new SupplierService(suppliersBService);

                var qpB = new QPB
                {
                    id = "test-qp-b-id-1",
                    property1 = "init1",
                    property2 = "init2",
                };
                await qpBService.UpsertQpAsync(qpB);

                var supplierB = new SupplierB
                {
                    id = "test-supplier-b-id-1",
                    QPs = new List<string> { qpB.id },
                    property1 = "sup1",
                    property2 = "sup2"
                };
                await supplierBService.UpsertSupplierAsync(supplierB);

                // 1. Find QP by ID (Model B)
                var (ruFindQpB, tFindQpB) = await qpBService.FindQpByIdAsync<QPB>(qpB.id);

                // 2. Find all QPs for supplier (Model B)
                var (ruQpsBySupplierB, tQpsBySupplierB, qpIdsB) = await supplierBService.FindQpsBySupplierListAsync(supplierB.id);

                // 3. Create QP and add to supplier (Model B)
                var (ruCreateQpB, tCreateQpB, newQpBId) = await qpBService.CreateQpAndAddToSupplierAsync<SupplierB>(supplierBService, supplierB.id);

                // 4. Update QP (Model B)
                var (ruUpdateQpB, tUpdateQpB) = await qpBService.UpdateQpBAsync(newQpBId);

                // 5. Update all suppliers (Model B)
                var (ruUpdateAllSuppliersB, tUpdateAllSuppliersB, updatedCountB) = await supplierBService.UpdateAllSuppliersAsync<SupplierB>();

                // --- COMPARATIF FINAL ---
                Console.WriteLine("\n--- FINAL COMPARISON (RU consumed & time ms) ---");

                string FormatRU(double ru) => ru < 0 ? "N/A" : ru.ToString();
                string FormatT(long t) => t < 0 ? "N/A" : t.ToString();

                Console.WriteLine("1. Find a QP by ID:");
                Console.WriteLine($"  Model B: {qpIdsB} QPs found for {FormatRU(ruFindQpB)} RU / {FormatT(tFindQpB)} ms | Model A: {qpIdsA} QPs found for {FormatRU(ruFindQpA)} RU / {FormatT(tFindQpA)} ms");
                Console.WriteLine("2. Find all QPs for a supplier:");
                Console.WriteLine($"  Model B: {FormatRU(ruQpsBySupplierB)} RU / {FormatT(tQpsBySupplierB)} ms | Model A: {FormatRU(ruQpsBySupplierA)} RU / {FormatT(tQpsBySupplierA)} ms");
                Console.WriteLine("3. Create a QP:");
                Console.WriteLine($"  Model B: {FormatRU(ruCreateQpB)} RU / {FormatT(tCreateQpB)} ms | Model A: {FormatRU(ruCreateQpA)} RU / {FormatT(tCreateQpA)} ms");
                Console.WriteLine("4. Update a QP:");
                Console.WriteLine($"  Model B: {FormatRU(ruUpdateQpB)} RU / {FormatT(tUpdateQpB)} ms | Model A: {FormatRU(ruUpdateQpA)} RU / {FormatT(tUpdateQpA)} ms");
                Console.WriteLine("5. Update all suppliers:");
                Console.WriteLine($"  Model B: {FormatRU(ruUpdateAllSuppliersB)} RU / {FormatT(tUpdateAllSuppliersB)} ms | Model A: {FormatRU(ruUpdateAllSuppliersA)} RU / {FormatT(tUpdateAllSuppliersA)} ms");
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"CosmosException: {ex.StatusCode} - {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }
    }
}
