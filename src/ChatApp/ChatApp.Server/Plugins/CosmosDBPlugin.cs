using Microsoft.SemanticKernel;
using Microsoft.Azure.Cosmos;
using System.ComponentModel;
using Microsoft.Extensions.Options;
using ChatApp.Server.Models.Options;
using System.Dynamic;

namespace ChatApp.Server.Plugins;

public class CosmosDBPlugin
{
    private readonly CosmosClient _cosmosClient;
    private Database _database;
    private Microsoft.Azure.Cosmos.Container _container;
    private readonly string _databaseId;
    private readonly string _containerId;

    public CosmosDBPlugin(CosmosClient cosmosClient, IOptions<CosmosOptions> cosmosOptions)
    {
        _cosmosClient = cosmosClient;

        _databaseId = cosmosOptions.Value.CosmosDatabaseId;
        _containerId = cosmosOptions.Value.CosmosStructuredDataContainerId;

        var dbResponse = _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId).Result;
        _database = dbResponse.Database;
        // splitting 1k throughput across history container and structured data container
        // todo: parameterize throughput and partition key as configuration
        var containerResponse = _database.CreateContainerIfNotExistsAsync(_containerId, "/partitionKey", 400).Result;
        _container = containerResponse.Container;
    }

    [KernelFunction("cosmos_query")]
    [Description("Query the Cosmos DB")]
    [return: Description("The result of the query")]
    public async Task<List<dynamic>> CosmosQueryAsync([Description("The query to run")] string query)
    {
        var queryDefinition = new QueryDefinition(query);

        // TODO: Double check to make sure the query is safe
        var queryResultSetIterator = _container.GetItemQueryIterator<ExpandoObject>(queryDefinition);

        List<dynamic> results = new List<dynamic>();

        while (queryResultSetIterator.HasMoreResults)
        {
            FeedResponse<ExpandoObject> currentResultSet = await queryResultSetIterator.ReadNextAsync();
            foreach (var item in currentResultSet)
            {
                results.Add(item);
            }
        }

        return results;
    }
}
