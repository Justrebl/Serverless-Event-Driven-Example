using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Diagnostics;

class Program
{
    static async Task Main(string[] args)
    {
        // --- Valeurs en dur pour test rapide ---
        
        Console.WriteLine($"Config - EndpointUri: {endpointUri}");
        Console.WriteLine($"Config - DatabaseId: {databaseId}");
        Console.WriteLine($"Config - QPContainerId: {qpContainerId}");
        Console.WriteLine($"Config - SuppliersContainerId: {suppliersContainerId}");
        Console.WriteLine($"Config - ClientId: {clientId}");
        Console.WriteLine($"Config - TenantId: {tenantId}");
        Console.WriteLine($"Config - ClientKey: {(string.IsNullOrEmpty(clientSecret) ? "(empty/null)" : "(provided)")}");

        CosmosClientOptions options = new CosmosClientOptions();
        CosmosClient client = new CosmosClient(
            endpointUri,
            new ClientSecretCredential(tenantId, clientId, clientSecret),
            options
        );

        try
        {
            Database database = client.GetDatabase(databaseId);

            // --- MODÈLE A (clé étrangère dans QP) ---
            Console.WriteLine("\n--- DÉBUT MODÈLE A (clé étrangère dans QP, containers QP_A/Suppliers_A) ---");
            string qpContainerIdA = "QP_A";
            string suppliersContainerIdA = "Suppliers_A";
            // Création systématique des containers (idempotent)
            var qpRespA = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(qpContainerIdA, "/id"));
            var qpContainerA = qpRespA.Container;
            var supRespA = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(suppliersContainerIdA, "/id"));
            var suppliersContainerA = supRespA.Container;
            // Initialisation d'un Supplier et d'une QP pour ce schéma
            var supplierA = new { id = "test-supplier-a-id-1", property1 = "sup1", property2 = "sup2" };
            var upsertSupplierAResp = await suppliersContainerA.UpsertItemAsync(supplierA, new PartitionKey(supplierA.id));
            Console.WriteLine($"[A] Supplier upserted: {supplierA.id} | RU: {upsertSupplierAResp.RequestCharge} | Status: {upsertSupplierAResp.StatusCode}");
            var qpA = new { id = "test-qp-a-id-1", property1 = "init1", property2 = "init2", idSupplier = supplierA.id };
            var upsertQpAResp = await qpContainerA.UpsertItemAsync(qpA, new PartitionKey(qpA.id));
            Console.WriteLine($"[A] QP upserted: {qpA.id} | RU: {upsertQpAResp.RequestCharge} | Status: {upsertQpAResp.StatusCode} | Resource: {upsertQpAResp.Resource}");
            await Task.Delay(2000); // Délai augmenté pour tester la consistance

            // Diagnostic : lister tous les documents du container QP_A (affichage complet)
            Console.WriteLine("[A] Diagnostic : listing all docs in QP_A (full JSON)...");
            var allQpsA = qpContainerA.GetItemQueryIterator<dynamic>(new QueryDefinition("SELECT * FROM c"));
            while (allQpsA.HasMoreResults)
            {
                var resp = await allQpsA.ReadNextAsync();
                foreach (var doc in resp)
                {
                    try {
                        Console.WriteLine($"[A] QP found in container: {doc}");
                    } catch { Console.WriteLine("[A] QP found in container (id/partitionKey non lisible)"); }
                }
            }

            // Mesure des temps pour chaque opération
            long tFindQpA = -1, tQpsBySupplierA = -1, tCreateQpA = -1, tUpdateQpA = -1, tUpdateAllSuppliersA = -1;
            long tFindQpB = -1, tQpsBySupplierB = -1, tQpReadsB = -1, tCreateQpB = -1, tUpdateQpB = -1, tUpdateAllSuppliersB = -1;
            double ruFindQpA = -1, ruQpsBySupplierA = -1, ruCreateQpA = -1, ruUpdateQpA = -1, ruUpdateAllSuppliersA = -1;
            double ruFindQpB = -1, ruQpsBySupplierB = -1, ruQpReadsB = -1, ruCreateQpB = -1, ruUpdateQpB = -1, ruUpdateAllSuppliersB = -1;
            double ruCreateQpB_updateSupplier = -1;
            // 1. Trouver une QP par son identifiant
            var swA = Stopwatch.StartNew();
            try {
                var qpReadA = await qpContainerA.ReadItemAsync<dynamic>(qpA.id, new PartitionKey(qpA.id));
                swA.Stop();
                tFindQpA = swA.ElapsedMilliseconds;
                Console.WriteLine($"[A] 1. QP found: {qpReadA.Resource}");
                ruFindQpA = qpReadA.RequestCharge;
                Console.WriteLine($"[A] RU consumed (find QP by id): {ruFindQpA} | Time: {tFindQpA} ms");
            } catch (CosmosException ex) {
                swA.Stop();
                tFindQpA = swA.ElapsedMilliseconds;
                Console.WriteLine($"[A] 1. QP not found: CosmosException {ex.StatusCode} - {ex.Message} | Time: {tFindQpA} ms");
            } catch (Exception ex) {
                swA.Stop();
                tFindQpA = swA.ElapsedMilliseconds;
                Console.WriteLine($"[A] 1. QP not found: Exception {ex.Message} | Time: {tFindQpA} ms");
            }

            // 2. Trouver toutes les QP d'un supplier ID (requête sur QP)
            QueryDefinition qpsBySupplierA = new QueryDefinition("SELECT * FROM c WHERE c.idSupplier = @supplierId").WithParameter("@supplierId", supplierA.id);
            FeedIterator<dynamic> qpsBySupplierIteratorA = qpContainerA.GetItemQueryIterator<dynamic>(qpsBySupplierA);
            var swA2 = Stopwatch.StartNew();
            ruQpsBySupplierA = 0;
            List<string> qpIdsA = new List<string>();
            while (qpsBySupplierIteratorA.HasMoreResults)
            {
                FeedResponse<dynamic> response = await qpsBySupplierIteratorA.ReadNextAsync();
                ruQpsBySupplierA += response.RequestCharge;
                foreach (var doc in response)
                {
                    qpIdsA.Add((string)doc.id);
                }
            }
            swA2.Stop();
            tQpsBySupplierA = swA2.ElapsedMilliseconds;
            Console.WriteLine($"[A] 2. QP IDs for supplier {supplierA.id}: [{string.Join(", ", qpIdsA)}]");
            Console.WriteLine($"[A] RU consumed (find QPs by supplier): {ruQpsBySupplierA} | Time: {tQpsBySupplierA} ms");

            // 3. Création d'une QP
            var newQpA = new { id = Guid.NewGuid().ToString(), property1 = "val1", property2 = "val2", idSupplier = supplierA.id };
            var swA3 = Stopwatch.StartNew();
            var createQpAResponse = await qpContainerA.CreateItemAsync<dynamic>(newQpA, new PartitionKey(newQpA.id));
            swA3.Stop();
            tCreateQpA = swA3.ElapsedMilliseconds;
            Console.WriteLine($"[A] 3. QP created: {newQpA.id}");
            ruCreateQpA = createQpAResponse.RequestCharge;
            Console.WriteLine($"[A] RU consumed (create QP): {ruCreateQpA} | Time: {tCreateQpA} ms");

            // 4. Mise à jour d'une QP
            var updatedQpA = new { id = newQpA.id, property1 = "val1-updated", property2 = "val2-updated", idSupplier = supplierA.id };
            var swA4 = Stopwatch.StartNew();
            var updateQpAResponse = await qpContainerA.ReplaceItemAsync<dynamic>(updatedQpA, updatedQpA.id, new PartitionKey(updatedQpA.id));
            swA4.Stop();
            tUpdateQpA = swA4.ElapsedMilliseconds;
            Console.WriteLine($"[A] 4. QP updated: {updatedQpA.id}");
            ruUpdateQpA = updateQpAResponse.RequestCharge;
            Console.WriteLine($"[A] RU consumed (update QP): {ruUpdateQpA} | Time: {tUpdateQpA} ms");

            // 5. Mettre à jour tous les suppliers
            QueryDefinition allSuppliersQueryA = new QueryDefinition("SELECT * FROM c");
            FeedIterator<dynamic> suppliersIteratorA = suppliersContainerA.GetItemQueryIterator<dynamic>(allSuppliersQueryA);
            var swA5 = Stopwatch.StartNew();
            ruUpdateAllSuppliersA = 0;
            int updatedCountA = 0;
            while (suppliersIteratorA.HasMoreResults)
            {
                FeedResponse<dynamic> response = await suppliersIteratorA.ReadNextAsync();
                ruUpdateAllSuppliersA += response.RequestCharge;
                foreach (var supplier in response)
                {
                    supplier.lastUpdated = DateTime.UtcNow;
                    var upResp = await suppliersContainerA.ReplaceItemAsync<dynamic>(supplier, (string)supplier.id, new PartitionKey((string)supplier.id));
                    ruUpdateAllSuppliersA += upResp.RequestCharge;
                    updatedCountA++;
                }
            }
            swA5.Stop();
            tUpdateAllSuppliersA = swA5.ElapsedMilliseconds;
            Console.WriteLine($"[A] 5. Suppliers updated: {updatedCountA}");
            Console.WriteLine($"[A] RU consumed (update all suppliers): {ruUpdateAllSuppliersA} | Time: {tUpdateAllSuppliersA} ms");

            // --- MODÈLE B (liste d'IDs QP dans Supplier) ---
            Console.WriteLine("\n--- DÉBUT MODÈLE B (liste d'IDs QP dans Supplier, containers QP_B/Suppliers_B) ---");
            string qpContainerIdB = "QP_B";
            string suppliersContainerIdB = "Suppliers_B";
            // Création systématique des containers (idempotent)
            var qpRespB = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(qpContainerIdB, "/id"));
            var qpContainerB = qpRespB.Container;
            var supRespB = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(suppliersContainerIdB, "/id"));
            var suppliersContainerB = supRespB.Container;
            // Initialisation d'une QP et d'un Supplier legacy
            var qpB = new { id = "test-qp-b-id-1", property1 = "init1", property2 = "init2" };
            await qpContainerB.UpsertItemAsync(qpB, new PartitionKey(qpB.id));
            Console.WriteLine($"[B] QP upserted: {qpB.id}");
            var supplierB = new { id = "test-supplier-b-id-1", QPs = new List<string> { qpB.id }, property1 = "sup1", property2 = "sup2" };
            await suppliersContainerB.UpsertItemAsync(supplierB, new PartitionKey(supplierB.id));
            Console.WriteLine($"[B] Supplier upserted: {supplierB.id}");
            await Task.Delay(500);

            // 1. Trouver une QP par ID
            var swB = Stopwatch.StartNew();
            var qpReadB = await qpContainerB.ReadItemAsync<dynamic>(qpB.id, new PartitionKey(qpB.id));
            swB.Stop();
            tFindQpB = swB.ElapsedMilliseconds;
            Console.WriteLine($"[B] 1. QP found: {qpReadB.Resource}");
            ruFindQpB = qpReadB.RequestCharge;
            Console.WriteLine($"[B] RU consumed (find QP by id): {ruFindQpB} | Time: {tFindQpB} ms");

            // 2. Trouver toutes les QP d'un supplier (lecture de la liste d'IDs puis lecture de chaque QP)
            QueryDefinition legacyQpIdsQueryB = new QueryDefinition("SELECT c.QPs FROM c WHERE c.id = @supplierId").WithParameter("@supplierId", supplierB.id);
            FeedIterator<dynamic> legacyQpIdsIteratorB = suppliersContainerB.GetItemQueryIterator<dynamic>(legacyQpIdsQueryB);
            var swB2 = Stopwatch.StartNew();
            ruQpsBySupplierB = 0;
            List<string> qpIdsB = new List<string>();
            while (legacyQpIdsIteratorB.HasMoreResults)
            {
                FeedResponse<dynamic> response = await legacyQpIdsIteratorB.ReadNextAsync();
                ruQpsBySupplierB += response.RequestCharge;
                foreach (var doc in response)
                {
                    if (doc.QPs != null)
                    {
                        foreach (var id in doc.QPs)
                        {
                            qpIdsB.Add((string)id);
                        }
                    }
                }
            }
            swB2.Stop();
            tQpsBySupplierB = swB2.ElapsedMilliseconds;
            // Lecture de chaque QP
            var swB3 = Stopwatch.StartNew();
            ruQpReadsB = 0;
            foreach (var id in qpIdsB)
            {
                var qpResp = await qpContainerB.ReadItemAsync<dynamic>(id, new PartitionKey(id));
                ruQpReadsB += qpResp.RequestCharge;
            }
            swB3.Stop();
            tQpReadsB = swB3.ElapsedMilliseconds;
            Console.WriteLine($"[B] 2. QP IDs for supplier {supplierB.id}: [{string.Join(", ", qpIdsB)}]");
            Console.WriteLine($"[B] RU consumed (find QPs by supplier): {ruQpsBySupplierB + ruQpReadsB} | Time: {tQpsBySupplierB + tQpReadsB} ms");

            // 3. Création d'une QP (et ajout dans la liste du supplier)
            var newQpB = new { id = Guid.NewGuid().ToString(), property1 = "val1", property2 = "val2" };
            var swB4 = Stopwatch.StartNew();
            var createQpBResponse = await qpContainerB.CreateItemAsync<dynamic>(newQpB, new PartitionKey(newQpB.id));
            // Mise à jour du supplier (ajout de l'ID dans la liste)
            var supplierBRead = await suppliersContainerB.ReadItemAsync<dynamic>(supplierB.id, new PartitionKey(supplierB.id));
            supplierBRead.Resource.QPs.Add(newQpB.id);
            var updateSupplierBResp = await suppliersContainerB.ReplaceItemAsync<dynamic>(supplierBRead.Resource, supplierB.id, new PartitionKey(supplierB.id));
            swB4.Stop();
            tCreateQpB = swB4.ElapsedMilliseconds;
            Console.WriteLine($"[B] 3. QP created: {newQpB.id}");
            ruCreateQpB = createQpBResponse.RequestCharge;
            ruCreateQpB_updateSupplier = updateSupplierBResp.RequestCharge;
            Console.WriteLine($"[B] RU consumed (create QP): {ruCreateQpB + ruCreateQpB_updateSupplier} | Time: {tCreateQpB} ms");

            // 4. Mise à jour d'une QP
            var updatedQpB = new { id = newQpB.id, property1 = "val1-updated", property2 = "val2-updated" };
            var swB5 = Stopwatch.StartNew();
            var updateQpBResponse = await qpContainerB.ReplaceItemAsync<dynamic>(updatedQpB, updatedQpB.id, new PartitionKey(updatedQpB.id));
            swB5.Stop();
            tUpdateQpB = swB5.ElapsedMilliseconds;
            Console.WriteLine($"[B] 4. QP updated: {updatedQpB.id}");
            ruUpdateQpB = updateQpBResponse.RequestCharge;
            Console.WriteLine($"[B] RU consumed (update QP): {ruUpdateQpB} | Time: {tUpdateQpB} ms");

            // 5. Mettre à jour tous les suppliers
            QueryDefinition allSuppliersQueryB = new QueryDefinition("SELECT * FROM c");
            FeedIterator<dynamic> suppliersIteratorB = suppliersContainerB.GetItemQueryIterator<dynamic>(allSuppliersQueryB);
            var swB6 = Stopwatch.StartNew();
            ruUpdateAllSuppliersB = 0;
            int updatedCountB = 0;
            while (suppliersIteratorB.HasMoreResults)
            {
                FeedResponse<dynamic> response = await suppliersIteratorB.ReadNextAsync();
                ruUpdateAllSuppliersB += response.RequestCharge;
                foreach (var supplier in response)
                {
                    supplier.lastUpdated = DateTime.UtcNow;
                    var upResp = await suppliersContainerB.ReplaceItemAsync<dynamic>(supplier, (string)supplier.id, new PartitionKey((string)supplier.id));
                    ruUpdateAllSuppliersB += upResp.RequestCharge;
                    updatedCountB++;
                }
            }
            swB6.Stop();
            tUpdateAllSuppliersB = swB6.ElapsedMilliseconds;
            Console.WriteLine($"[B] 5. Suppliers updated: {updatedCountB}");
            Console.WriteLine($"[B] RU consumed (update all suppliers): {ruUpdateAllSuppliersB} | Time: {tUpdateAllSuppliersB} ms");

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
        finally
        {
            client.Dispose();
        }
    }

    static async Task InitCosmosAsync(CosmosClient client, string databaseId, string qpContainerId, string suppliersContainerId)
    {
        // Création base et containers si besoin
        DatabaseResponse dbResp = await client.CreateDatabaseIfNotExistsAsync(databaseId);
        Console.WriteLine($"DB created/exist. RU: {dbResp.RequestCharge}");
        Database db = dbResp.Database;

        ContainerResponse qpResp = await db.CreateContainerIfNotExistsAsync(new ContainerProperties(qpContainerId, "/id"));
        Console.WriteLine($"QP container created/exist. RU: {qpResp.RequestCharge}");
        Container qpContainer = qpResp.Container;

        ContainerResponse suppliersResp = await db.CreateContainerIfNotExistsAsync(new ContainerProperties(suppliersContainerId, "/id"));
        Console.WriteLine($"Suppliers container created/exist. RU: {suppliersResp.RequestCharge}");
        Container suppliersContainer = suppliersResp.Container;

        // Création d'une QP
        var qp = new { id = "test-qp-id-1", property1 = "init1", property2 = "init2" };
        var qpCreateResp = await qpContainer.UpsertItemAsync(qp, new PartitionKey(qp.id));
        Console.WriteLine($"QP inserted. RU: {qpCreateResp.RequestCharge}");

        // Création d'un Supplier qui référence la QP
        var supplier = new { id = "test-supplier-id-1", QPs = new List<string> { qp.id }, property1 = "sup1", property2 = "sup2" };
        var supCreateResp = await suppliersContainer.UpsertItemAsync(supplier, new PartitionKey(supplier.id));
        Console.WriteLine($"Supplier inserted. RU: {supCreateResp.RequestCharge}");
    }

}
