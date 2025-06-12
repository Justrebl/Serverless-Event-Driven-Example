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

        public async Task<FeedIterator<T>> QueryItemsAsync<T>(QueryDefinition query) where T : class
        {
            return _container.GetItemQueryIterator<T>(query);
        }
    }
}
