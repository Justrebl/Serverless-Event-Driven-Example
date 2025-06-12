using CosmosRetryConsoleApp.Config;
using CosmosRetryConsoleApp.Services;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CosmosRetryConsoleApp.Sequences
{
    public static class CompareModelsSequence
    {
        public static async Task RunAsync(Database database)
        {
            // Mesure des temps pour chaque opération
            long tFindQpA = -1, tQpsBySupplierA = -1, tCreateQpA = -1, tUpdateQpA = -1, tUpdateAllSuppliersA = -1;
            long tFindQpB = -1, tQpsBySupplierB = -1, tQpReadsB = -1, tCreateQpB = -1, tUpdateQpB = -1, tUpdateAllSuppliersB = -1;
            double ruFindQpA = -1, ruQpsBySupplierA = -1, ruCreateQpA = -1, ruUpdateQpA = -1, ruUpdateAllSuppliersA = -1;
            double ruFindQpB = -1, ruQpsBySupplierB = -1, ruQpReadsB = -1, ruCreateQpB = -1, ruUpdateQpB = -1, ruUpdateAllSuppliersB = -1;
            double ruCreateQpB_updateSupplier = -1;

            try
            {
                Console.WriteLine("\n--- DÉBUT MODÈLE A (clé étrangère dans QP, containers QP_A/Suppliers_A) ---");

                Container qpContainerA = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Const.QPContainerIdA, "/id"))).Container;
                Container suppliersContainerA = (await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Const.suppliersContainerIdA, "/id"))).Container;

                var qpAContainerService = new ContainerService(qpContainerA);
                var qpAService = new QPService(qpAContainerService);
                var suppliersAService = new ContainerService(suppliersContainerA);
                var supplierAService = new SupplierService(suppliersAService);

                var supplierA = new Models.SupplierA
                {
                    id = "test-supplier-a-id-1",
                    property1 = "sup1",
                    property2 = "sup2"
                };
                var qpA = new Models.QPA
                {
                    id = "test-qp-a-id-1",
                    property1 = "init1",
                    property2 = "init2",
                    idSupplier = supplierA.id
                };
                var upsertSupplierAResp = await supplierAService.UpsertSupplierAsync(supplierA);
                var upsertQpAResp = await qpAService.UpsertQpAsync(qpA);

                // 1. Find QP by ID (Model A)
                (ruFindQpA, tFindQpA) = await qpAService.FindQpByIdAsync<Models.QPA>(qpA.id);

                // 2. Find all QPs for supplier (Model A)
                (ruQpsBySupplierA, tQpsBySupplierA, List<string> qpIdsA) = await supplierAService.FindQpsBySupplierAsync<Models.QPA>(supplierA.id, nameof(Models.QPA.idSupplier));

                // 3. Create QP (Model A)
                (ruCreateQpA, tCreateQpA, var newQpAId) = await qpAService.CreateQpAsync<Models.QPA>(supplierA.id, nameof(Models.QPA.idSupplier));

                // 4. Update QP (Model A)
                (ruUpdateQpA, tUpdateQpA) = await qpAService.UpdateQpAsync<Models.QPA>(newQpAId, supplierA.id, nameof(Models.QPA.idSupplier));

                // 5. Update all suppliers (Model A)
                (ruUpdateAllSuppliersA, tUpdateAllSuppliersA, int updatedCountA) = await supplierAService.UpdateAllSuppliersAsync<Models.SupplierA>();

                // --- MODÈLE B (liste d'IDs QP dans Supplier) ---
                Console.WriteLine("\n--- DÉBUT MODÈLE B (liste d'IDs QP dans Supplier, containers QP_B/Suppliers_B) ---");
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
                (ruFindQpB, tFindQpB) = await qpBService.FindQpByIdAsync<QPB>(qpB.id);

                // 2. Find all QPs for supplier (Model B)
                (ruQpsBySupplierB, tQpsBySupplierB, qpIdsB) = await supplierBService.FindQpsBySupplierListAsync(supplierB.id);

                // 3. Create QP and add to supplier (Model B)
                (ruCreateQpB, tCreateQpB, var newQpBId) = await qpBService.CreateQpAndAddToSupplierAsync<Models.SupplierB>(supplierBService, supplierB.id);

                // 4. Update QP (Model B)
                (ruUpdateQpB, tUpdateQpB) = await qpBService.UpdateQpBAsync(newQpBId);

                // 5. Update all suppliers (Model B)
                (ruUpdateAllSuppliersB, tUpdateAllSuppliersB, updatedCountB) = await supplierBService.UpdateAllSuppliersAsync<Models.SupplierB>();

                // --- COMPARATIF FINAL ---
                Console.WriteLine("\n--- COMPARATIF FINAL (RU consommées & temps ms) ---");
                string FormatRU(double ru) => ru < 0 ? "N/A" : ru.ToString();
                string FormatT(long t) => t < 0 ? "N/A" : t.ToString();
                Console.WriteLine("1. Trouver une QP par ID :");
                Console.WriteLine($"  Modèle B : {FormatRU(ruFindQpB)} RU / {FormatT(tFindQpB)} ms | Modèle A : {FormatRU(ruFindQpA)} RU / {FormatT(tFindQpA)} ms");
                Console.WriteLine("2. Trouver toutes les QP d’un supplier :");
                Console.WriteLine($"  Modèle B : {FormatRU(ruQpsBySupplierB + ruQpReadsB)} RU / {FormatT(tQpsBySupplierB + tQpReadsB)} ms | Modèle A : {FormatRU(ruQpsBySupplierA)} RU / {FormatT(tQpsBySupplierA)} ms");
                Console.WriteLine("3. Créer une QP :");
                Console.WriteLine($"  Modèle B : {FormatRU(ruCreateQpB + ruCreateQpB_updateSupplier)} RU / {FormatT(tCreateQpB)} ms | Modèle A : {FormatRU(ruCreateQpA)} RU / {FormatT(tCreateQpA)} ms");
                Console.WriteLine("4. Mettre à jour une QP :");
                Console.WriteLine($"  Modèle B : {FormatRU(ruUpdateQpB)} RU / {FormatT(tUpdateQpB)} ms | Modèle A : {FormatRU(ruUpdateQpA)} RU / {FormatT(tUpdateQpA)} ms");
                Console.WriteLine("5. Mettre à jour tous les suppliers :");
                Console.WriteLine($"  Modèle B : {FormatRU(ruUpdateAllSuppliersB)} RU / {FormatT(tUpdateAllSuppliersB)} ms | Modèle A : {FormatRU(ruUpdateAllSuppliersA)} RU / {FormatT(tUpdateAllSuppliersA)} ms");
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
