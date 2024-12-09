using Microsoft.SemanticKernel;
using Microsoft.Azure.Cosmos;
using System.ComponentModel;
using Microsoft.Extensions.Options;
using ChatApp.Server.Models.Options;

namespace ChatApp.Server.Plugins;

public class CosmosDBPlugin
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Microsoft.Azure.Cosmos.Container _container;
    private readonly string _databaseId;
    private readonly string _containerId;

    public CosmosDBPlugin(CosmosClient cosmosClient, IOptionsSnapshot<CosmosOptions> cosmosOptions)
    {
        _cosmosClient = cosmosClient;

        var structuredDataOptions = cosmosOptions.Get("StructuredData");
        _databaseId = structuredDataOptions.CosmosDatabaseId;
        _containerId = structuredDataOptions.CosmosContainerId;
        _database = _cosmosClient.GetDatabase(_databaseId);
        _container = _cosmosClient.GetContainer(_databaseId, _containerId);
    }

    [KernelFunction("cosmos_query")]
    [Description("Query the Cosmos DB")]
    [return: Description("The result of the query")]
    public async Task<List<dynamic>> CosmosQueryAsync([Description("The query to run")] string query)
    {
        var queryDefinition = new QueryDefinition(query);
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