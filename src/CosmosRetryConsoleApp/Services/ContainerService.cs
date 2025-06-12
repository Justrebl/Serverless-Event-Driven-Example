using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmosRetryConsoleApp.Models;

namespace CosmosRetryConsoleApp.Services
{
    public class ContainerService
    {
        private readonly Container _container;

        public ContainerService(Container container)
        {
            _container = container;
        }

        public async Task<ItemResponse<T>> UpsertItemAsync<T>(T item, string partitionKey) where T : class
        {
            return await _container.UpsertItemAsync(item, new PartitionKey(partitionKey));
        }

        public async Task<ItemResponse<T>> CreateItemAsync<T>(T item, string partitionKey) where T : class
        {
            return await _container.CreateItemAsync(item, new PartitionKey(partitionKey));
        }

        public async Task<ItemResponse<T>> ReplaceItemAsync<T>(T item, string id) where T : class
        {
            return await _container.ReplaceItemAsync(item, id, new PartitionKey(id));
        }

        public async Task<ItemResponse<T>> ReadItemAsync<T>(string id) where T : class
        {
            return await _container.ReadItemAsync<T>(id, new PartitionKey(id));
        }

        public async Task<List<T>> QueryItemsAsync<T>(string query, Dictionary<string, object> parameters = null) where T : class
        {
            var items = new List<T>();
            QueryDefinition queryDef = new QueryDefinition(query);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    queryDef = queryDef.WithParameter(param.Key, param.Value);
                }
            }
            FeedIterator<T> iterator = _container.GetItemQueryIterator<T>(queryDef);
            while (iterator.HasMoreResults)
            {
                FeedResponse<T> response = await iterator.ReadNextAsync();
                items.AddRange(response);
            }
            return items;
        }
    }
}
