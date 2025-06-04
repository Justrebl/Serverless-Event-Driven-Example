using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System.IO;

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
            Container? qpContainerA = null;
            Container? suppliersContainerA = null;
            try {
                qpContainerA = database.GetContainer(qpContainerIdA);
            } catch {
                Console.WriteLine("[INFO] Création du container QP_A...");
                var qpRespA = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(qpContainerIdA, "/id"));
                qpContainerA = qpRespA.Container;
            }
            try {
                suppliersContainerA = database.GetContainer(suppliersContainerIdA);
            } catch {
                Console.WriteLine("[INFO] Création du container Suppliers_A...");
                var supRespA = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(suppliersContainerIdA, "/id"));
                suppliersContainerA = supRespA.Container;
            }
            // Initialisation d'un Supplier et d'une QP pour ce schéma
            var supplierA = new { id = "test-supplier-a-id-1", property1 = "sup1", property2 = "sup2" };
            await suppliersContainerA.UpsertItemAsync(supplierA, new PartitionKey(supplierA.id));
            Console.WriteLine($"[A] Supplier upserted: {supplierA.id}");
            var qpA = new { id = "test-qp-a-id-1", property1 = "init1", property2 = "init2", idSupplier = supplierA.id };
            await qpContainerA.UpsertItemAsync(qpA, new PartitionKey(qpA.id));
            Console.WriteLine($"[A] QP upserted: {qpA.id}");
            await Task.Delay(500);

            // Diagnostic : lister tous les documents du container QP_A
            Console.WriteLine("[A] Diagnostic : listing all docs in QP_A...");
            var allQpsA = qpContainerA.GetItemQueryIterator<dynamic>(new QueryDefinition("SELECT * FROM c"));
            while (allQpsA.HasMoreResults)
            {
                var resp = await allQpsA.ReadNextAsync();
                foreach (var doc in resp)
                {
                    try {
                        Console.WriteLine($"[A] QP found in container: id={doc.id} partitionKey={doc.id}");
                    } catch { Console.WriteLine("[A] QP found in container (id/partitionKey non lisible)"); }
                }
            }

            // 1. Trouver une QP par son identifiant
            Console.WriteLine($"[A] Lecture QP id={qpA.id}...");
            var qpReadA = await qpContainerA.ReadItemAsync<dynamic>(qpA.id, new PartitionKey(qpA.id));
            Console.WriteLine($"[A] 1. QP found: {qpReadA.Resource}");
            Console.WriteLine($"[A] RU consumed (find QP by id): {qpReadA.RequestCharge}");

            // 2. Trouver toutes les QP d'un supplier ID (requête sur QP)
            QueryDefinition qpsBySupplierA = new QueryDefinition("SELECT * FROM c WHERE c.idSupplier = @supplierId").WithParameter("@supplierId", supplierA.id);
            FeedIterator<dynamic> qpsBySupplierIteratorA = qpContainerA.GetItemQueryIterator<dynamic>(qpsBySupplierA);
            double ruQpsBySupplierA = 0;
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
            Console.WriteLine($"[A] 2. QP IDs for supplier {supplierA.id}: [{string.Join(", ", qpIdsA)}]");
            Console.WriteLine($"[A] RU consumed (find QPs by supplier): {ruQpsBySupplierA}");

            // 3. Création d'une QP
            var newQpA = new { id = Guid.NewGuid().ToString(), property1 = "val1", property2 = "val2", idSupplier = supplierA.id };
            var createQpAResponse = await qpContainerA.CreateItemAsync<dynamic>(newQpA, new PartitionKey(newQpA.id));
            Console.WriteLine($"[A] 3. QP created: {newQpA.id}");
            Console.WriteLine($"[A] RU consumed (create QP): {createQpAResponse.RequestCharge}");

            // 4. Mise à jour d'une QP
            var updatedQpA = new { id = newQpA.id, property1 = "val1-updated", property2 = "val2-updated", idSupplier = supplierA.id };
            var updateQpAResponse = await qpContainerA.ReplaceItemAsync<dynamic>(updatedQpA, updatedQpA.id, new PartitionKey(updatedQpA.id));
            Console.WriteLine($"[A] 4. QP updated: {updatedQpA.id}");
            Console.WriteLine($"[A] RU consumed (update QP): {updateQpAResponse.RequestCharge}");

            // 5. Mettre à jour tous les suppliers
            QueryDefinition allSuppliersQueryA = new QueryDefinition("SELECT * FROM c");
            FeedIterator<dynamic> suppliersIteratorA = suppliersContainerA.GetItemQueryIterator<dynamic>(allSuppliersQueryA);
            double ruUpdateAllSuppliersA = 0;
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
            Console.WriteLine($"[A] 5. Suppliers updated: {updatedCountA}");
            Console.WriteLine($"[A] RU consumed (update all suppliers): {ruUpdateAllSuppliersA}");

            // --- MODÈLE B (liste d'IDs QP dans Supplier) ---
            Console.WriteLine("\n--- DÉBUT MODÈLE B (liste d'IDs QP dans Supplier, containers QP_B/Suppliers_B) ---");
            string qpContainerIdB = "QP_B";
            string suppliersContainerIdB = "Suppliers_B";
            Container? qpContainerB = null;
            Container? suppliersContainerB = null;
            try {
                qpContainerB = database.GetContainer(qpContainerIdB);
            } catch {
                Console.WriteLine("[INFO] Création du container QP_B...");
                var qpRespB = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(qpContainerIdB, "/id"));
                qpContainerB = qpRespB.Container;
            }
            try {
                suppliersContainerB = database.GetContainer(suppliersContainerIdB);
            } catch {
                Console.WriteLine("[INFO] Création du container Suppliers_B...");
                var supRespB = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(suppliersContainerIdB, "/id"));
                suppliersContainerB = supRespB.Container;
            }
            // Initialisation d'une QP et d'un Supplier legacy
            var qpB = new { id = "test-qp-b-id-1", property1 = "init1", property2 = "init2" };
            await qpContainerB.UpsertItemAsync(qpB, new PartitionKey(qpB.id));
            Console.WriteLine($"[B] QP upserted: {qpB.id}");
            var supplierB = new { id = "test-supplier-b-id-1", QPs = new List<string> { qpB.id }, property1 = "sup1", property2 = "sup2" };
            await suppliersContainerB.UpsertItemAsync(supplierB, new PartitionKey(supplierB.id));
            Console.WriteLine($"[B] Supplier upserted: {supplierB.id}");
            await Task.Delay(500);

            // 1. Trouver une QP par ID
            Console.WriteLine($"[B] Lecture QP id={qpB.id}...");
            var qpReadB = await qpContainerB.ReadItemAsync<dynamic>(qpB.id, new PartitionKey(qpB.id));
            Console.WriteLine($"[B] 1. QP found: {qpReadB.Resource}");
            Console.WriteLine($"[B] RU consumed (find QP by id): {qpReadB.RequestCharge}");

            // 2. Trouver toutes les QP d'un supplier (lecture de la liste d'IDs puis lecture de chaque QP)
            QueryDefinition legacyQpIdsQueryB = new QueryDefinition("SELECT c.QPs FROM c WHERE c.id = @supplierId").WithParameter("@supplierId", supplierB.id);
            FeedIterator<dynamic> legacyQpIdsIteratorB = suppliersContainerB.GetItemQueryIterator<dynamic>(legacyQpIdsQueryB);
            double ruQpsBySupplierB = 0;
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
            // Lecture de chaque QP
            double ruQpReadsB = 0;
            foreach (var id in qpIdsB)
            {
                var qpResp = await qpContainerB.ReadItemAsync<dynamic>(id, new PartitionKey(id));
                ruQpReadsB += qpResp.RequestCharge;
            }
            Console.WriteLine($"[B] 2. QP IDs for supplier {supplierB.id}: [{string.Join(", ", qpIdsB)}]");
            Console.WriteLine($"[B] RU consumed (find QPs by supplier): {ruQpsBySupplierB + ruQpReadsB}");

            // 3. Création d'une QP (et ajout dans la liste du supplier)
            var newQpB = new { id = Guid.NewGuid().ToString(), property1 = "val1", property2 = "val2" };
            var createQpBResponse = await qpContainerB.CreateItemAsync<dynamic>(newQpB, new PartitionKey(newQpB.id));
            // Mise à jour du supplier (ajout de l'ID dans la liste)
            var supplierBRead = await suppliersContainerB.ReadItemAsync<dynamic>(supplierB.id, new PartitionKey(supplierB.id));
            supplierBRead.Resource.QPs.Add(newQpB.id);
            var updateSupplierBResp = await suppliersContainerB.ReplaceItemAsync<dynamic>(supplierBRead.Resource, supplierB.id, new PartitionKey(supplierB.id));
            Console.WriteLine($"[B] 3. QP created: {newQpB.id}");
            Console.WriteLine($"[B] RU consumed (create QP): {createQpBResponse.RequestCharge + updateSupplierBResp.RequestCharge}");

            // 4. Mise à jour d'une QP
            var updatedQpB = new { id = newQpB.id, property1 = "val1-updated", property2 = "val2-updated" };
            var updateQpBResponse = await qpContainerB.ReplaceItemAsync<dynamic>(updatedQpB, updatedQpB.id, new PartitionKey(updatedQpB.id));
            Console.WriteLine($"[B] 4. QP updated: {updatedQpB.id}");
            Console.WriteLine($"[B] RU consumed (update QP): {updateQpBResponse.RequestCharge}");

            // 5. Mettre à jour tous les suppliers
            QueryDefinition allSuppliersQueryB = new QueryDefinition("SELECT * FROM c");
            FeedIterator<dynamic> suppliersIteratorB = suppliersContainerB.GetItemQueryIterator<dynamic>(allSuppliersQueryB);
            double ruUpdateAllSuppliersB = 0;
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
            Console.WriteLine($"[B] 5. Suppliers updated: {updatedCountB}");
            Console.WriteLine($"[B] RU consumed (update all suppliers): {ruUpdateAllSuppliersB}");

            // --- COMPARATIF FINAL ---
            Console.WriteLine("\n--- COMPARATIF FINAL (RU consommées) ---");
            Console.WriteLine("1. Trouver une QP par ID :");
            Console.WriteLine($"  Modèle B : {qpReadB.RequestCharge} RU | Modèle A : {qpReadA.RequestCharge} RU");
            Console.WriteLine("2. Trouver toutes les QP d’un supplier :");
            Console.WriteLine($"  Modèle B : {ruQpsBySupplierB + ruQpReadsB} RU | Modèle A : {ruQpsBySupplierA} RU");
            Console.WriteLine("3. Créer une QP :");
            Console.WriteLine($"  Modèle B : {createQpBResponse.RequestCharge + updateSupplierBResp.RequestCharge} RU | Modèle A : {createQpAResponse.RequestCharge} RU");
            Console.WriteLine("4. Mettre à jour une QP :");
            Console.WriteLine($"  Modèle B : {updateQpBResponse.RequestCharge} RU | Modèle A : {updateQpAResponse.RequestCharge} RU");
            Console.WriteLine("5. Mettre à jour tous les suppliers :");
            Console.WriteLine($"  Modèle B : {ruUpdateAllSuppliersB} RU | Modèle A : {ruUpdateAllSuppliersA} RU");
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
