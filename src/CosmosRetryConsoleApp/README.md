## Grant access to Cosmos DB with an identity

> **Note:**
> There is currently no role assignment option available in the Azure portal for Cosmos DB RBAC. You must use Azure CLI, PowerShell, or an ARM template.

### Azure CLI commands

# Create a service principal (SPN) if you want to use an app registration
https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app

# Grant the access
```
resourceGroupName='<myResourceGroup>'
accountName='<myCosmosAccount>'
readOnlyRoleDefinitionId='00000000-0000-0000-0000-000000000002' # Cosmos DB Built-in Data Contributor role definition ID
principalId='<your-object-id>' # Object ID of the managed identity

az cosmosdb sql role assignment create \
  --account-name $accountName \
  --resource-group $resourceGroupName \
  --scope "/" \
  --principal-id $principalId \
  --role-definition-id $readOnlyRoleDefinitionId
```

For more details, see the official documentation: [Grant access - Managed Identity & Cosmos DB](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/tutorial-vm-managed-identities-cosmos?tabs=azure-cli#grant-access)


## Open the workspace in a Dev Container and run the project

1. Open the `/workspaces/Serverless-Event-Driven-Example` folder in Visual Studio Code.
2. If prompted, or from the Command Palette (`F1`), select **"Reopen in Container"** to open the project in the Dev Container environment (recommended for all dependencies and tools).
3. In the file explorer, navigate to `src/CosmosRetryConsoleApp/Program.cs`.
4. Press `F5` to start the project in debug mode (or click the "Run"/"Start Debugging" button).
5. Make sure the `appsettings.json` file in `src/CosmosRetryConsoleApp/` is properly configured with your Azure settings.

The project entry point is:
- `src/CosmosRetryConsoleApp/Program.cs`

The main configuration file is:
- `src/CosmosRetryConsoleApp/appsettings.json`

---

## Useful links

- [CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.maxretryattemptsonratelimitedrequests?view=azure-dotnet)
- [CosmosClientOptions.MaxRetryWaitTimeOnRateLimitedRequests](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.maxretrywaittimeonratelimitedrequests?view=azure-dotnet)
- [Tune connection configurations for .NET SDK v3](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/tune-connection-configurations-net-sdk-v3)
- [Best practices for .NET and Azure Cosmos DB](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/best-practice-dotnet)