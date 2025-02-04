﻿using ChatApp.Server.Models;
using ChatApp.Server.Models.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;

namespace ChatApp.Server.Services;

internal class CosmosConversationService
{
    private readonly CosmosClient _cosmosClient;
    private Database _database;
    private Container _container;
    private readonly string _databaseId;
    private readonly string _containerId;

    public CosmosConversationService(CosmosClient cosmosClient, IOptions<CosmosOptions> cosmosOptions)
    {
        _cosmosClient = cosmosClient;

        _databaseId = cosmosOptions.Value.CosmosDatabaseId;
        _containerId = cosmosOptions.Value.ContainerId;

        // TodoItems >> db name
        // Items >> structured data
        // need to create a container for History


        var dbResponse = _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId).Result;
        _database = dbResponse.Database;
        // splitting 1k throughput across history container and structured data container
        // todo: parameterize throughput and partition key as configuration

        ContainerResponse containerResponse;
        if (cosmosOptions.Value.ContainerRUs.HasValue)
        {
            if (cosmosOptions.Value.ContainerRUs.Value < 400)
                throw new Exception("Cannot create a container with less than 400 RUs.");

            containerResponse = _database.CreateContainerIfNotExistsAsync(
                    _containerId,
                    cosmosOptions.Value.PartitionKey,
                    cosmosOptions.Value.ContainerRUs.Value).Result;
        }
        else
            containerResponse = _database.CreateContainerIfNotExistsAsync(
                _containerId,
                cosmosOptions.Value.PartitionKey).Result;

        _container = containerResponse.Container;
    }

    // todo: evaluate for moving into constructor and switching from scoped to singleton
    internal async Task<(bool, Exception?)> EnsureAsync()
    {
        if (_cosmosClient == null || _database == null || _container == null)
            return (false, new Exception($"CosmosDB database with ID {_databaseId} on account {_cosmosClient?.Endpoint} not initialized correctly."));

        try
        {
            var dbInfo = await _database.ReadAsync();
        }
        catch (Exception readException)
        {
            return (false, new Exception($"CosmosDB database with ID {_databaseId} on account {_cosmosClient?.Endpoint} not found.", readException));
        }

        try
        {
            var containerInfo = await _container.ReadContainerAsync();
        }
        catch (Exception readException)
        {
            return (false, new Exception($"CosmosDB container with ID {_databaseId} on account {_cosmosClient?.Endpoint} not found.", readException));
        }

        return (true, null);  // return True, "CosmosDB client initialized successfully"
    }

    internal async Task<Conversation> CreateConversationAsync(string userId, string title = "")
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid().ToString(),
            Type = "conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserId = userId,
            Title = title
        };

        //## TODO: add some error handling based on the output of the upsert_item call
        var response = await _container.UpsertItemAsync(conversation);

        if (response != null)
            return response;
        else
            throw new Exception("Failed to create conversation.");
    }

    internal async Task<Conversation?> UpdateConversationAsync(string userId, Conversation conversation)
    {
        // verify we have an id
        if (string.IsNullOrWhiteSpace(conversation.Id))
            throw new Exception("Conversation ID is required.");

        var dbConversation = await GetConversationAsync(userId, conversation.Id);

        if (dbConversation != null && dbConversation?.UserId != userId)
            throw new Exception("Conversation does not belong to user.");

        conversation.UpdatedAt = DateTime.UtcNow;

        var response = await _container.UpsertItemAsync(conversation);

        return response.Resource;
    }

    internal async Task<bool> DeleteConversationAsync(string userId, string conversationId)
    {
        // todo: make sure we delete related messages as well
        var conversation = await _container.ReadItemAsync<Conversation>(conversationId, new PartitionKey(userId));

        if (conversation != null)
        {
            var response = await _container.DeleteItemAsync<Conversation>(conversationId, new PartitionKey(userId));
            return response != null; // todo: in original code, some branches offer the deleted item as a return value while others return a boolean
        }
        else
        {
            return true;
        }
    }

    internal async Task<bool> DeleteConversationsAsync(string userId)
    {
        // todo: is return type of bool worthwile?
        var iterator = _container.GetItemLinqQueryable<Conversation>()
           .Where(m => m.UserId == userId)
           .ToFeedIterator();

        var tasks = new List<Task>();

        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                tasks.Add(_container.DeleteItemAsync<Conversation>(item.Id, new PartitionKey(userId)));
            }
        }

        await Task.WhenAll(tasks);

        return true;
    }

    internal async Task DeleteMessagesAsync(string conversationId, string userId)
    {
        var conversation = await GetConversationAsync(userId, conversationId) ?? throw new Exception($"Failed to find conversation with id: {conversationId}");
        conversation.Messages.Clear();
        _ = await UpdateConversationAsync(userId, conversation) ?? throw new Exception($"Failed to update conversation with id: {conversationId}");
    }

    internal async Task<IAsyncEnumerable<HistoryMessage>> GetMessagesAsync(string userId, string conversationId)
    {
        var conversation = await GetConversationAsync(userId, conversationId) ?? throw new Exception($"Failed to find conversation with id: {conversationId}");
        return conversation.Messages.OfType<HistoryMessage>().ToAsyncEnumerable();
    }

    public async IAsyncEnumerable<Conversation> GetConversationsAsync(string userId, int? limit = null, bool sortDesc = true, int offset = 0)
    {
        var query = _container.GetItemLinqQueryable<Conversation>()
            .Where(m => m.UserId == userId && m.Type == "conversation")

            .Skip(offset);

        if (sortDesc)
            query.OrderByDescending(m => m.UpdatedAt);
        else
            query.OrderBy(m => m.UpdatedAt);

        if (limit.HasValue)
            query.Take(limit.Value);

        using FeedIterator<Conversation> feed = query
            .ToFeedIterator();

        while (feed.HasMoreResults)
        {
            foreach (var session in await feed.ReadNextAsync())
            {
                yield return session;
            }
        }
    }

    public async Task<Conversation?> GetConversationAsync(string userId, string conversationId)
    {
        try
        {
            var response = await _container.ReadItemAsync<Conversation>(conversationId, new PartitionKey(userId));

            return response?.Resource;
        }
        catch (CosmosException)
        {
            return null;
        }
    }

    public async Task<HistoryMessage?> UpdateMessageFeedbackAsync(string userId, string conversationId, string messageId, string feedback)
    {
        var conversation = await GetConversationAsync(userId, conversationId)
            ?? throw new Exception($"Failed to find conversation with id: {conversationId}");

        var message = conversation.Messages.FirstOrDefault(m => m.Id.Equals(messageId, StringComparison.OrdinalIgnoreCase));
        if (message == null)
            return null;

        message.Feedback = feedback;
        var response = await UpdateConversationAsync(userId, conversation) ?? throw new Exception($"Failed to update conversation with id: {conversationId}");

        return message;
    }
}
