using CosmosRetryConsoleApp.Config;
using CosmosRetryConsoleApp.Models;
using CosmosRetryConsoleApp.Services;
using Microsoft.Azure.Cosmos;

namespace CosmosRetryConsoleApp.Sequences
{
    public static class CompareModelsSequence
    {
        private static string FormatRU(double ru) => ru < 0 ? "N/A" : ru.ToString();
        private static string FormatT(long t) => t < 0 ? "N/A" : t.ToString();

        private static async Task<ModelResult> RunModelAAsync(Database database)
        {
            Console.WriteLine("\n--- START MODEL A (foreign key in QP, containers QP_A/Suppliers_A) ---");

            Container qpContainerA = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Const.QPContainerIdA, "/id"))).Container;
            Container suppliersContainerA = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Const.suppliersContainerIdA, "/id"))).Container;

            var qpAService = new QPService(new ContainerService(qpContainerA));
            var supplierAService = new SupplierService(new ContainerService(suppliersContainerA));

            var supplierA = new SupplierA
            {
                id = Guid.NewGuid().ToString(),
                property1 = "sup1",
                property2 = "sup2"
            };
            var qpA = new QPA
            {
                id = Guid.NewGuid().ToString(),
                property1 = "init1",
                property2 = "init2",
                idSupplier = supplierA.id
            };

            // Start by creating basic items in the containers
            var upsertSupplierAResp = await supplierAService.UpsertSupplierAsync(supplierA);
            var upsertQpAResp = await qpAService.UpsertQpAsync(qpA);

            // 1. Find QP by ID (Model A)
            var (ruFindQpA, tFindQpA, foundQpA) = await qpAService.FindQpByIdAsync<QPA>(upsertQpAResp.Resource.id);

            // 2. Find all QPs for supplier (Model A)
            var (ruQpsBySupplierA, tQpsBySupplierA, qpIdsA) = await qpAService.FindQpsBySupplierIdAsync(upsertSupplierAResp.Resource.id);

            // 3. Create QP (Model A)
            var (ruCreateQpA, tCreateQpA, newQpA) = await qpAService.CreateQpAsync(foundQpA);

            // 4. Update QP (Model A)
            var (ruUpdateQpA, tUpdateQpA) = await qpAService.UpdateQpAsync(newQpA);

            // 5. Update all suppliers (Model A)
            var (ruUpdateAllSuppliersA, tUpdateAllSuppliersA, updatedCountA) = await supplierAService.UpdateAllSuppliersAsync<SupplierA>();

            // Set resultA values
            return new ModelResult
            {
                ruFindQp = ruFindQpA,
                tFindQp = tFindQpA,
                ruQpsBySupplier = ruQpsBySupplierA,
                tQpsBySupplier = tQpsBySupplierA,
                ruCreateQp = ruCreateQpA,
                tCreateQp = tCreateQpA,
                ruUpdateQp = ruUpdateQpA,
                tUpdateQp = tUpdateQpA,
                ruUpdateAllSuppliers = ruUpdateAllSuppliersA,
                tUpdateAllSuppliers = tUpdateAllSuppliersA
            };
        }

        private static async Task<ModelResult> RunModelBAsync(Database database)
        {
            Console.WriteLine("\n--- START MODEL B (list of QP IDs in Supplier, containers QP_B/Suppliers_B) ---");

            // Idempotent container creation
            Container qpContainerB = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Const.QPContainerIdB, "/id"))).Container;
            Container suppliersContainerB = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Const.suppliersContainerIdB, "/id"))).Container;

            var qpBService = new QPService(new ContainerService(qpContainerB));
            var supplierBService = new SupplierService(new ContainerService(suppliersContainerB));

            var qpB = new QPB
            {
                id = Guid.NewGuid().ToString(),
                property1 = "init1",
                property2 = "init2",
            };

            // Upsert initial QP B 
            var upserQPBResp = await qpBService.UpsertQpAsync(qpB);

            var supplierB = new SupplierB
            {
                id = Guid.NewGuid().ToString(),
                QPs = new List<string> { qpB.id },
                property1 = "sup1",
                property2 = "sup2"
            };

            // Upsert supplier B with initial QP B
            var upserSupplierB = await supplierBService.UpsertSupplierAsync(supplierB);

            // 1. Find QP by ID (Model B)
            var (ruFindQpB, tFindQpB, foundQpB) = await qpBService.FindQpByIdAsync<QPB>(upserQPBResp.Resource.id);

            // 2. Find all QPs for supplier (Model B)
            var (ruQpsBySupplierB, tQpsBySupplierB, qpIdsB) = await supplierBService.FindQpsListBySupplierIdAsync(upserSupplierB.Resource.id);

            // 3. Create QP and add to supplier (Model B)
            var (ruCreateQpB, tCreateQpB, newQPB) = await qpBService.CreateQpAndAddToSupplierAsync(supplierBService, foundQpB, supplierB.id);


            // 4. Update QP (Model B)
            var (ruUpdateQpB, tUpdateQpB) = await qpBService.UpdateQpBAsync(newQPB);

            // 5. Update all suppliers (Model B)
            var (ruUpdateAllSuppliersB, tUpdateAllSuppliersB, updatedCountB) = await supplierBService.UpdateAllSuppliersAsync<SupplierB>();

            // Set resultB values
            return new ModelResult
            {
                ruFindQp = ruFindQpB,
                tFindQp = tFindQpB,
                ruQpsBySupplier = ruQpsBySupplierB,
                tQpsBySupplier = tQpsBySupplierB,
                ruCreateQp = ruCreateQpB,
                tCreateQp = tCreateQpB,
                ruUpdateQp = ruUpdateQpB,
                tUpdateQp = tUpdateQpB,
                ruUpdateAllSuppliers = ruUpdateAllSuppliersB,
                tUpdateAllSuppliers = tUpdateAllSuppliersB
            };
        }

        public static async Task RunAsync(Database database)
        {
            ModelResult resultA, resultB;

            try
            {
                resultA = await RunModelAAsync(database);
                resultB = await RunModelBAsync(database);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during model execution: {ex.Message}");
                return;
            }            

            // Print comparison summary
            Console.WriteLine("\n--- FINAL COMPARISON (RU consumed & time ms) ---");
            Console.WriteLine("Scenario 1. Find a QP by ID:");
            Console.WriteLine($"  Model A: {FormatRU(resultA.ruFindQp)} RU in {FormatT(resultA.tFindQp)} ms | Model B: {FormatRU(resultB.ruFindQp)} RU in {FormatT(resultB.tFindQp)} ms");
            Console.WriteLine("Scenario 2. Find all QPs for a supplier:");
            Console.WriteLine($"  Model A: {FormatRU(resultA.ruQpsBySupplier)} RU in {FormatT(resultA.tQpsBySupplier)} ms | Model B: {FormatRU(resultB.ruQpsBySupplier)} RU in {FormatT(resultB.tQpsBySupplier)} ms");
            Console.WriteLine("Scenario 3. Create a QP:");
            Console.WriteLine($"  Model A: {FormatRU(resultA.ruCreateQp)} RU in {FormatT(resultA.tCreateQp)} ms | Model B: {FormatRU(resultB.ruCreateQp)} RU in {FormatT(resultB.tCreateQp)} ms");
            Console.WriteLine("Scenario 4. Update a QP:");
            Console.WriteLine($"  Model A: {FormatRU(resultA.ruUpdateQp)} RU in {FormatT(resultA.tUpdateQp)} ms | Model B: {FormatRU(resultB.ruUpdateQp)} RU in {FormatT(resultB.tUpdateQp)} ms");
            Console.WriteLine("Scenario 5. Update all suppliers:");
            Console.WriteLine($"  Model A: {FormatRU(resultA.ruUpdateAllSuppliers)} RU in {FormatT(resultA.tUpdateAllSuppliers)} ms | Model B: {FormatRU(resultB.ruUpdateAllSuppliers)} RU in {FormatT(resultB.tUpdateAllSuppliers)} ms");
        }
    }    
}
