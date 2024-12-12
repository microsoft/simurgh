using Microsoft.SemanticKernel;
using Microsoft.Azure.Cosmos;
using System.ComponentModel;
using Microsoft.Extensions.Options;
using ChatApp.Server.Models.Options;
using System.Dynamic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

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

        ContainerResponse containerResponse;
        if (cosmosOptions.Value.CosmosStructuredDataContainerRUs.HasValue)
        {
            if (cosmosOptions.Value.CosmosStructuredDataContainerRUs.Value < 400)
                throw new Exception("Cannot create a container with less than 400 RUs.");

            containerResponse = _database.CreateContainerIfNotExistsAsync(
                    _containerId,
                    cosmosOptions.Value.CosmosStructuredDataContainerPartitionKey,
                    cosmosOptions.Value.CosmosStructuredDataContainerRUs.Value).Result;
        }
        else
            containerResponse = _database.CreateContainerIfNotExistsAsync(
                _containerId,
                cosmosOptions.Value.CosmosStructuredDataContainerPartitionKey).Result;

        _container = containerResponse.Container;
    }

    [KernelFunction(nameof(ExecuteCosmosQueryAsync))]
    [Description("Execute a query against the Cosmos DB")]
    [return: Description("The result of the query")]
    public async Task<List<dynamic>> ExecuteCosmosQueryAsync([Description("The query to run")] string query)
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
                string json = JsonConvert.SerializeObject(item, Formatting.Indented);
                results.Add(item);
            }
        }

        return results;
    }

    // function for fetching metadata names of columns
    [KernelFunction(nameof(GetColumnNamesAsync))]
    [Description("Get the column names of the Cosmos DB container")]
    [return: Description("The column names of the container")]
    public async Task<List<string>> GetColumnNamesAsync()
    {
        var query = "SELECT * FROM c";
        var iterator = _container.GetItemQueryIterator<JObject>(query, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        var response = await iterator.ReadNextAsync();

        if (response.Count == 0)
            return [];

        var colNames = response.First().Properties().Select(p => p.Name).ToList();

        return colNames;
    }
}
